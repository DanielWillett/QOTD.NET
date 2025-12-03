#if USE_MS_EXTENTIONS
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QOTD.NET;

/// <summary>
/// Options for configuring a <see cref="DailyQuoteProvider"/>.
/// </summary>
public class DailyQuoteProviderOptions
{
    /// <summary>
    /// List of quotes to cycle through.
    /// </summary>
    /// <remarks>Must contain at least one.</remarks>
    [Required]
    [MinLength(1)]
    public List<string> Quotes { get; set; } = new List<string>();

    /// <summary>
    /// The time of day at which a new quote will be generated.
    /// </summary>
    /// <remarks>Defaults to midnight.</remarks>
    public TimeSpan RolloverTime { get; set; }

    /// <summary>
    /// System ID of the time zone to use for quotes, defaulting to the local time.
    /// </summary>
    public string? TimeZone { get; set; }

    internal void UpdateFrom(DailyQuoteProviderOptions options)
    {
        Quotes = options.Quotes;
    }
}
#endif