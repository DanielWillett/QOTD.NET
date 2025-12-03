#if USE_MS_EXTENTIONS
namespace QOTD.NET;

/// <summary>
/// Options for configuring a <see cref="QotdServer"/>.
/// </summary>
public class QotdServerOptions : BaseQotdOptions
{
    /// <summary>
    /// If this property has a value, takes priority over <see cref="Port"/> for the UDP listener if it's enabled.
    /// </summary>
    /// <remarks>Defaults to <see langword="null"/>.</remarks>
    public ushort? UdpPort { get; set; }

    /// <summary>
    /// If this property has a value, takes priority over <see cref="Port"/> for the TCP listener if it's enabled.
    /// </summary>
    /// <remarks>Defaults to <see langword="null"/>.</remarks>
    public ushort? TcpPort { get; set; }

    /// <summary>
    /// The port to use for the TCP and UDP listeners if they're enabled. Can be overridden with <see cref="UdpPort"/> and <see cref="TcpPort"/>.
    /// </summary>
    /// <remarks>Defaults to <see cref="QotdServer.DefaultPort"/> (17).</remarks>
    public ushort Port { get; set; } = QotdServer.DefaultPort;

    /// <summary>
    /// Which protocol(s) to use for listening for incoming requests.
    /// </summary>
    /// <remarks>Defaults to <see cref="QotdServerMode.Both"/>.</remarks>
    public QotdServerMode Mode { get; set; } = QotdServerMode.Both;

    /// <summary>
    /// Whether or not to support IPv6 and IPv4, isntead of just IPv4.
    /// </summary>
    /// <remarks>Defaults to <see langword="true"/>.</remarks>
    public bool DualMode { get; set; } = true;

    internal void UpdateFrom(QotdServerOptions options)
    {
        UpdateFrom((BaseQotdOptions)options);
        UdpPort = options.UdpPort;
        TcpPort = options.TcpPort;
        Port = options.Port;
        Mode = options.Mode;
        DualMode = options.DualMode;
    }
}
#endif