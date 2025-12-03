#if USE_MS_EXTENTIONS
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace QOTD.NET;

/// <summary>
/// A quote provider which chooses a new quote every day at a configurable time.
/// </summary>
public class DailyQuoteProvider : IQuoteProvider, IDisposable
{
    private readonly TimeSpan _rolloverTime;
    private readonly TimeZoneInfo _timeZone;
    private readonly string[]? _quotePool;
    private DateTime _lastQuoteDate;
    private string? _previousQuote;
    private string? _currentQuote;

#if USE_MS_EXTENTIONS
    private readonly ILogger<DailyQuoteProvider>? _logger;
    private IDisposable? _optionsChangeMonitor;
#endif

    /// <summary>
    /// The quote for today.
    /// </summary>
    /// <remarks>May be null or out of date if it hasn't been generated yet.</remarks>
    public string? CurrentQuote => _currentQuote;

#if NET9_0_OR_GREATER
    private readonly TimeProvider? _timeProvider;

    /// <inheritdoc cref="DailyQuoteProvider(TimeSpan,TimeZoneInfo?)"/>
    /// <param name="timeProvider"><see cref="TimeProvider"/> implementation to override system time provider.</param>
    protected DailyQuoteProvider(TimeProvider? timeProvider, TimeSpan rolloverTime, TimeZoneInfo? timeZone)
        : this(rolloverTime, timeZone ?? timeProvider?.LocalTimeZone)
    {
        _timeProvider = timeProvider;
    }

    /// <inheritdoc cref="DailyQuoteProvider(TimeSpan,TimeZoneInfo?,IList{string})"/>
    /// <param name="timeProvider"><see cref="TimeProvider"/> implementation to override system time provider.</param>
    public DailyQuoteProvider(TimeProvider? timeProvider, TimeSpan rolloverTime, TimeZoneInfo? timeZone, params IList<string> quotePool)
        : this(rolloverTime, timeZone ?? timeProvider?.LocalTimeZone, quotePool)
    {
        _timeProvider = timeProvider;
    }

    /// <inheritdoc cref="DailyQuoteProvider(IList{string})"/>
    /// <param name="timeProvider"><see cref="TimeProvider"/> implementation to override system time provider.</param>
    public DailyQuoteProvider(TimeProvider? timeProvider, params IList<string> quotePool)
        : this((timeProvider ?? TimeProvider.System).GetLocalNow().TimeOfDay, timeProvider?.LocalTimeZone, quotePool)
    {
        _timeProvider = timeProvider;
    }

#endif

#if USE_MS_EXTENTIONS

#if NET9_0_OR_GREATER

    /// <summary>
    /// Creates a new <see cref="DailyQuoteProvider"/> with a custom <see cref="TimeProvider"/> using options and a logger.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="FormatException">Invalid configuration.</exception>
    [ActivatorUtilitiesConstructor]
    public DailyQuoteProvider(
        TimeProvider timeProvider,
        IOptionsMonitor<DailyQuoteProviderOptions> optionsMonitor,
        ILogger<DailyQuoteProvider> logger)
        : this(optionsMonitor, logger)
    {
        _timeProvider = timeProvider;
    }
#endif

    /// <summary>
    /// Creates a new <see cref="DailyQuoteProvider"/> using options and a logger.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="FormatException">Invalid configuration.</exception>
#if !NET9_0_OR_GREATER
    [ActivatorUtilitiesConstructor]
#endif
    public DailyQuoteProvider(IOptionsMonitor<DailyQuoteProviderOptions> optionsMonitor, ILogger<DailyQuoteProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (optionsMonitor == null)
            throw new ArgumentNullException(nameof(optionsMonitor));

        DailyQuoteProviderOptions options = optionsMonitor.CurrentValue;

        _rolloverTime = options.RolloverTime;
        if (_rolloverTime < TimeSpan.Zero || _rolloverTime.Days > 1)
            _rolloverTime = TimeSpan.Zero;

        string? tz = options.TimeZone;

        try
        {
            _timeZone = !string.IsNullOrEmpty(tz)
                ? TimeZoneInfo.FindSystemTimeZoneById(tz)
                : TimeZoneInfo.Local;
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new FormatException(ex.Message, ex);
        }

        if (GetType() != typeof(DailyQuoteProvider))
            return;

        _quotePool = options.Quotes.ToArray();

        if (_quotePool.Length == 0)
            throw new FormatException(Properties.Resources.DailyQuoteProviderPoolMustHaveElements);

        if (Array.IndexOf(_quotePool, null) >= 0)
            throw new FormatException(Properties.Resources.DailyQuoteProviderPoolContainsNull);
    }

#endif

    /// <summary>
    /// For classes that want to override the behavior of <see cref="DailyQuoteProvider"/>.
    /// Instead of pulling from a pool, the method 
    /// </summary>
    /// <param name="rolloverTime">The time of day at which a new quote will be chosen.</param>
    /// <param name="dayDuration">The length of a 'day'.</param>
    /// <param name="timeZone">The time zone to use for time calculations. Defaults to local if <see langword="null"/>.</param>
    protected DailyQuoteProvider(TimeSpan rolloverTime, TimeZoneInfo? timeZone)
    {
        if (rolloverTime < TimeSpan.Zero || rolloverTime.Days > 1)
            rolloverTime = TimeSpan.Zero;

        _quotePool = null;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
        _rolloverTime = rolloverTime;
    }

