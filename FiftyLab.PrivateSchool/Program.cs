using FiftyLab.PrivateSchool;
using FiftyLab.PrivateSchool.Helpers;
using FiftyLab.PrivateSchool.Hubs;
using FiftyLab.PrivateSchool.Models;
using FiftyLab.PrivateSchool.Services;
using FiftyLab.PrivateSchool.Validations;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.Storage.SQLite;
using Jetsons.JetPack;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Ng.Services;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;

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

var webApplicationOptions = new WebApplicationOptions()
{
    ContentRootPath = AppContext.BaseDirectory,
    Args = args,
    ApplicationName = System.Diagnostics.Process.GetCurrentProcess().ProcessName
};
var builder = WebApplication.CreateBuilder(webApplicationOptions);

//---------- Logging ---------------//
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Verbose)
    .Enrich.FromLogContext()
    .Enrich.WithClientIp()
    .Enrich.WithClientAgent()
    .Enrich.WithExceptionData()
    .Enrich.WithMemoryUsage()
    .Enrich.WithProcessName()
    .Enrich.WithThreadName()
    .Enrich.WithDemystifiedStackTraces()
    .Enrich.WithRequestUserId()
    .WriteTo.Console(theme: SystemConsoleTheme.Colored, restrictedToMinimumLevel: LogEventLevel.Information)
    .WriteTo.File(AppContext.BaseDirectory + @"\Log\[VERBOSE]_Log_.log",
        rollingInterval: RollingInterval.Day,
        rollOnFileSizeLimit: true,
        retainedFileCountLimit: 30,
        shared: true,
        restrictedToMinimumLevel: LogEventLevel.Verbose)
    .WriteTo.File(AppContext.BaseDirectory + @"\Log\[ERROR]_Log_.log",
        rollingInterval: RollingInterval.Day,
        rollOnFileSizeLimit: true,
        retainedFileCountLimit: 30,
        shared: true,
        restrictedToMinimumLevel: LogEventLevel.Error)
    .WriteTo.File(AppContext.BaseDirectory + @"\Log\[INFO]_Log_.log",
        rollingInterval: RollingInterval.Day,
        rollOnFileSizeLimit: true,
        retainedFileCountLimit: 30,
        shared: true,
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

builder.Host.UseSerilog();

//---------- MongoDB & Caching ---------------//
var mongoConnectionString = builder.Configuration["MongoDB:ConnectionString"];
var mongoDbName = builder.Configuration["MongoDB:DatabaseName"];
await DatabaseHelper.InitDb(mongoDbName, mongoConnectionString);
DatabaseHelper.InitCache();

//---------- Background Jobs ---------------//
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSQLiteStorage()
//.UseSQLiteStorage($"Data Source={Path.Combine(AppContext.BaseDirectory, @"Cache\cache.db")};Version=3;")
);


builder.Services.AddHangfireServer(serverOptions =>
{
    serverOptions.WorkerCount = builder.Configuration["BackgroundJobs:WorkerCount"].ToInt(20);
    serverOptions.ServerName = "50LAB PrivateSchool Background Service";
});
//---------- Authentication ---------------//
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            }
        };
    });


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(JwtBearerDefaults.AuthenticationScheme, policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireClaim(ClaimTypes.NameIdentifier);
    });
});

//---------- JSON Serializer ---------------//
builder.Services
    .AddControllers()
    .AddNewtonsoftJson(o =>
{
    o.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
    o.SerializerSettings.Formatting = Formatting.Indented;
    o.SerializerSettings.ContractResolver = null;
});

