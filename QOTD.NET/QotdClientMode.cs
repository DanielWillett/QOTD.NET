namespace QOTD.NET;

/// <summary>
/// Defines the mode of operation for a <see cref="QotdClient"/>.
/// </summary>
public enum QotdClientMode
{
    /// <summary>
    /// A TCP client connects to a server which listens for connections on the assigned TCP port (17).
    /// </summary>
    Tcp,

    /// <summary>
    /// A UDP client connects to a server which listens for datagrams on the assigned UDP port (17).
    /// </summary>
    Udp
}