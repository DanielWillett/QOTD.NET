#if NET10_0_OR_GREATER
using QOTD.NET.Tests.Mocks;
using System.Globalization;

namespace QOTD.NET.Tests;

[NonParallelizable]
public class DailyQuoteProviderTests
{
    private static readonly TimeZoneInfo EST = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private static DateTime Time(string time)
    {
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.Parse(time, CultureInfo.InvariantCulture), EST);
    }

    [Test]
    public void QuoteChangesOverDayBorder()
    {
        TestTimeProvider timeProvider = new TestTimeProvider(Time("2000/1/1 11:59:59"), EST);

        DailyQuoteProvider provider = new DailyQuoteProvider(timeProvider, TimeSpan.Zero, EST, "Quote 1", "Quote 2");

        string quote = provider.GetQuote();
        Console.WriteLine(quote);

        timeProvider.SetTime(Time("2000/1/2 00:00:00"));

        string secondQuote = provider.GetQuote();
        Console.WriteLine(secondQuote);

        Assert.That(quote, Is.Not.EqualTo(secondQuote));
    }

    [Test]
    public void QuoteChangesOverDaylightSavingsForward()
    {
        TestTimeProvider timeProvider = new TestTimeProvider(Time("2025/3/9 01:59:00"), EST);

        DailyQuoteProvider provider = new DailyQuoteProvider(timeProvider, TimeSpan.Parse("2:05:00"), EST, "Quote 1", "Quote 2");

        string quote = provider.GetQuote();
        Console.WriteLine(quote);

        timeProvider.SetTime(Time("2025/3/9 03:00:00"));

        string secondQuote = provider.GetQuote();
        Console.WriteLine(secondQuote);

        Assert.That(quote, Is.Not.EqualTo(secondQuote));
    }

    [Test]
    public void QuoteChangesOverDaylightSavingsBackwards()
    {
        TestTimeProvider timeProvider = new TestTimeProvider(Time("2025/11/2 01:59:00"), EST);

        DailyQuoteProvider provider = new DailyQuoteProvider(timeProvider, TimeSpan.Parse("2:05:00"), EST, "Quote 1", "Quote 2");

        string quote = provider.GetQuote();
        Console.WriteLine(quote);

        timeProvider.SetTime(Time("2025/11/2 02:01:00").AddHours(-1d));

        string secondQuote = provider.GetQuote();
        Console.WriteLine(secondQuote);

        Assert.That(quote, Is.EqualTo(secondQuote));
    }
}
#endif