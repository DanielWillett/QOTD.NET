using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QOTD.NET.Tests.Mocks;

namespace QOTD.NET.Tests;

[NonParallelizable]
public class TcpServerTests
{
    private IHost _host;

    [SetUp]
    public async Task Setup()
    {
        HostApplicationBuilder appBuilder = new HostApplicationBuilder();

        appBuilder.Logging.AddConsole().SetMinimumLevel(LogLevel.Trace);
        
        appBuilder.Services.AddTransient<IQuoteProvider, TestQuoteProvider>()
                           .AddQotdServer();

        appBuilder.Services.AddQotdClient();

        appBuilder.Services.Configure<QotdServerOptions>(opt =>
        {
            opt.Mode = QotdServerMode.Tcp;
        });

        appBuilder.Services.Configure<QotdClientOptions>(opt =>
        {
            opt.Mode = QotdClientMode.Tcp;
            opt.DefaultTimeout = Timeout.InfiniteTimeSpan;
        });

        _host = appBuilder.Build();

        await _host.StartAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    [Test]
    public async Task BasicQuoteOverTcp()
    {
        using IServiceScope scope = _host.Services.CreateScope();
        QotdClient client = scope.ServiceProvider.GetRequiredService<QotdClient>();

        string quote = await client.RequestQuoteAsync();

        Assert.That(quote, Is.EqualTo(TestQuoteProvider.Value));
    }
}
