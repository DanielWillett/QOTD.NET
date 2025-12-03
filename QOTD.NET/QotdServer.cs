#if USE_MS_EXTENTIONS
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#endif
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QOTD.NET;

/// <summary>
/// A server that hosts quotes from a <see cref="IQuoteProvider"/> according to the <see href="https://www.rfc-editor.org/rfc/rfc865">QOTD protocol (RFC-865)</see>.
/// </summary>
public sealed class QotdServer : BaseQotdHost
#if USE_MS_EXTENTIONS
    , IHostedService
#endif
{
    private readonly IQuoteProvider _quoteProvider;
    private bool _isDisposing;
    private UdpClient? _udpClient;
    private TcpListener? _tcpServer;
    private AsyncCallback? _tcpConnectionAccepted;
    private AsyncCallback? _udpMessageAccepted;
    private int _eventCt;
#if USE_MS_EXTENTIONS
    private IDisposable? _optionsChangeListener;
    private bool _isHosted;
#endif

    /// <summary>
    /// The default encoding used for QOTD hosts.
    /// </summary>
    /// <remarks>The value of this property is an ASCII encoding that will throw an exception if invalid characters are found.</remarks>
    public static ASCIIEncoding DefaultEncoding { get; }

    static QotdServer()
    {
        ASCIIEncoding encoding = (ASCIIEncoding)Encoding.ASCII.Clone();
        encoding.EncoderFallback = EncoderFallback.ExceptionFallback;
        encoding.DecoderFallback = DecoderFallback.ExceptionFallback;
        DefaultEncoding = encoding;
    }

    /// <summary>
    /// The default port allocated for the QOTD protocol.
    /// </summary>
    /// <remarks>This is managed by IANA at <see href="https://www.iana.org/assignments/service-names-port-numbers/service-names-port-numbers.xhtml"/>.</remarks>
    public const ushort DefaultPort = 17;

    /// <summary>
    /// Which transport protocols are accepted for QOTD requests.
    /// </summary>
    public QotdServerMode Mode { get; private set; }

    /// <summary>
    /// The port used for the <see cref="UdpClient"/>, if it's enabled.
    /// </summary>
    public ushort UdpPort { get; private set; }

    /// <summary>
    /// The port used for the <see cref="TcpListener"/>, if it's enabled.
    /// </summary>
    public ushort TcpPort { get; private set; }

    /// <summary>
    /// Whether or not to allow connections over IPv6 as well as IPv4.
    /// </summary>
    public bool DualMode { get; private set; }

    /// <summary>
    /// The encoding to use for quotes.
    /// <para>
    /// Using an encoding other than ASCII is not supported by the protocol and will have to be explicitly supported by clients.
    /// </para>
    /// </summary>
    /// <remarks>Defaults to ASCII with an exception fallback.</remarks>
    public Encoding Encoding
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = DefaultEncoding;

    /// <summary>
    /// Create a new QOTD protocol server. Starts listening immediately until <see cref="Dispose"/> is called.
    /// </summary>
    /// <param name="quoteProvider">Abstraction that provides quotes to requests.</param>
    /// <param name="mode">Which protocols to accept connections from.</param>
    /// <param name="port">The port to listen on.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is not a valid port number.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="quoteProvider"/> is <see langword="null"/>.</exception>
    public QotdServer(IQuoteProvider quoteProvider, QotdServerMode mode, ushort port = DefaultPort) :
        this(quoteProvider, mode, port == 0 ? throw new ArgumentOutOfRangeException(nameof(port)) : port, port) { }

    /// <summary>
    /// Create a new QOTD protocol server. Starts listening immediately until <see cref="Dispose"/> is called.
    /// </summary>
    /// <param name="quoteProvider">Abstraction that provides quotes to requests.</param>
    /// <param name="mode">Which protocols to accept connections from.</param>
    /// <param name="udpPort">The port to listen on for the UDP server.</param>
    /// <param name="tcpPort">The port to listen on for the TCP server.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="udpPort"/> or <paramref name="tcpPort"/> is not a valid port number.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="quoteProvider"/> is <see langword="null"/>.</exception>
    public QotdServer(IQuoteProvider quoteProvider, QotdServerMode mode, ushort udpPort, ushort tcpPort)
    {
        if (mode is < 0 or > QotdServerMode.Both)
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (udpPort == 0)
            throw new ArgumentOutOfRangeException(nameof(udpPort));
        if (tcpPort == 0)
            throw new ArgumentOutOfRangeException(nameof(tcpPort));

        _quoteProvider = quoteProvider ?? throw new ArgumentNullException(nameof(quoteProvider));

        StartListening(mode, udpPort, tcpPort, dualMode: true);
    }

    private void StartListening(QotdServerMode mode, ushort udpPort, ushort tcpPort, bool dualMode)
    {
        // assume locked or in ctor
        Mode = mode;

        // wait for current events to finish
        SpinWait.SpinUntil(() => _eventCt == 0, 5000);

        Interlocked.Exchange(ref _udpClient, null)?.Dispose();
        if (mode is QotdServerMode.Udp or QotdServerMode.Both)
        {
            if (udpPort == 0)
                udpPort = DefaultPort;

            UdpClient udpClient = new UdpClient(dualMode ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork);
            UdpPort = udpPort;
            if (dualMode)
            {
                udpClient.Client.DualMode = true;
            }
            udpClient.Client.Bind(new IPEndPoint(dualMode ? IPAddress.IPv6Any : IPAddress.Any, udpPort));
            _udpMessageAccepted ??= UdpMessageReceived;
            _udpClient = udpClient;
            udpClient.BeginReceive(_udpMessageAccepted, udpClient);
        }
        else
        {
            _udpClient = null;
        }

        // wait for current events to finish
        SpinWait.SpinUntil(() => _eventCt == 0, 5000);

        Interlocked.Exchange(ref _tcpServer, null)?.Stop();
        if (mode is QotdServerMode.Tcp or QotdServerMode.Both)
        {
            if (tcpPort == 0)
                tcpPort = DefaultPort;

            TcpListener tcpServer = new TcpListener(dualMode ? IPAddress.IPv6Any : IPAddress.Any, tcpPort);
            TcpPort = tcpPort;
            if (dualMode)
            {
                tcpServer.Server.DualMode = true;
            }
            tcpServer.Start();
            _tcpConnectionAccepted ??= TcpConnectionAccepted;
            _tcpServer = tcpServer;
            tcpServer.BeginAcceptTcpClient(_tcpConnectionAccepted, tcpServer);
        }
        else
        {
            _tcpServer = null;
        }

        DualMode = dualMode;
    }

#if USE_MS_EXTENTIONS
    /// <summary>
    /// Create a new QOTD protocol server. Starts listening when this service is hosted.
    /// </summary>
    /// <param name="quoteProvider">Abstraction that provides quotes to requests.</param>
    /// <param name="options">Configuration for <see cref="QotdServer"/>.</param>
    /// <param name="logger">Logger for socket exceptions and other messages.</param>
    [ActivatorUtilitiesConstructor]
    public QotdServer(IQuoteProvider quoteProvider, ILogger<QotdServer> logger, IOptionsMonitor<QotdServerOptions> options)
        : base(logger)
    {
        _quoteProvider = quoteProvider;

        QotdServerOptions value = options.CurrentValue;
        Mode = value.Mode;
        UdpPort = value.UdpPort ?? value.Port;
        TcpPort = value.TcpPort ?? value.Port;
        DualMode = value.DualMode;
        Encoding = value.Encoding;
        UpdateFromOptions(value);

        _optionsChangeListener = options.OnChange(HandleOptionsChange);
    }

    private void HandleOptionsChange(QotdServerOptions options, string? optionsName)
    {
        lock (this)
        {
            QotdServerMode mode = options.Mode;
            ushort udp = options.UdpPort ?? options.Port;
            ushort tcp = options.TcpPort ?? options.Port;
            bool dualMode = options.DualMode;
            Encoding = options.Encoding;
            UpdateFromOptions(options);

            if (_isHosted)
            {
                StartListening(mode, udp, tcp, dualMode);
            }
            else
            {
                Mode = mode;
                TcpPort = tcp;
                UdpPort = udp;
                DualMode = dualMode;
            }
        }
    }

    Task IHostedService.StartAsync(CancellationToken token)
    {
        lock (this)
        {
            _isHosted = true;
            StartListening(Mode, UdpPort, TcpPort, DualMode);
        }
        return Task.CompletedTask;
    }

    async Task IHostedService.StopAsync(CancellationToken token)
    {
        _isDisposing = true;
        await Task.Run(
            () => SpinWait.SpinUntil(() => _eventCt == 0, 5000),
            CancellationToken.None
        ).ConfigureAwait(false);
        lock (this)
        {
            _isHosted = false;
            Dispose();
        }
    }
#endif

    #region UDP

    private void UdpMessageReceived(IAsyncResult ar)
    {
        UdpClient client = (UdpClient)ar.AsyncState!;
        bool dispose = true;
        try
        {
            IPEndPoint? ipEndpoint = null;
            _ = client.EndReceive(ar, ref ipEndpoint);

            if (ipEndpoint == null)
                return;

#if USE_MS_EXTENTIONS
            try
            {
                if (Logger.IsEnabled(LogLevel.Trace))
                {
                    Logger.LogTrace(
                        string.Format(
                            Properties.Resources.Trace_QotdServerReceiveRequest,
                            ipEndpoint,
                            "UDP"
                        )
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(Properties.Resources.LoggerException);
                Debug.WriteLine(ex);
            }
#endif

            ValueTask<string> task;
            try
            {
                task = _quoteProvider.GetQuote(ipEndpoint.Address, GetToken());
            }
            catch (Exception ex)
            {
                LogError(Properties.Resources.QotdServerExceptionThrownByQuoteProvider, ex);
                task = default;
            }

            if (task.IsCompleted)
            {
                string? quote = task.Result;
                if (quote != null)
                {
                    dispose = false;
                    HandleQuoteHandshakeUdp(client, ipEndpoint, quote);
                }
            }
            else
            {
                dispose = false;
                ValueTask<string> vt = task;
                UdpClient uc = client;
                IPEndPoint ep = ipEndpoint;
                Task.Run(async () =>
                {
                    string? quote;
                    try
                    {
                        quote = await vt.ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogError(Properties.Resources.QotdServerExceptionThrownByQuoteProvider, ex);
                        quote = null;
                    }

                    if (quote == null)
                        return;

                    HandleQuoteHandshakeUdp(uc, ep, quote);
                });
            }

            if (!_isDisposing)
            {
                UdpClient? udpClient = _udpClient;
                udpClient?.BeginReceive(_udpMessageAccepted, udpClient);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            if (dispose)
            {
                client?.Dispose();
            }
        }
    }

    private async void HandleQuoteHandshakeUdp(UdpClient udpClient, IPEndPoint endPoint, string quote)
    {
        if (_isDisposing)
            return;

        Interlocked.Increment(ref _eventCt);
        if (_isDisposing)
        {
            Interlocked.Decrement(ref _eventCt);
            return;
        }

        try
        {
            byte[]? buffer;
            int bytesWritten;
            try
            {
                buffer = WriteQuote(quote, out bytesWritten);
            }
            catch
            {
                // already logged by WriteQuote
                buffer = null;
                bytesWritten = 0;
            }

            if (buffer == null)
                return;

            try
            {
                await udpClient.SendAsync(buffer, bytesWritten, endPoint).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogError(Properties.Resources.QotdServerTransportError, ex);
            }
            finally
            {
                ReturnBuffer(buffer);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _eventCt);
        }
    }

    #endregion


    #region TCP

    private void TcpConnectionAccepted(IAsyncResult ar)
    {
        TcpClient? client = null;
        bool dispose = true;
        try
        {
            client = ((TcpListener)ar.AsyncState!).EndAcceptTcpClient(ar);
            IPAddress address;
            if (client.Client.RemoteEndPoint is IPEndPoint ipEp)
                address = ipEp.Address;
            else
                address = IPAddress.None;

#if USE_MS_EXTENTIONS
            try
            {
                if (Logger.IsEnabled(LogLevel.Trace))
                {
                    Logger.LogTrace(
                        string.Format(
                            Properties.Resources.Trace_QotdServerReceiveRequest,
                            address,
                            "TCP"
                        )
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(Properties.Resources.LoggerException);
                Debug.WriteLine(ex);
            }
#endif

            ValueTask<string> task;
            try
            {
                task = _quoteProvider.GetQuote(address, GetToken());
            }
            catch (Exception ex)
            {
                task = default;
                LogError(Properties.Resources.QotdServerExceptionThrownByQuoteProvider, ex);
            }

            if (task.IsCompleted)
            {
                string? quote = task.Result;
                if (quote != null)
                {
                    dispose = false;
                    HandleQuoteHandshakeTcp(client, quote);
                }
            }
            else
            {
                dispose = false;
                TcpClient s = client;
                ValueTask<string> vt = task;
                Task.Run(async () =>
                {
                    string? quote;
                    try
                    {
                        quote = await vt.ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogError(Properties.Resources.QotdServerExceptionThrownByQuoteProvider, ex);
                        quote = null;
                    }

                    if (quote == null)
                    {
                        s.Dispose();
                        return;
                    }

                    HandleQuoteHandshakeTcp(s, quote);
                });
            }

            if (!_isDisposing)
            {
                TcpListener? tcpListener = _tcpServer;
                tcpListener?.BeginAcceptTcpClient(_tcpConnectionAccepted, tcpListener);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            if (dispose)
            {
                client?.Dispose();
            }
        }
    }

    private async void HandleQuoteHandshakeTcp(TcpClient tcpClient, string quote)
    {
        if (_isDisposing)
        {
            tcpClient.Close();
            tcpClient.Dispose();
            return;
        }

        Interlocked.Increment(ref _eventCt);
        if (_isDisposing)
        {
            try
            {
                tcpClient.Close();
                tcpClient.Dispose();
            }
            finally
            {
                Interlocked.Decrement(ref _eventCt);
            }
            return;
        }

        try
        {
            byte[]? buffer;
            int bytesWritten;
            try
            {
                buffer = WriteQuote(quote, out bytesWritten);
            }
            catch
            {
                // already logged by WriteQuote
                buffer = null;
                bytesWritten = 0;
            }

            if (buffer == null)
                return;

            try
            {
                if (!tcpClient.Connected)
                    return;

                NetworkStream networkStream = tcpClient.GetStream();

                await networkStream.WriteAsync(buffer, 0, bytesWritten, GetToken()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogError(Properties.Resources.QotdServerTransportError, ex);
            }
            finally
            {
                ReturnBuffer(buffer);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _eventCt);
            tcpClient.Close();
            tcpClient.Dispose();
        }
    }

    #endregion

    private unsafe byte[]? WriteQuote(string quote, out int bytesWritten)
    {
        byte[] buffer = RentBuffer();
        Encoding encoding = Encoding;
        try
        {
            bytesWritten = encoding.GetBytes(quote, 0, quote.Length, buffer, 0);
        }
        catch (EncoderFallbackException)
        {
            ReturnBuffer(buffer);
            LogError(string.Format(Properties.Resources.QotdServerUnexpectedCharacterInEncoding, encoding.EncodingName));
            bytesWritten = 0;
            return null;
        }
        catch (ArgumentException) // string is too long, truncate
        {
            Encoder encoder = encoding.GetEncoder();
            fixed (char* chAddr = quote)
            fixed (byte* bnAddr = buffer)
            {
                try
                {
                    encoder.Convert(chAddr, quote.Length, bnAddr, buffer.Length, flush: true, out _, out bytesWritten, out _);
                }
                catch (EncoderFallbackException)
                {
                    ReturnBuffer(buffer);
                    LogError(string.Format(Properties.Resources.QotdServerUnexpectedCharacterInEncoding, encoding.EncodingName));
                    bytesWritten = 0;
                    return null;
                }
            }

            LogError(Properties.Resources.QotdServerQuoteTruncated);
        }
        catch
        {
            ReturnBuffer(buffer);
            bytesWritten = 0;
            throw;
        }

        return buffer;
    }

    private protected override void Dispose(bool disposing)
    {
#if USE_MS_EXTENTIONS
        Interlocked.Exchange(ref _optionsChangeListener, null)?.Dispose();
#endif
        if (!_isDisposing)
        {
            // wait for all requests to finish
            _isDisposing = true;
            SpinWait.SpinUntil(() => _eventCt == 0, 5000);
        }

        base.Dispose(disposing);

        Interlocked.Exchange(ref _udpClient, null)?.Dispose();
        Interlocked.Exchange(ref _tcpServer, null)?.Stop();

        _ = disposing;
    }
}