    /// <summary>
    /// Creates a new <see cref="DailyQuoteProvider"/> that provides random quotes every day from a pool, refreshing at the current time.
    /// </summary>
    /// <param name="quotePool">Pool of at least one quote to pull quotes from. A single quote will be chosen every day.</param>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Invalid quote list, must have at least one element and zero null elements.</exception>
    public DailyQuoteProvider(params IList<string> quotePool)
        : this(DateTime.Now.TimeOfDay, TimeZoneInfo.Local, quotePool) { }

    /// <summary>
    /// Creates a new <see cref="DailyQuoteProvider"/> that provides random quotes every day from a pool, refreshing at <paramref name="rolloverTime"/>.
    /// </summary>
    /// <param name="rolloverTime">The UTC time of day at which a new quote will be chosen.</param>
    /// <param name="quotePool">Pool of at least one quote to pull quotes from. A single quote will be chosen every day.</param>
    /// <param name="timeZone">The time zone to use for time calculations. Defaults to local if <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentException">Invalid quote list, must have at least one element and zero null elements.</exception>
    public DailyQuoteProvider(TimeSpan rolloverTime, TimeZoneInfo? timeZone, params IList<string> quotePool)
    {
        if (quotePool == null)
            throw new ArgumentNullException(nameof(quotePool));

        _quotePool = quotePool.ToArray();

        if (_quotePool.Length == 0)
            throw new ArgumentException(Properties.Resources.DailyQuoteProviderPoolMustHaveElements, nameof(quotePool));

        if (Array.IndexOf(_quotePool, null) >= 0)
            throw new ArgumentException(Properties.Resources.DailyQuoteProviderPoolContainsNull, nameof(quotePool));

        if (rolloverTime < TimeSpan.Zero || rolloverTime.Days > 1)
            rolloverTime = TimeSpan.Zero;

        _timeZone = timeZone ?? TimeZoneInfo.Local;
        _rolloverTime = rolloverTime;
    }

    /// <summary>
    /// Get the current time in the selected <see cref="TimeZoneInfo"/>, taking into account the <see cref="T:System.TimeProvider"/> if available.
    /// </summary>
    protected DateTime GetRelevantTime()
    {
#if NET9_0_OR_GREATER

        if (_timeProvider != null)
        {
            if (ReferenceEquals(_timeProvider.LocalTimeZone, _timeZone))
            {
                return _timeProvider.GetLocalNow().DateTime;
            }

            DateTimeOffset utc = _timeProvider.GetUtcNow();
            return TimeZoneInfo.ConvertTime(utc, _timeZone).DateTime;
        }
#endif

        if (ReferenceEquals(TimeZoneInfo.Local, _timeZone))
            return DateTime.Now;

        return TimeZoneInfo.ConvertTime(DateTime.UtcNow, _timeZone);
    }

    private bool TryGenerateNewQuote(out DateTime date)
    {
        // should handle daylight savings ok
        DateTime now = GetRelevantTime();
        DateTime dateLcl = now.Date;
        date = dateLcl;
        if (_lastQuoteDate >= dateLcl)
            return false;

        if (_currentQuote != null && now.TimeOfDay < _rolloverTime)
            return false;

        return UpdateQuote(dateLcl, now);
    }

    /// <summary>
    /// When implemented by a child class, gets the next day's quote. Invoked once every 'day'.
    /// </summary>
    /// <remarks>
    /// It is rare but possible that this method will be invoked more than once per day due to multi-threading but only one value will be used.
    /// The value of <see cref="CurrentQuote"/> can tell you if this is happening if it affects your application.
    /// </remarks>
    /// <param name="date">The date being generated for, with the time set to the rollover time.</param>
    /// <returns>A new quote.</returns>
    /// <exception cref="InvalidOperationException">Not overridden when no quotes were passed to the constructor.</exception>
    protected virtual string GetNewDayQuote(DateTime date)
    {
        if (_quotePool == null || _quotePool.Length == 0)
            throw new InvalidOperationException();

#if NET6_0_OR_GREATER
        Random random = Random.Shared;
#else
        Random random = new Random();
#endif

        int index = random.Next(0, _quotePool.Length);
        string q = _quotePool[index];
        if (!ReferenceEquals(q, _currentQuote) || _quotePool.Length <= 1)
            return q;

        if (index == 0)
            ++index;
        else
            --index;
        return _quotePool[index];

    }

    private bool UpdateQuote(DateTime day, DateTime lclTime)
    {
        string? previousQuote = _currentQuote;
        if (_quotePool != null && _quotePool.Length == 1)
        {
            _currentQuote = _quotePool[0];
            return true;
        }

        string newQuote = GetNewDayQuote(day + _rolloverTime);
        string? value = Interlocked.CompareExchange(ref _currentQuote, newQuote, previousQuote);
        if (ReferenceEquals(value, previousQuote))
        {
            _previousQuote = value;
            _lastQuoteDate = lclTime.TimeOfDay > _rolloverTime ? day : day.AddDays(-1);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the quote for today.
    /// </summary>
    public string GetQuote()
    {
        bool hasNewQuote = TryGenerateNewQuote(out DateTime date);
        string quote = _currentQuote!;

#if USE_MS_EXTENTIONS
        if (hasNewQuote && _logger != null && _logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace(string.Format(Properties.Resources.DailyQuoteProviderLogNewQuote, date.ToString("d"), quote));
        }
#endif
        return quote;
    }

    /// <inheritdoc />
    public ValueTask<string> GetQuote(IPAddress clientAddress, CancellationToken token = default)
    {
        return new ValueTask<string>(GetQuote());
    }

    /// <inheritdoc />
    public void Dispose()
    {
#if USE_MS_EXTENTIONS
        Interlocked.Exchange(ref _optionsChangeMonitor, null)?.Dispose();
#endif
    }
}
