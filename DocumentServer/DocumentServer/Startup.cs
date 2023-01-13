using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Routing;
using Microsoft.OData.UriParser;
using Microsoft.AspNetCore.Cors.Infrastructure;
using RisDocumentServer.Controllers;
using Syncfusion.EJ2.SpellChecker;
using System.IO;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace RisDocumentServer
{
    public class Startup
    {
        internal static List<DictionaryData> spellDictCollection;
        internal static string path;
        internal static string personalDictPath;

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
            path = Configuration["SPELLCHECK_DICTIONARY_PATH"];
            string jsonFileName = Configuration["SPELLCHECK_JSON_FILENAME"];
            //check the spell check dictionary path environment variable value and assign default data folder
            //if it is null.
            path = string.IsNullOrEmpty(path) ? Path.Combine(env.ContentRootPath, "Data") : Path.Combine(env.ContentRootPath, path);
            //Set the default spellcheck.json file if the json filename is empty.
            jsonFileName = string.IsNullOrEmpty(jsonFileName) ? Path.Combine(path, "spellcheck.json") : Path.Combine(path, jsonFileName);
            if (System.IO.File.Exists(jsonFileName))
            {
                string jsonImport = System.IO.File.ReadAllText(jsonFileName);
                List<DictionaryData> spellChecks = JsonConvert.DeserializeObject<List<DictionaryData>>(jsonImport);
                spellDictCollection = new List<DictionaryData>();
                //construct the dictionary file path using customer provided path and dictionary name
                foreach (var spellCheck in spellChecks)
                {
                    spellDictCollection.Add(new DictionaryData(spellCheck.LanguadeID, Path.Combine(path, spellCheck.DictionaryPath), Path.Combine(path, spellCheck.AffixPath)));
                    personalDictPath = Path.Combine(path, spellCheck.PersonalDictPath);
                }
            }
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                   {
                       builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                   });

                options.AddPolicy("AllowAllOrigins", builder =>
                {
                    builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
                });
            });

            // Add framework services.
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            string license_key = Configuration["SYNCFUSION_LICENSE_KEY"];
            if (license_key != null && license_key != string.Empty)
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(license_key);
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDeveloperExceptionPage()
                .UseRouting();

            app.UseCors("AllowAllOrigins");

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}