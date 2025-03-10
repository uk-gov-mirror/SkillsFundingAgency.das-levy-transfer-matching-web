using System;
using System.IO;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SFA.DAS.Configuration.AzureTableStorage;
using SFA.DAS.LevyTransferMatching.Infrastructure.Configuration;
using SFA.DAS.LevyTransferMatching.Web.StartupExtensions;
using SFA.DAS.Validation.Mvc.Filters;

namespace SFA.DAS.LevyTransferMatching.Web
{
    public class Startup
    {
        private readonly IWebHostEnvironment _environment;
        private IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _environment = environment;
            Configuration = configuration;

            var config = new ConfigurationBuilder()
                .AddConfiguration(configuration)
                .SetBasePath(Directory.GetCurrentDirectory())
#if DEBUG
                .AddJsonFile("appsettings.json", true)
                .AddJsonFile("appsettings.Development.json", true)
#endif
                .AddEnvironmentVariables();

            if (!configuration["Environment"].Equals("DEV", StringComparison.CurrentCultureIgnoreCase))
            {
                config.AddAzureTableStorage(options =>
                    {
                        options.ConfigurationKeys = configuration["ConfigNames"].Split(",");
                        options.StorageConnectionString = configuration["ConfigurationStorageConnectionString"];
                        options.EnvironmentName = configuration["Environment"];
                        options.PreFixConfigurationKeys = false;
                    }
                );
            }

            Configuration = config.Build();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddConfigurationOptions(Configuration);
            var config = Configuration.GetSection<LevyTransferMatchingWeb>();

            services.AddSingleton(config);
            services.AddSingleton(Configuration.GetSection<LevyTransferMatchingApi>());

            services.AddControllersWithViews();

            services.AddMvc(options =>
            {
                options.Filters.Add<ValidateModelStateFilter>(int.MaxValue);
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());

            })
            .AddControllersAsServices()
            .AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<Startup>());

            services.AddEmployerAuthentication(Configuration.GetSection<Authentication>());
            services.AddCache(_environment, config);
            services.AddCookieTempDataProvider();
            services.AddDasDataProtection(config, _environment);
            services.AddDasHealthChecks();
            services.AddServiceRegistrations();

            #if DEBUG
            services.AddControllersWithViews().AddRazorRuntimeCompilation();
            #endif
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseDasHealthChecks();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
