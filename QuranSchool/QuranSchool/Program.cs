using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using AspNetCore.Identity.Mongo;

using FluentEmail.MailKitSmtp;

using FluentValidation;
using FluentValidation.AspNetCore;

using Hangfire;
using Hangfire.Mongo;

using Jetsons.JetPack;

using MailKit.Security;

using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using MongoDB.Driver;

using NetDevPack.Security.Jwt.AspNetCore;
using NetDevPack.Security.Jwt.Core;

using Newtonsoft.Json;

using Ng.Services;

using QuranApi;

using QuranSchool;
using QuranSchool.Email;
using QuranSchool.Helpers;
using QuranSchool.Hubs;
using QuranSchool.Middleware;
using QuranSchool.Models;
using QuranSchool.Models.Validations;
using QuranSchool.Services;

using Refit;

using Revoke.NET.AspNetCore;
using Revoke.NET.MongoDB;

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

try
{
    Directory.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ASP.NET"));
}
catch
{
}

var webApplicationOptions = new WebApplicationOptions
{
    ContentRootPath = AppContext.BaseDirectory,
    Args = args,
    ApplicationName = Process.GetCurrentProcess().ProcessName
};
var builder = WebApplication.CreateBuilder(webApplicationOptions);

var config = builder.Configuration;
var env = builder.Environment;

#region Autofac Multitenancy

//---------- AUTOFAC  --------------//

/*builder.Host.UseServiceProviderFactory(
    new AutofacMultitenantServiceProviderFactory(SchoolMultitenant.ConfigureMultitenantContainer));
builder.Services.AddAutofacMultitenantRequestServices();*/
//---------- HttpClient ------------//

#endregion


builder.Services
       .AddHttpClient();

#region Logging

//---------- Logging ---------------//
var logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Verbose)
            .Enrich.WithExceptionData()
            .Enrich.FromLogContext()
            .Enrich.WithClientIp()
            .Enrich.WithClientAgent()
            .Enrich.WithMemoryUsage()
            .Enrich.WithProcessName()
            .Enrich.WithThreadName()
            .Enrich.WithDemystifiedStackTraces()
            .Enrich.WithRequestUserId();
if (config["EnableConsoleLogging"].ToBool())
    logger = logger.WriteTo.Console(theme: AnsiConsoleTheme.Code,
        restrictedToMinimumLevel: LogEventLevel.Information);

if (config["EnableFileLogging"].ToBool())
    logger = logger
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
                 restrictedToMinimumLevel: LogEventLevel.Information);

Log.Logger = logger.CreateLogger();
builder.Host.UseSerilog();

#endregion


//---------- MongoDB & Caching ---------------//
var mongoConnectionString = config["MongoDB:ConnectionString"];
var mongoDbName = config["MongoDB:DatabaseName"];
await DatabaseHelper.InitDb(mongoDbName, mongoConnectionString);

#region HangFire Background Jobs

//---------- Background Jobs ---------------//
builder.Services.AddHangfire(configuration => configuration
                                             .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                                             .UseSimpleAssemblyNameTypeSerializer()
                                             .UseRecommendedSerializerSettings()
                                             .UseColouredConsoleLogProvider()
                                             .UseSerilogLogProvider()
                                             .UseMongoStorage(
                                                  mongoConnectionString.Replace(mongoDbName, $"{mongoDbName}_Hangfire"),
                                                  new MongoStorageOptions
                                                  {
                                                      ByPassMigration = true
                                                  })
//.UseSQLiteStorage($"Data Source={Path.Combine(AppContext.BaseDirectory, @"Cache\cache.db")};Version=3;")
);


builder.Services.AddHangfireServer(serverOptions =>
{
    serverOptions.WorkerCount = config["BackgroundJobs:WorkerCount"].ToInt(20);
    serverOptions.ServerName = "50LAB PrivateSchool Background Service";
});

#endregion

#region Identity

//----------- Identity ----------------------//
builder.Services.UpgradePasswordSecurity()
       .ChangeWorkFactor(4)
       .UseBcrypt<Account>();
