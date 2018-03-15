using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using System.Diagnostics;
using System.IO;
using Serilog;
using Serilog.Filters;
using IssueTrackerAPI.Controllers;
using CommonLibrary;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Swashbuckle.AspNetCore.Swagger;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace IssueTrackerAPI
{
    public class Startup
    {

        public static string ConnectionString { get; private set; }
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsEnvironment("Development"))
            {
                // This will push telemetry data through Application Insights pipeline faster, allowing you to view results immediately.
                builder.AddApplicationInsightsSettings(developerMode: true);
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();

            //Sanjay[2017-02-11] For Logging Configuration
#if DEBUG
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Error()
            .WriteTo.RollingFile("../logs/error_log-{Date}.txt")
            .WriteTo.Logger(l => l
            .MinimumLevel.Warning()
            .WriteTo.RollingFile("../logs/warling_log-{Date}.log")).CreateLogger();
#else
                Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.RollingFile("../logs/log-{Date}.txt").CreateLogger();
#endif

        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddApplicationInsightsTelemetry(Configuration);

            var corsBuilder = new CorsPolicyBuilder();
            corsBuilder.AllowAnyHeader();
            corsBuilder.AllowAnyMethod();
            corsBuilder.AllowAnyOrigin();
            corsBuilder.AllowCredentials();

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", corsBuilder.Build());
            });

            // Sanjay [2017-02-08] All Properties Initials were got automatically converted in lowercase. Following code convert it into proper format
            services.AddMvc().AddJsonOptions(options => { options.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented; });
            services.AddMvc().AddJsonOptions(options => options.SerializerSettings.ContractResolver = new DefaultContractResolver());


            //Sanjay [2018-02-12] To Maintain API Documentation with Versions
            services.AddMvcCore().AddVersionedApiExplorer(o => o.GroupNameFormat = "'v'VVV");

            services.AddApiVersioning(option =>
            {
                option.ReportApiVersions = true;
                //option.ApiVersionReader = new HeaderApiVersionReader("api-version");
                option.AssumeDefaultVersionWhenUnspecified = true;
                option.DefaultApiVersion = new ApiVersion(1, 0);
            });

            services.AddSwaggerGen(
       options =>
       {
           var provider = services.BuildServiceProvider()
                                  .GetRequiredService<IApiVersionDescriptionProvider>();

           foreach (var description in provider.ApiVersionDescriptions)
           {
               options.SwaggerDoc(
                   description.GroupName,
                   new Info()
                   {
                       Title = $"SimpliResolveAPI {description.ApiVersion}",
                       Version = description.ApiVersion.ToString(),
                       Description = "SimpliResolve V 1.0 Web API Services",
                       Contact = new Contact()
                       { Name = "Vega Innovations & Technoconsultants Pvt Ltd", Email = "sanjay.gunjal@vegainnovations.co.in", Url = "www.vegainnovations.co.in" }

                   });
               options.IncludeXmlComments(GetXmlCommentsPath());
               options.DescribeAllEnumsAsStrings();
           }
       });

            services.AddMvc();
            ConnectionString = Configuration.GetSection("Data:DefaultConnection").Value.ToString();

            CommonLibrary.Constants.sqlConnStr = ConnectionString;


        }


        private string GetXmlCommentsPath()
        {
            var app = PlatformServices.Default.Application;
            return System.IO.Path.Combine(app.ApplicationBasePath, "IssueTrackerAPI.xml");
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IApiVersionDescriptionProvider provider)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();
            app.UseCors("AllowAll");
            app.UseApplicationInsightsRequestTelemetry();

            app.UseApplicationInsightsExceptionTelemetry();


            //Sanjay [2017-02-11] For Logging
            if (env.IsDevelopment())
            {
                loggerFactory.AddDebug(LogLevel.Information).AddSerilog();
            }
            else
            {
                loggerFactory.AddDebug(LogLevel.Error).AddSerilog();
            }

            app.UseMvc();

            //Sanjay [2018-02-12] To Implement Swagger Documentation for APIs
            app.UseSwagger();
            app.UseSwaggerUI(
                options =>
                {
                    foreach (var description in provider.ApiVersionDescriptions)
                    {
                        options.SwaggerEndpoint(
                            $"/swagger/{description.GroupName}/swagger.json",
                            description.GroupName.ToUpperInvariant());
                    }
                });
        }
    }
}
