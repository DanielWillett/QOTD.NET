using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QOTD.NET;

HostApplicationBuilder appBuilder = new HostApplicationBuilder();

// configure program
appBuilder.Logging
    .AddSystemdConsole()
    .SetMinimumLevel(LogLevel.Trace);

appBuilder.Services
    .AddDailyQuoteProvider(options =>
    {
        options.RolloverTime = TimeSpan.Parse("03:00:00");
        options.TimeZone = "America/New_York";
        options.Quotes =
        [
            "Some random quote",
            "Another random quote",
            "A third qoute"
        ];
    })
    .AddQotdServer(server =>
    {
        server.Mode = QotdServerMode.Both;
        server.DualMode = true;
    });

IHost app = appBuilder.Build();

// run server
await app.RunAsync();