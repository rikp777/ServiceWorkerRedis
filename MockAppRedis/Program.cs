// See https://aka.ms/new-console-template for more information

//setup our DI

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MockAppRedis;
using MockAppRedis.Extensions;
using MockAppRedis.Redis;
using Serilog;


static void ConfigSetup(IConfigurationBuilder builder)
{
    builder.SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile(
            path: "appsettings.json",
            optional: false,
            reloadOnChange: true
        )
        .AddJsonFile(
            path: $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
            optional: true
        )
        .AddEnvironmentVariables();
}

static void ConfigSerilog(IConfigurationBuilder builder)
{
    // Define Serilog Configs
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Build())
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .CreateLogger();
}

static IHost SetupHost()
{
    // Initiate DI container
    var host = Host.CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            services.AddRedis(context.Configuration);
            services.AddTransient<Program>();
        })
        .UseSerilog()
        .Build();
    return host;
}

static IHost AppStartup()
{
    var builder = new ConfigurationBuilder();
    ConfigSetup(builder);
    ConfigSerilog(builder);

    var host = SetupHost();

    return host;
}

var app = AppStartup();
var logger = app.Services.GetService<ILogger<Program>>();

var handler = new WorkHandler();
handler.Run();


var client = new RedisClient(new List<string>() {"localhost:6379"}, "testPrefix", logger);
var calculation = new Calculation();


var subTask = Task.Run(() =>
{
    Log.Information("Starting sub..");
    client.SubscribeConcurrentlyAsync("test", calculation.PricingCalculation, CancellationToken.None);
});

await Task.Delay(100);

var messages = new List<string>()
{
    "TestMessage1",
    "TestMessage2",
    "TestMessage3"
};

for (var i = 0; i < 5000; i++)
{
    messages.Add("TestMessage1" + i);
}

var pubTask = Task.Run(() =>
{
    foreach (var message in messages)
    {
        client.TryPublish("test", message);
    }
});

await pubTask;



Console.ReadLine();




public class Calculation : IDistributedWorkItem
{
    public void PricingCalculation()
    {
        Console.WriteLine("test");
    }

    public Guid Id { get; set; }
    public bool IsDone { get; set; }
    public bool HasPriority { get; }
}