builder
   .Services
   .AddIdentityMongoDbProvider<Account, UserRole, string>(identity =>
        {
            identity.User.RequireUniqueEmail = false;
            identity.SignIn.RequireConfirmedAccount =
                true;
            identity.Lockout.MaxFailedAccessAttempts =
                10;
            identity.Password = new PasswordOptions
            {
                RequireLowercase = false,
                RequireNonAlphanumeric = false,
                RequireUppercase = false,
                RequireDigit = false,
                RequiredLength = 6,
                RequiredUniqueChars = 0
            };
            identity.User.AllowedUserNameCharacters =
                "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_-@.";
        },
        mongo =>
        {
            mongo.ConnectionString =
                config["MongoDB:ConnectionString"];
        })
   .AddUserConfirmation<UserConfirmation>();

#endregion

#region Authentication

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = "JWT_BASIC";
    x.DefaultChallengeScheme = "JWT_BASIC";

    //x.DefaultAuthenticateScheme = GoogleDefaults.AuthenticationScheme;
    //x.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
});
builder.Services.AddAuthentication()
       .AddGoogle(googleOptions =>
        {
            googleOptions.ClientId = config["Google:ClientId"];
            googleOptions.ClientSecret = config["Google:ClientSecret"];
            googleOptions.Scope.Add("profile");
            googleOptions.Events.OnCreatingTicket = context =>
            {
                try
                {
                    var image = context.User.GetProperty("picture");
                    context.Identity?.AddClaim(new Claim("picture",
                        image.GetString()));

                    var birthday =
                        context.User.GetProperty("birthday");
                    context.Identity?.AddClaim(new Claim("birthday",
                        birthday.ToString() ??
                        DateTime.MaxValue
                                .ToString("yyyy-MM-dd")));
                }
                catch
                {
                }

                return Task.CompletedTask;
            };
        });
//builder.Services.AddAuthentication("basic")
//    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("basic", null);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
       .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = "madrasacloud.com",
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.Zero,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"] + "_appended"))
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken =
                        context.Request.Query["access_token"];

                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) &&
                        path.StartsWithSegments("/hubs"))
                        context.Token = accessToken;

                    return Task.CompletedTask;
                }
            };
        })
       .AddPolicyScheme("JWT_BASIC", "JWT_BASIC", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                if (context.Request.Path.ToString().Contains("google"))
                    return GoogleDefaults.AuthenticationScheme;

                var headers = context.Request.Headers;
                var cookies = context.Request.Cookies;

                if (cookies.ContainsKey("T"))
                    return JwtBearerDefaults.AuthenticationScheme;

                if (headers.ContainsKey("Authorization"))
                    if (headers["Authorization"]
                       .Any(x => x.StartsWith("Basic ")))
                        return "basic";

                return JwtBearerDefaults.AuthenticationScheme;
            };
        });

#endregion

#region Authorization

builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(JwtBearerDefaults.AuthenticationScheme, policy =>
            {
                //policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                policy.RequireClaim(ClaimTypes.NameIdentifier);
            });
        })
       .AddJwksManager()
       .UseJwtValidation();

builder.Services.AddMemoryCache();

#endregion


#region Swagger

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(setup =>
{
    //setup.OperationFilter<SwaggerFileOperationFilter>();

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
            Id = JwtBearerDefaults
               .AuthenticationScheme,
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

    setup.SwaggerDoc("v1",
        new OpenApiInfo { Title = "50LAB Private School", Version = "v1" });
    setup.AddSecurityDefinition("basic", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        In = ParameterLocation.Header,
        Description =
            "User / Password Authentication"
    });
    setup.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "basic"
                }
            },
            new string[] { }
        }
    });
});


builder.Services.AddSwaggerGenNewtonsoftSupport();

#endregion

#region FluentValidations

builder.Services
       .AddFluentValidationAutoValidation()
       .AddValidatorsFromAssemblyContaining<SignupValidator>(ServiceLifetime.Singleton, includeInternalTypes: true);

#endregion


//---------------- Global Error Handling --------------//
builder.Services.AddErrorHandler();

