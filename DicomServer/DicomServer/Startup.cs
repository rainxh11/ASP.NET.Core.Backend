using DicomServer.Helper;
using DicomServer.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Refit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using DicomServer.Hubs;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Newtonsoft.Json.Serialization;
using Hangfire;
using Hangfire.MemoryStorage;

namespace DicomServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var config = ConfigHelper.GetConfig();
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins", builder =>
                {
                    builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
                });
            });
            services.AddControllers();
            services
                .AddRefitClient<IOrthancApi>(new RefitSettings() 
                {
                    ContentSerializer= new NewtonsoftJsonContentSerializer(),
                    AuthorizationHeaderValueGetter = () => Task.FromResult(config.OrthancApi.GetAuthentification())
                })
                .ConfigureHttpClient(c => {
                    c.BaseAddress = new Uri(config.OrthancApi.Host);
                });

            services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.SmallestSize)
                .Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.SmallestSize);
            services.AddResponseCompression(options =>
            {
                options.Providers.Add<GzipCompressionProvider>();
                options.Providers.Add<BrotliCompressionProvider>();
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "DicomServer", Version = "v1" });
            });
            services.AddHangfire(config => config.UseMemoryStorage());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment() )
            {
                
            }
            app.UseDeveloperExceptionPage()
                    .UseSwagger()
                    .UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DicomServer v1"));

            app.UseRouting()
               .UseCors("AllowAllOrigins");

            //app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();
            app.UseResponseCompression();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseHangfireDashboard();
        }
    }
}
