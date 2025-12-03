#if USE_MS_EXTENTIONS
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace QOTD.NET;

public abstract class BaseQotdHost : IDisposable
{
    private CancellationTokenSource? _disposeTokenSource;

#if USE_MS_EXTENTIONS
    private protected readonly ILogger Logger;
#endif

    private readonly List<byte[]> _quoteBuffer;
    private int _maximumQuoteLength = 512;

    /// <summary>
    /// The maximum number of binary buffers that can be in the pool before they start getting collected by GC.
    /// </summary>
    /// <remarks>Defaults to 16.</remarks>
    public int MaximumPooledBuffers
    {
        get;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            field = value;
        }
    } = 16;

    /// <summary>
    /// The maximum number of characters that quotes can contain.
    /// <para>
    /// Setting this to a value greater than 512 will only work if clients also support values larger than 512, which is not a requirement of the protocol.
    /// </para>
    /// </summary>
    /// <remarks>Defaults to 512.</remarks>
    public int MaximumQuoteLength
    {
        get => _maximumQuoteLength;
        set
        {
            if (value is <= 0 or > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value));

            lock (_quoteBuffer)
            {
                _maximumQuoteLength = value;
                _quoteBuffer.Clear();
            }
        }
    }

    private protected BaseQotdHost()
    {
        _quoteBuffer = new List<byte[]>(4);
        _disposeTokenSource = new CancellationTokenSource();
#if USE_MS_EXTENTIONS
        Logger = NullLogger.Instance;
#endif
    }

#if USE_MS_EXTENTIONS
    private protected BaseQotdHost(ILogger logger) : this()
    {
        Logger = logger;
    }
#endif

    private protected CancellationToken GetToken()
    {
        CancellationTokenSource? cts = _disposeTokenSource;
        try
        {
            return cts?.Token ?? CancellationToken.None;
        }
        catch (ObjectDisposedException)
        {
            return CancellationToken.None;
        }
    }

#if USE_MS_EXTENTIONS
    private protected void UpdateFromOptions(BaseQotdOptions options)
    {
        MaximumQuoteLength = options.MaximumQuoteLength;
        MaximumPooledBuffers = options.MaximumPooledBuffers;
    }
#endif

    private protected byte[] RentBuffer()
    {
        lock (_quoteBuffer)
        {
            if (_quoteBuffer.Count == 0)
                return new byte[_maximumQuoteLength];

            byte[] buffer = _quoteBuffer[_quoteBuffer.Count - 1];
            _quoteBuffer.RemoveAt(_quoteBuffer.Count - 1);
            return buffer;
        }
    }

    private protected void ReturnBuffer(byte[] buffer)
    {
        lock (_quoteBuffer)
        {
            if (_quoteBuffer.Count < MaximumPooledBuffers)
                _quoteBuffer.Add(buffer);
        }
    }

    private protected void LogError(string message)
    {
#if USE_MS_EXTENTIONS
        try
        {
            Logger.LogError(message);
        }
        catch (Exception ex)
        {
            try
            {
                Debug.WriteLine(Properties.Resources.LoggerException);
                Debug.WriteLine(ex);
            }
            catch
            {
                // ignored
            }
        }
#else
        try
        {
            Debug.WriteLine(message);
        }
        catch
        {
            // ignored
        }
#endif
    }

    private protected void LogError(string? message, Exception exception)
    {
#if USE_MS_EXTENTIONS
        try
        {
            Logger.LogError(exception, message);
        }
        catch (Exception ex)
        {
            try
            {
                if (message != null)
                    Debug.WriteLine(message);
                Debug.WriteLine(exception);
                Debug.WriteLine(Properties.Resources.LoggerException);
                Debug.WriteLine(ex);
            }
            catch
            {
                // ignored
            }
        }
#else
        try
        {
            if (message != null)
                Debug.WriteLine(message);
            Debug.WriteLine(exception);
        }
        catch
        {
            // ignored
        }
#endif
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
    }

    ~BaseQotdHost()
    {
        Dispose(false);
    }

    private protected virtual void Dispose(bool disposing)
    {
        CancellationTokenSource? cts = Interlocked.Exchange(ref _disposeTokenSource, null);
        if (cts == null)
            return;

        try
        {
            cts.Cancel();
        }
        catch { /* ignored */ }
        cts.Dispose();
    }
}
