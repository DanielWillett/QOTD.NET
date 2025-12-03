using System;
using System.Text;

#if USE_MS_EXTENTIONS
namespace QOTD.NET;

/// <summary>
/// Common options for configuring a <see cref="QotdServer"/> or <see cref="QotdClient"/>.
/// </summary>
public abstract class BaseQotdOptions
{
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
    /// For servers, setting this to a value greater than 512 will only work if clients also support values larger than 512, which is not a requirement of the protocol.
    /// </para>
    /// </summary>
    /// <remarks>Defaults to 512.</remarks>
    public int MaximumQuoteLength
    {
        get;
        set
        {
            if (value is <= 0 or > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value));

            field = value;
        }
    } = 512;

    /// <summary>
    /// The encoding to use for quotes.
    /// <para>
    /// Most servers will send quotes in ASCII format, as specified by the protocol.
    /// </para>
    /// <para>
    /// Using an encoding other than ASCII is not supported by the protocol and will have to be explicitly supported by clients.
    /// </para>
    /// </summary>
    /// <remarks>Defaults to ASCII with an exception fallback.</remarks>
    public Encoding Encoding
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = QotdServer.DefaultEncoding;

    protected void UpdateFrom(BaseQotdOptions options)
    {
        Encoding = options.Encoding;
        MaximumQuoteLength = options.MaximumQuoteLength;
        MaximumPooledBuffers = options.MaximumPooledBuffers;
    }
}
#endif