//---------- Swagger ---------------//
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(setup =>
{
    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        BearerFormat = "JWT",
        Description = "Past JWT Bearer Token",
        Name = "JWT Authentication",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };
    setup.AddSecurityDefinition(jwtSecurityScheme.Reference.Id, jwtSecurityScheme);
    setup.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddSwaggerGenNewtonsoftSupport();

//---------- Validator ---------------//
builder.Services
    .AddFluentValidation(fv => fv
        .RegisterValidatorsFromAssemblyContaining<SignupValidator>(lifetime: ServiceLifetime.Singleton)
    );
//---------- MongoDB Change Stream ---------------//
builder.Services
    .AddHostedService<DatabaseChangeService>();

//---------- Token & Identity ---------------//
builder.Services
    .AddScoped<IIdentityService, IdentityService>()
    .AddScoped<ITokenService, TokenService>();

/*builder.Services
    //.AddScoped<SakonyConsoleMiddleware>()
    .AddScoped<MailAttachementVerificationMiddleware>();*/

builder.Services
    .AddScoped<TokenInjectionMiddleware>()
    .AddScoped<TokenFromCookiesMiddleware>()
    .AddScoped<AttachmentBasicAuthMiddleware>();

//---------- Attachments Limits ---------------//
builder.Services.Configure<FormOptions>(x =>
{
    x.ValueLengthLimit = int.MaxValue;
    x.MultipartBodyLengthLimit = int.MaxValue; // In case of multipart
});

//---------- CORS ---------------//
builder.Services.AddCors(options =>
{
    //options.AddDefaultPolicy(x => x.AllowAnyOrigin().AllowAnyMethod().AllowCredentials());
});

//---------- SignalR Websocket ---------------//

builder.Services.AddSignalR(x =>
{
    //x.EnableDetailedErrors = true;
});

//---------- NotificationService ---------------//
builder.Services
    .AddUserAgentService();
builder.Services
    .AddSingleton<LoginInfoSaver>()
    .AddSingleton<NotificationService>();

//---------- Email Notifications ---------------//
builder.Services
    .AddFluentEmail(builder.Configuration["SMTP:Email"], "Creche 50LAB")
    //.AddRazorRenderer()
    .AddSmtpSender(new SmtpClient(builder.Configuration["SMTP:Host"], builder.Configuration["SMTP:Port"].ToInt(587))
    {
        EnableSsl = true,
        Credentials = new NetworkCredential(builder.Configuration["SMTP:Email"],
            builder.Configuration["SMTP:Password"])
    });

//---------- Compression ---------------//
builder.Services.AddResponseCompression(options =>
{
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes =
        ResponseCompressionDefaults.MimeTypes.Concat(
            new[] { "image/svg+xml+application/json" });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.SmallestSize);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.SmallestSize);
//------------- Hosting SPA App ------------------------------------------------------------------------//

builder.Services.AddSpaStaticFilesWithUrlRewrite(config =>
{
    config.RootPath = Path.Combine(AppContext.BaseDirectory, "dist");
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
                {
                    listenOptions.UseHttps();
                }
                /*listenOptions.UseHttps(ctx =>
                {
                    /*cnxAdapter.SslProtocols = SslProtocols.Tls13;

                    var certPath = Path.Combine(AppContext.BaseDirectory, builder.Configuration["Https:CertPemFile"]);
                    var password = builder.Configuration["Https:Password"];
                    cnxAdapter.ServerCertificate = X509Certificate2.CreateFromPemFile(certPath);
                });*/
            }
        });
        o.Limits.MaxRequestBodySize = int.MaxValue;
    })
    .UseUrls();

//-----------------------------------------------------------//
builder.Services
    .AddSingleton<BackupHelper>()
    .AddHostedService<BackupService>();
//-----------------------------------------------------------//
builder.Host.UseWindowsService();
builder.Host.UseSystemd();
builder.Host.UseSerilog();

var app = builder.Build();
//---------------------------------------------------------------------------------------------------------//
//---------------------------------------------------------------------------------------------------------//


app.UseSerilogRequestLogging();


if (app.Configuration["UseSwagger"].ToBool())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();

}

if (app.Environment.IsProduction() && app.Configuration["UseHttps"].ToBool())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}


app.UseCors(config => config
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithOrigins(builder.Configuration["CorsHosts"].Split(";"))
    .AllowCredentials()
);

app
    .UseMiddleware<TokenInjectionMiddleware>()
    .UseMiddleware<TokenFromCookiesMiddleware>()
    .UseMiddleware<AttachmentBasicAuthMiddleware>();

app.UseAuthentication();

app.UseHttpLogging();
app.MapControllers();

app.UseRouting();

app.UseWebSockets();
app.UseAuthorization();


app.UseEndpoints(endpoint =>
{
    endpoint.MapHub<PrivateSchoolHub>("/hubs/creche");
});

app.UseHangfireDashboard("/backgroundjobs", new DashboardOptions
{
    DashboardTitle = "50LAB Private School - Background Jobs Dashboard",
    IsReadOnlyFunc = ctx => true,
    AsyncAuthorization = new[]
    {
        new DashboardAuthFilter()
    },
    AppPath = "/"
});

if (app.Environment.IsProduction())
{
    app.UseSpaStaticFiles();
    app.UseSpa(config => { });
}

app.Run();