//---------- Attachments Limits ---------------//
builder.Services.Configure<FormOptions>(x =>
{
    x.ValueLengthLimit = int.MaxValue;
    x.MultipartBodyLengthLimit = int.MaxValue; // In case of multipart
});

//---------- CORS ---------------//
builder.Services.AddCors();

//---------- SignalR Websocket ---------------//

builder.Services.AddSignalR(x =>
{
    //x.EnableDetailedErrors = true;
});


#region Compression & Response Caching

builder.Services.AddResponseCaching();

builder.Services.AddResponseCompression(options =>
{
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes =
        ResponseCompressionDefaults.MimeTypes.Concat(new[]
        {
            "image/svg+xml+application/json+application/javascript+text/javascript+text/html+application/font-woff2+application/x-font-ttf+text/css"
        });
});
builder.Services
       .Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.SmallestSize)
       .Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.SmallestSize);

#endregion


//------------- Hosting SPA App ------------------------------------------------------------------------//

builder.Services.AddSpaStaticFilesWithUrlRewrite(config =>
{
    config.RootPath = Path.Combine(AppContext.BaseDirectory, "dist");
});


#region JSON Serialization

builder.Services
       .AddControllers(o => { o.RespectBrowserAcceptHeader = true; })
       .AddNewtonsoftJson(o =>
        {
            o.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            o.SerializerSettings.Formatting = Formatting.Indented;
            o.SerializerSettings.ContractResolver = null;
            o.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        });

#endregion

#region Services

//---------- MongoDB Change Stream ---------------//
builder.Services
       .AddHostedService<DatabaseChangeService>();

//---------- Token & Identity ---------------//
builder.Services
       .AddScoped<IIdentityService, IdentityService>()
       .AddScoped<AuthService>();

builder.Services
        //.AddTransient<BasicAuthenticationHelper>()
        //.AddScoped<AttachmentBasicAuthMiddleware>()
       .AddScoped<TokenInjectionMiddleware>()
       .AddScoped<TokenFromCookiesMiddleware>()
       .AddScoped<DemoRestrictionMiddleware>();
//-----------------------------  Revoke.NET ------------------------------//
builder.Services
       .AddRevokeMongoStore($"{mongoDbName}_Revoke",
            MongoClientSettings.FromConnectionString(mongoConnectionString.Replace(mongoDbName,
                $"{mongoDbName}_Revoke")))
       .AddJWTBearerTokenRevokeMiddleware();

//---------- WebsocketNotificationService ---------------//
builder.Services
       .AddUserAgentService();
builder.Services
       .AddSingleton<LoginInfoSaver>()
       .AddSingleton<WebsocketNotificationService>()
       .AddScoped<AccountGenerator>()
       .AddSingleton<GoogleService>()
       .AddSingleton<HtmlEntityService>()
       .AddSingleton<SessionEndCollector>();
//-------------- QURAN -------------------//
builder.Services
       .AddQuranService()
       .AddHostedService<QuranService>()
       .AddHostedService<GeneratorService>();


builder.Services
       .AddSingleton<BackupHelper>()
       .AddHostedService<BackupService>()
       .AddSingleton<SessionService>()
       .AddSingleton<ParentService>()
       .AddSingleton<ExcelConverter>();
//------------------------------------ Reports & Printing ------------------------//
builder.Services
       .AddSingleton<PrintService>()
       .AddSingleton<ReportService>();

#endregion

#region Mail Client & Server

//------------------------------------ Mail --------------------------------------//
builder.Services
       .AddSingleton(x => RestService.For<IMailerSendApiClient>(config["MailerSend:BasePath"]));

builder.Services
       .AddSingleton<MailerService>();

builder.Services
       .AddEmailSender()
       .AddFluentEmail(config["SMTP:Email"])
       .AddMailKitSender(new SmtpClientOptions
        {
            Server = config["SMTP:Host"],
            Port = config["SMTP:Port"].ToInt(587),
            User = config["SMTP:Email"],
            Password = config["SMTP:Password"],
            SocketOptions = SecureSocketOptions.StartTls,
            UseSsl = false
        })
       .AddRazorRenderer();
