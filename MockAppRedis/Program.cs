// See https://aka.ms/new-console-template for more information

//setup our DI

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MockAppRedis;
using MockAppRedis.Extensions;
using MockAppRedis.Redis;
using Serilog;
using StackExchange.Redis;
using ILogger = Serilog.ILogger;


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

var logger = Log.ForContext<Program>();
var client = new RedisClient(new List<string>() {"localhost:6379"}, "testPrefix", logger);


var calculation = new Calculation(Guid.NewGuid(),20,20);
var subTask = Task.Run(() =>
{
    client.SubscribeConcurrentlyAsync<Calculation>("test", calculation.ExpensiveWork, CancellationToken.None);
});
//
// await Task.Delay(100);
//
// var messages = new List<Calculation>()
// {
//     new(Guid.NewGuid(),10,40),
//     new(Guid.NewGuid(),10,40),
//     new(Guid.NewGuid(),10,40)
// };
//
// for (var i = 0; i < 1000; i++)
// {
//     messages.Add(new Calculation(Guid.NewGuid(), i, i));
// }
//
//
// var pubTask = Task.Run(() =>
// {
//     foreach (var message in messages)
//     {
//         client.TryPublish("test", message);
//     }
// });
//
//
// await pubTask;
// await Task.Delay(15000).ContinueWith((x) => {
//     foreach (var message in messages)
//     {
//         client.TryPublish("test", message);
//     }
// });



var redisClientConnection = client.getRedisConnection();

var db = redisClientConnection.GetDatabase();
var topic = "testPrefix:test";
Task.Run(async () =>
{
    var random = new Random();
    while (true)
    {
        var calculation = new Calculation(Guid.NewGuid(), random.Next(1000),random.Next(1000));
        await db.StreamAddAsync(topic,
            new NameValueEntry[] {new("data", JsonSerializer.Serialize(calculation))}
        );
        logger.Information($"{topic} - Produced message on stream");
        await Task.Delay(10);
    }
});


Console.ReadLine();




public class Calculation : IDistributedWorkItem
{
    private Random _random = new Random();
    public Guid Id { get; set; }
    public bool IsDone { get; set; }
    public bool HasPriority { get; }
    public void ExpensiveWork()
    {
        PricingCalculation();
    }

    public int s1 { get; set; }
    public int s2 { get; set; }

    public Calculation(Guid id, int s1, int s2)
    {
        Id = id;
        this.s1 = s1;
        this.s2 = s2;
    }

    public async Task PricingCalculation()
    {
        var list = new List<int>();
        for (var i = 0; i < 1000000; i++)
        {
            if (list.Contains(i))
            {
                list.Add(i);
            }

            list.Contains(i);
            list.Contains(i);
            list.Contains(i);
            list.Contains(i);
        }
        await Task.Delay((_random.Next(10000) > 5000) ? 100 : 10000);
        Console.WriteLine($"{IsDone} {Id} {s1} {s2}");
    }
}