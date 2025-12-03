using System.Net;

namespace QOTD.NET.Tests.Mocks;

internal class TestQuoteProvider : IQuoteProvider
{
    public const string Value = "Test Quote";

    /// <inheritdoc />
    public ValueTask<string> GetQuote(IPAddress clientAddress, CancellationToken token = default)
    {
        return new ValueTask<string>(Value);
    }
}
