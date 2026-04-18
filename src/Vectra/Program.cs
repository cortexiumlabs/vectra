using Microsoft.AspNetCore.DataProtection;
using System.CommandLine;
using System.Text.Json.Serialization;
using Vectra.Application;
using Vectra.Extensions;
using Vectra.Infrastructure;
using Vectra.Middleware;
using Vectra.Services;

var urlsOption = new Option<string?>("--urls", "-u")
{
    Description = "Semicolon-separated list of URLs the server will listen on (e.g. http://0.0.0.0:5000;https://0.0.0.0:5001).",
};

var versionOption = new Option<bool>("--version", "-v")
{
    Description = "Show the current Vectra version."
};

var rootCommand = new RootCommand("Vectra – Intent-Aware Governance Gateway");

// Replace the built-in VersionOption with our own so the output uses VectraVersion.
var builtIn = rootCommand.Options.OfType<VersionOption>().FirstOrDefault();
if (builtIn is not null)
    rootCommand.Options.Remove(builtIn);

rootCommand.Options.Add(urlsOption);
rootCommand.Options.Add(versionOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    if (parseResult.GetValue(versionOption))
    {
        Console.WriteLine($"Vectra {VectraVersion.GetApplicationVersion()}");
        return;
    }

    PrintSplash();

    var urls = parseResult.GetValue(urlsOption);

    if (!string.IsNullOrWhiteSpace(urls))
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", urls);

    var builder = WebApplication.CreateBuilder(args);

    try
    {
        ConfigureServices(builder, args);

        var app = builder.Build();
        await ConfigureWebApplication(app);

        await app.RunAsync(cancellationToken);
    }
    catch (Exception ex)
    {
        await HandleStartupExceptionAsync(builder, ex);
    }
});

return await rootCommand.Parse(args).InvokeAsync();

static void PrintSplash()
{
    var version = VectraVersion.GetApplicationVersion();
    var asm = System.Reflection.Assembly.GetExecutingAssembly();

    using var stream = asm.GetManifestResourceStream("Vectra.Resources.splash.txt")!;
    using var reader = new StreamReader(stream);

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine(reader.ReadToEnd());
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.WriteLine($"    v{version} | © Cortexium Labs. All rights reserved.");
    Console.WriteLine("    https://cortexiumlabs.com/vectra");
    Console.WriteLine("    ------------------------------------------------");
    Console.WriteLine("    Intent-Aware Governance for Secure, Observable,");
    Console.WriteLine("    and Controlled Interactions Across Autonomous");
    Console.WriteLine("    Agents and Systems.");
    Console.WriteLine();
    Console.ResetColor();
}

static void ConfigureServices(WebApplicationBuilder builder, string[] args)
{
    var services = builder.Services;
    var config = builder.Configuration;
    var env = builder.Environment;

    services.AddDataProtection()
            .SetApplicationName("VectraGateway");

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNameCaseInsensitive = true;
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    services
        .AddVectraConfiguration(config)
        .AddSystemClock()
        .AddJsonSerialization()
        .AddCache()
        .AddInfrastructure()
        .AddVectraPersistence()
        .AddVectraApiDocumentation()
        .AddVectraProxyForwarder()
        .AddVectraHealthChecker()
        .AddVectraVersion()
        .AddVectraApplication()
        .AddVectraLogging();

    builder.ConfigureVectraHttpServer();
}

static async Task ConfigureWebApplication(WebApplication app)
{
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseVectraCustomException();
    }

    app.UseVectraHttps();
    app.UseVectraCustomHeaders();

    app.UseRouting();
    app.UseMiddleware<AgentAuthMiddleware>();

    app.MapWhen(ctx => ctx.Request.Path.StartsWithSegments("/proxy"), proxyBranch =>
    {
        proxyBranch.UseMiddleware<ProxyMiddleware>();
    });

    app.MapEndpoints();

    app.Map("/{**catch-all}", async ctx =>
    {
        ctx.Response.StatusCode = 404;
        await ctx.Response.WriteAsync($"No endpoint found for {ctx.Request.Path}");
    });

    app.UseVectraApiDocumentation();
    app.UseVectraHealthCheck();

    await app.EnsureApplicationDatabaseCreated();
}

static async Task HandleStartupExceptionAsync(WebApplicationBuilder builder, Exception ex)
{
    try
    {
        using var scope = builder.Services.BuildServiceProvider().CreateScope();
        var logger = scope.ServiceProvider.GetService<ILogger<Program>>();
        logger?.LogError(ex, "Unhandled exception during startup");
    }
    catch
    {
        await Console.Error.WriteLineAsync($"Startup error: {ex.Message}");
    }

    // Prevent console from closing immediately
    await Task.Delay(500);
}