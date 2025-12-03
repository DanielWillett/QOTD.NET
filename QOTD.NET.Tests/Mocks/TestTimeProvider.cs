#if NET9_0_OR_GREATER
namespace QOTD.NET.Tests.Mocks;

internal class TestTimeProvider : TimeProvider
{
    private DateTime _utcTime;

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone { get; }

    /// <inheritdoc />
    public TestTimeProvider(DateTime utcTime, TimeZoneInfo localTimeZone)
    {
        _utcTime = utcTime;
        LocalTimeZone = localTimeZone;
    }

    public void SetTime(DateTime utcNow)
    {
        _utcTime = utcNow;
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _utcTime;
}

#endif