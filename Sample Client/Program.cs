using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QOTD.NET;

HostApplicationBuilder appBuilder = new HostApplicationBuilder();

// configure program
appBuilder.Logging
    .AddSystemdConsole()
    .SetMinimumLevel(LogLevel.Trace);

appBuilder.Services
    .AddQotdClient(server =>
    {
        server.Mode = QotdClientMode.Udp;
        server.Host = IPAddress.IPv6Loopback;
    });

IHost app = appBuilder.Build();

// start services
await app.StartAsync();

// fetch client from container
QotdClient client = app.Services.GetRequiredService<QotdClient>();

while (true)
{
    Console.Write("[Enter] for new quote, or \"quit\" to exit.");

    if (Console.ReadLine() == "quit")
        break;

    // request a quote
    string quote = await client.RequestQuoteAsync();

    Console.WriteLine();
    Console.WriteLine("\"\"\"");
    Console.WriteLine(quote);
    Console.WriteLine("\"\"\"");
}

await app.StopAsync();