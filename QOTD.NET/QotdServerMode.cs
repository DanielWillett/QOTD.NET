namespace QOTD.NET;

/// <summary>
/// Defines the mode of operation for a <see cref="QotdServer"/>.
/// </summary>
public enum QotdServerMode
{
    /// <summary>
    /// A TCP server listens for connections on the assigned port (17).
    /// After the connection is established the quote is sent out and the connection is closed.
    /// </summary>
    Tcp,

    /// <summary>
    /// A UDP server listens for messages on the assigned port (17).
    /// Once a message is received, the server replies with the quote.
    /// </summary>
    Udp,

    /// <summary>
    /// Quotes can be requested using either TCP or UDP.
    /// </summary>
    Both
}