//------------------------------------ Mail Server --------------------------------------//
builder.Services
       .AddSmtpServer(smtpServerOptionsBuilder =>
        {
            return smtpServerOptionsBuilder
                  .ServerName(config["SMTP:Host"])
                  .Endpoint(x =>
                   {
                       //var cert = X509Certificate2.CreateFromPemFile(config["SMTP:Certificate"], config["SMTP:PrivateKey"]);
                       var certFile =
                           File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,
                               config["Https:Certificate"]));
                       var password = config["Https:CertificatePassword"];

                       var cert = new X509Certificate(certFile, password);

                       x.Port(config["SMTP:Port"].ToInt(587))
                        .Certificate(cert)
                        .AllowUnsecureAuthentication(false);
                   })
                  .Build();
        });

#endregion

//-----------------------------------------------------------//
builder.Host.UseWindowsService(options => { options.ServiceName = "50LabSchool"; });
builder.Host.UseSerilog();

#region Kestrel Configurations

builder.WebHost
       .UseKestrel(o =>
        {
            o.ListenAnyIP(config["PortHttp"].ToInt(5000));
            o.ListenAnyIP(config["PortHttps"].ToInt(6000), listenOptions =>
            {
                listenOptions.Protocols =
                    HttpProtocols.Http1AndHttp2;
                listenOptions
                   .UseHttps(Path.Combine(AppContext.BaseDirectory, config["Https:Certificate"]),
                        config
                            ["Https:CertificatePassword"]);
            });
            o.Limits.MaxRequestBodySize = int.MaxValue;
        })
       .UseUrls();

#endregion


//---------------------------------------------------------------------------------------------------------//
var app = builder.Build();
//---------------------------------------------------------------------------------------------------------//

app.UseErrorHandlerMiddleware();


app.UseSerilogRequestLogging();
app.UseResponseCompression();

if (app.Configuration["UseSwagger"].ToBool())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

if (app.Environment.IsProduction() && config.GetSection("EnableHsts").Get<bool>())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}


app.UseCors(options =>
    {
        #region CORS

        //---------------------- Get Local IP Addresses as CORS Origins
        var localOrigins = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                              .Append(IPAddress.Loopback)
                              .Where(x => x.AddressFamily == AddressFamily.InterNetwork)
                              .SelectMany(x => new List<string>
                               {
                                   $"http://{x}:{config["PortHttp"].ToInt(5000)}",
                                   $"https://{x}:{config["PortHttps"].ToInt(6000)}"
                               })
                              .Concat(config.GetSection("AllowedOrigins").Get<string[]>())
                              .Distinct()
                              .ToArray();

        if (app.Environment.IsProduction() && config["EnableCors"].ToBool())
            options
               .AllowAnyHeader()
               .AllowAnyMethod()
               .WithOrigins(localOrigins)
               .AllowCredentials();
        else
            options
               .AllowAnyHeader()
               .AllowAnyMethod()
               .WithOrigins(localOrigins)
               .AllowCredentials();

        #endregion
    }
);

app
   .UseRevoke()
   .UseMiddleware<TokenInjectionMiddleware>()
   .UseMiddleware<TokenFromCookiesMiddleware>();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});


app.UseAuthentication();

if (config.GetSection("Google:DemoEnabled").Get<bool>())
    app.UseMiddleware<DemoRestrictionMiddleware>();

app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.Lax
});

app.UseHttpLogging();
app.UseResponseCaching();
app.UseResponseCompression();


app.MapControllers();

app.UseRouting();
//.Use(async (ctx, next) =>
//{
//    var query = ctx.Request.QueryString.Value.Contains("token") ? "" : ctx.Request?.QueryString.Value;

//    Console.WriteLine($"{ctx.Request?.Path}, {query}");
//    await next(ctx);
//});

app.UseWebSockets();
app.UseAuthorization();

app.MapHub<PrivateSchoolHub>("/hubs/school");

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

app.UseSpaStaticFiles();
app.UseSpa(config =>
{
    /*if (app.Environment.IsDevelopment())
        config.UseProxyToSpaDevelopmentServer("http://127.0.0.1:8080");*/
});


app.Run();