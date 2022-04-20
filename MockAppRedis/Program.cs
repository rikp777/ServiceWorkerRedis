// See https://aka.ms/new-console-template for more information

//setup our DI

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MockAppRedis;
using MockAppRedis.Extensions;
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
            optional:true
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


WorkHandler handler = new WorkHandler();
handler.Run();




