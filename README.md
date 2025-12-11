Implementation of the [QOTD protocol](https://www.rfc-editor.org/rfc/rfc865) for .NET in C#.

* Supports TCP and UDP servers and clients.
* Supports IPv4 and IPv6.
* Integrates nicely with hosted applications.
* Customizable quotes with `IQuoteProvider`.
* Thread-safe.

### QOTD Client Application
The client is able to request one or more quotes from a server.
```cs
HostApplicationBuilder appBuilder = new HostApplicationBuilder();

appBuilder.Services
    .AddQotdClient(client =>
    {
        client.Mode = QotdClientMode.Udp; // Tcp, Udp
        client.Host = IPAddress.IPv6Loopback;
        client.Port = 17;
    });

IHost app = appBuilder.Build();

await app.StartAsync();

QotdClient client = app.Services.GetRequiredService<QotdClient>();

string quote = await client.RequestQuoteAsync();
```

### QOTD Server Application
The server hosts quotes for clients to connect to.
```cs
HostApplicationBuilder appBuilder = new HostApplicationBuilder();

appBuilder.Services
    .AddDailyQuoteProvider(options =>
    {
        options.RolloverTime = TimeSpan.Parse("22:22:00");
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
        server.Mode = QotdServerMode.Both; // Tcp, Udp, Both
    });

IHost app = appBuilder.Build();

await app.RunAsync();
```
