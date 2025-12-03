#if USE_MS_EXTENTIONS
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#endif
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QOTD.NET;

/// <summary>
/// A client that requests quotes from a server according to the <see href="https://www.rfc-editor.org/rfc/rfc865">QOTD protocol (RFC-865)</see>.
/// </summary>
public sealed class QotdClient : BaseQotdHost
{
    private bool _isDisposing;
    private int _requestCt;

    private static IPEndPoint? _any4;
    private static IPEndPoint? _any6;

#if USE_MS_EXTENTIONS
    private IDisposable? _optionsChangeListener;
#endif

    /// <summary>
    /// Protocol to use when connecting to a server. Either TCP or UDP.
    /// </summary>
    public QotdClientMode Mode { get; private set; }

    /// <summary>
    /// The endpoint being connected to.
    /// </summary>
    public IPEndPoint EndPoint { get; private set; }

    /// <summary>
    /// Default timeout for quote requests. Negative values indicate an infinite timeout.
    /// </summary>
    /// <remarks>Defaults to 5 seconds.</remarks>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(5d);

    /// <summary>
    /// The encoding to use for quotes.
    /// <para>
    /// Most servers will send quotes in ASCII format, as specified by the protocol.
    /// </para>
    /// </summary>
    /// <remarks>Defaults to ASCII with an exception fallback.</remarks>
    public Encoding Encoding
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = QotdServer.DefaultEncoding;

    /// <summary>
    /// Create a new client implementing the QOTD protocol.
    /// </summary>
    /// <param name="mode">Transport protocol to use when connecting.</param>
    /// <param name="address">Address of the server to connect to.</param>
    /// <param name="port">Port of the server to connect to.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> or <paramref name="port"/> is an invalid value.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is <see langword="null"/>.</exception>
    public QotdClient(QotdClientMode mode, IPAddress address, ushort port = QotdServer.DefaultPort)
        : this(mode,
            new IPEndPoint(
                address ?? throw new ArgumentNullException(nameof(address)),
                port == 0 ? throw new ArgumentOutOfRangeException(nameof(port)) : port
                )
        )
    {

    }

    /// <summary>
    /// Create a new client implementing the QOTD protocol.
    /// </summary>
    /// <param name="mode">Transport protocol to use when connecting.</param>
    /// <param name="endPoint">Address and port of the server to connect to.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is an invalid value.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="endPoint"/> is <see langword="null"/>.</exception>
    public QotdClient(QotdClientMode mode, IPEndPoint endPoint)
    {
        if (mode is not QotdClientMode.Tcp and not QotdClientMode.Udp)
            throw new ArgumentOutOfRangeException(nameof(mode));

        Mode = mode;
        EndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
    }

#if USE_MS_EXTENTIONS
    /// <summary>
    /// Create a new QOTD protocol client.
    /// </summary>
    /// <param name="options">Configuration for <see cref="QotdClient"/>.</param>
    /// <param name="logger">Logger for socket exceptions and other messages.</param>
    [ActivatorUtilitiesConstructor]
    public QotdClient(ILogger<QotdClient> logger, IOptionsMonitor<QotdClientOptions> options) : base(logger)
    {
        EndPoint = null!;

        UpdateFromOptions(options.CurrentValue);
        
        _optionsChangeListener = options.OnChange(HandleOptionsChange);
    }
#endif

    /// <summary>
    /// Request a single quote from the server host.
    /// <para>
    /// This method does not implement any retry functionality.
    /// </para>
    /// </summary>
    /// <param name="token"><see cref="CancellationToken"/> for cancelling the request early..</param>
    /// <param name="timeout">Timeout for overall handshake duration. If left <see langword="default"/>, uses <see cref="DefaultTimeout"/>. A negative <see cref="TimeSpan"/> indicates an infinite timeout.</param>
    /// <returns>The quote that was returned from the server as a <see cref="string"/>.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this object is disposed at the time of the request.</exception>
    public Task<string> RequestQuoteAsync(TimeSpan timeout = default, CancellationToken token = default)
    {
        QotdClientMode mode;
        IPEndPoint endPoint;
        Encoding encoding = Encoding;
        lock (this)
        {
            mode = Mode;
            endPoint = EndPoint;
        }

        if (timeout == TimeSpan.Zero)
            timeout = DefaultTimeout;

        if (_isDisposing)
            throw new ObjectDisposedException(nameof(QotdClient));
        
        Interlocked.Increment(ref _requestCt);

        if (_isDisposing)
        {
            Interlocked.Decrement(ref _requestCt);
            throw new ObjectDisposedException(nameof(QotdClient));
        }

        return mode switch
        {
            QotdClientMode.Udp => RequestUdpQuote(endPoint, encoding, timeout, token),
            _ => RequestTcpQuote(endPoint, encoding, timeout, token)
        };
    }

