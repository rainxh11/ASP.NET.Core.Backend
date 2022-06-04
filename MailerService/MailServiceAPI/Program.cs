using System.Diagnostics;
using System.Text.Json.Serialization;
using FluentValidation.AspNetCore;
using Jetsons.JetPack;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using ReniwnMailServiceApi;
using ReniwnMailServiceApi.Models.Validations;
using ReniwnMailServiceApi.Services;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

try
{
    if (args[0].Contains("install"))
    {
        WindowsServiceInstaller.InstallService();
        Process.GetCurrentProcess().Kill();
    }
}
catch
{
}

var builder = WebApplication.CreateBuilder(args);

//---------- Logging ---------------//
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Verbose)
    .Enrich.FromLogContext()
    .WriteTo.Console(theme: SystemConsoleTheme.Colored, restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

builder.Host.UseSerilog();
//-----------------------------//
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.WriteIndented = true;
    });

//----------------------------- Refit Http Client ----------------------//
builder.Services
    .AddHttpClient()
    .AddSingleton<RestApiClientFactory>();
//----------------------------- Swagger ---------------------------------//
builder.Services
    .AddEndpointsApiExplorer()
    .AddSwaggerGen();
//---------- Validator ---------------//
builder.Services
    .AddFluentValidation(fv => fv
        .RegisterValidatorsFromAssemblyContaining<UserValidator>(lifetime: ServiceLifetime.Singleton)
    );
//-----------------------------//
builder.Host.UseWindowsService();
builder.Host.UseSystemd();
builder.Host.UseSerilog();
//---------- CORS ---------------//
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(x =>
        x.AllowAnyHeader()
            .AllowAnyMethod()
            .WithOrigins(builder.Configuration["CorsHosts"].Split(";")));
});

//---------------------------------------------------------------------------------------------------------//
builder.WebHost
    .UseKestrel(o =>
    {
        o.ListenAnyIP(builder.Configuration["Port"].ToInt(), listenOptions =>
        {
            if (builder.Environment.IsProduction())
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                if (builder.Configuration["UseHttps"].ToBool() && builder.Environment.IsProduction())
                    listenOptions.UseHttps();
            }
        });
        o.Limits.MaxRequestBodySize = int.MaxValue;
    })
    .UseUrls();
var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsProduction() && app.Configuration["UseHttps"].ToBool())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseCors();
app.UseAuthorization();

app.MapControllers();

app.Run();