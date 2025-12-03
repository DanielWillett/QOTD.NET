#if USE_MS_EXTENTIONS
using System;
using System.Net;

namespace QOTD.NET;

/// <summary>
/// Options for configuring a <see cref="QotdServer"/>.
/// </summary>
public class QotdClientOptions : BaseQotdOptions
{
    /// <summary>
    /// The port to use for outgoing requests.
    /// </summary>
    /// <remarks>Defaults to <see cref="QotdServer.DefaultPort"/> (17).</remarks>
    public ushort Port { get; set; } = QotdServer.DefaultPort;

    /// <summary>
    /// Which protocol to use for outgoing requests.
    /// </summary>
    /// <remarks>Defaults to <see cref="QotdServerMode.Tcp"/>.</remarks>
    public QotdClientMode Mode { get; set; } = QotdClientMode.Tcp;

    /// <summary>
    /// Host IP address to connect to, configurable from config files.
    /// </summary>
    /// <remarks>Defaults to <c>127.0.0.1</c> (loopback).</remarks>
    public string? HostString { get; set; }

    /// <summary>
    /// Host IP address to connect to.
    /// </summary>
    /// <remarks>Defaults to <c>127.0.0.1</c> (loopback).</remarks>
    public IPAddress? Host { get; set; } = IPAddress.Loopback;

    /// <summary>
    /// Default timeout for quote requests. Negative values indicate an infinite timeout.
    /// </summary>
    /// <remarks>Defaults to 5 seconds.</remarks>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(5d);


    internal void UpdateFrom(QotdClientOptions options)
    {
        UpdateFrom((BaseQotdOptions)options);
        Port = options.Port;
        Mode = options.Mode;
        HostString = options.HostString;
        Host = options.Host;
        DefaultTimeout = options.DefaultTimeout;
    }
}
#endif