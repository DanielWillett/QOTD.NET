using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace QOTD.NET;

/// <summary>
/// Defines the behavior of a service that provides quotes to a <see cref="QotdServer"/>.
/// </summary>
public interface IQuoteProvider
{
    /// <summary>
    /// Retrieves a quote to send to a given user.
    /// </summary>
    /// <param name="clientAddress">The IP address of the client asking for the quote.</param>
    /// <param name="token">A cancellation token used to cancel the request.</param>
    /// <returns>A quote in the form of ASCII text.</returns>
    /// <exception cref="OperationCanceledException"/>
    ValueTask<string> GetQuote(IPAddress clientAddress, CancellationToken token = default);
}