    #region UDP

    private async Task<string> RequestUdpQuote(IPEndPoint endPoint, Encoding encoding, TimeSpan timeout, CancellationToken token)
    {
        Exception? innerException;
        string? quote;

        try
        {
            int timeoutMs = timeout <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(timeout.TotalMilliseconds);

            using Socket socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

            IPEndPoint listen;
            if (endPoint.AddressFamily == AddressFamily.InterNetworkV6)
                listen = _any6 ??= new IPEndPoint(IPAddress.IPv6Any, 0);
            else
                listen = _any4 ??= new IPEndPoint(IPAddress.Any, 0);


            socket.Bind(listen);

            // 8 = UDP header size
            socket.SendBufferSize = 8;
            socket.ReceiveBufferSize = MaximumQuoteLength + 8;
            socket.SendTimeout = timeoutMs;
            socket.ReceiveTimeout = timeoutMs;

            using CancellationTokenRegistration cancelTokenRegistration = token.Register(Cancel, socket);

            Timer? timer = null;
            try
            {
                if (timeout > TimeSpan.Zero)
                {
                    // create a timeout cancellation
                    timer = new Timer(Cancel, socket, timeout, Timeout.InfiniteTimeSpan);
                }

                SendUdpQuoteRequest(socket, endPoint, token);

                token.ThrowIfCancellationRequested();

                (quote, innerException) = await ReceiveUdpQuote(socket, endPoint, encoding, token).ConfigureAwait(false);
            }
            finally
            {
                timer?.Dispose();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _requestCt);
        }

        return !string.IsNullOrEmpty(quote)
            ? quote
            : throw new FormatException(Properties.Resources.QotdClientReceivedInvalidQuote, innerException);

        static void Cancel(object? state) => ((Socket)state!).Dispose();
    }

    private void SendUdpQuoteRequest(Socket socket, IPEndPoint endPoint, CancellationToken token)
    {
        try
        {
            SocketAsyncEventArgs e = new SocketAsyncEventArgs { RemoteEndPoint = endPoint };
            e.SetBuffer(Array.Empty<byte>(), 0, 0);

            e.Completed += (_, args) =>
            {
                if (args.SocketError != SocketError.Success)
                {
                    LogError(Properties.Resources.QotdClientTransportError, new SocketException((int)args.SocketError));
                }
            };

            socket.SendToAsync(e);
        }
        catch (ObjectDisposedException) when (token.IsCancellationRequested)
        {
            throw new OperationCanceledException(token);
        }
    }

    private async Task<(string?, Exception?)> ReceiveUdpQuote(Socket socket, IPEndPoint endPoint, Encoding encoding, CancellationToken token)
    {
        string? quote = null;
        Exception? innerException = null;
        byte[] buffer = RentBuffer();
        try
        {
            SocketReceiveFromResult result;
            do
            {
                result = await socket
                    .ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, socket.LocalEndPoint!)
                    .ConfigureAwait(false);
            }
            while (!endPoint.Equals(result.RemoteEndPoint));

            if (result.ReceivedBytes > 0)
            {
                DecodeQuote(encoding, buffer, result.ReceivedBytes, out quote, ref innerException);
            }
        }
        catch (ObjectDisposedException) when (token.IsCancellationRequested)
        {
            throw new OperationCanceledException(token);
        }
        finally
        {
            ReturnBuffer(buffer);
        }

        return (quote, innerException);
    }

    #endregion



    #region TCP

    private async Task<string> RequestTcpQuote(IPEndPoint endPoint, Encoding encoding, TimeSpan timeout, CancellationToken token)
    {
        Exception? innerException;
        string? quote;

        try
        {
            int timeoutMs = timeout <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(timeout.TotalMilliseconds);

            using TcpClient tcp = new TcpClient();

            // 60 = TCP header size
            tcp.SendTimeout = timeoutMs;
            tcp.ReceiveTimeout = timeoutMs;
            tcp.ReceiveBufferSize = MaximumQuoteLength + 60;
            tcp.SendBufferSize = 60;

            using CancellationTokenRegistration cancelTokenRegistration = token.Register(Cancel, tcp);

            Timer? timer = null;
            if (timeout > TimeSpan.Zero)
            {
                // create a timeout cancellation
                timer = new Timer(Cancel, tcp, timeout, Timeout.InfiniteTimeSpan);
            }

            try
            {
                await tcp.ConnectAsync(endPoint.Address, endPoint.Port).ConfigureAwait(false);

                token.ThrowIfCancellationRequested();

                (quote, innerException) = await ReceiveTcpQuote(tcp, encoding, token).ConfigureAwait(false);
            }
            catch (ObjectDisposedException) when (token.IsCancellationRequested)
            {
                throw new OperationCanceledException(token);
            }
            finally
            {
                timer?.Dispose();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _requestCt);
        }

        if (string.IsNullOrEmpty(quote))
            throw new FormatException(Properties.Resources.QotdClientReceivedInvalidQuote, innerException);

        return quote;


        static void Cancel(object? state)
        {
            TcpClient tcpClient = (TcpClient)state!;
            try
            {
                tcpClient.Close();
            }
            catch { /* ignored */ }
            tcpClient.Dispose();
        }
    }

    private async Task<(string?, Exception?)> ReceiveTcpQuote(TcpClient tcp, Encoding encoding, CancellationToken token)
    {
        string? quote;
        Exception? innerException = null;

        NetworkStream stream = tcp.GetStream();

        byte[] buffer = RentBuffer();
        try
        {
            int quoteSize = await stream.ReadAsync(buffer, 0, buffer.Length, token);

            if (quoteSize == 0)
            {
                // TCP connection closed
                quote = null;
            }
            else
            {
                DecodeQuote(encoding, buffer, quoteSize, out quote, ref innerException);
            }
        }
        finally
        {
            ReturnBuffer(buffer);
        }

        return (quote, innerException);
    }

    #endregion

    private void DecodeQuote(Encoding encoding, byte[] buffer, int byteCt, out string? quote, ref Exception? innerException)
    {
        try
        {
            quote = encoding.GetString(buffer, 0, byteCt);
        }
        // DecoderFallbackException is ArgumentException
        catch (ArgumentException ex)
        {
            LogError(string.Format(Properties.Resources.QotdClientUnexpectedCharacterInEncoding, encoding.EncodingName));
            quote = null;
            innerException = ex;
        }
    }


#if USE_MS_EXTENTIONS
    private void HandleOptionsChange(QotdClientOptions options, string optionName)
    {
        lock (this)
        {
            UpdateFromOptions(options);
        }
    }

    private void UpdateFromOptions(QotdClientOptions options)
    {
        // assumed: lock on this
        IPAddress? address = options.Host;
        if (!string.IsNullOrEmpty(options.HostString)
            && (options.Host == null || options.Host.Equals(IPAddress.Loopback)))
        {
            IPAddress.TryParse(options.HostString!, out address);
        }

        address ??= IPAddress.Loopback;
        ushort port = options.Port == 0 ? QotdServer.DefaultPort : options.Port;

        Mode = options.Mode == QotdClientMode.Udp ? QotdClientMode.Udp : QotdClientMode.Tcp;
        EndPoint = new IPEndPoint(address, port);
        Encoding = options.Encoding;
        DefaultTimeout = options.DefaultTimeout;
        UpdateFromOptions((BaseQotdOptions)options);
    }
#endif

    private protected override void Dispose(bool disposing)
    {
#if USE_MS_EXTENTIONS
        Interlocked.Exchange(ref _optionsChangeListener, null)?.Dispose();
#endif
        if (!_isDisposing)
        {
            // wait for all requests to finish
            _isDisposing = true;
            SpinWait.SpinUntil(() => _requestCt == 0, 5000);
        }

        base.Dispose(disposing);
    }
}
