using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Microsoft.Extensions.ObjectPool;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using IdentityProvider.Function.Infrastructure;
using System.IO;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace IdentityProvider.Function
{
    public static class ProxyFunction
    {
        [FunctionName("RouterFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(
                AuthorizationLevel.Anonymous,
                "get", "post","put", "patch",
                Route = "{*any}")]
            HttpRequest req,
            ExecutionContext context,
            ILogger log)
        {
            /* Add configuration */
            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            /* Setup environment */
            var functionPath = Path.Combine(context.FunctionAppDirectory, "bin");
            var contentPath = functionPath;
            var webRootPath = Path.Combine(functionPath, "wwwroot");
            var hostingEnvironment = new WebHostEnvironment
            {
                ContentRootPath = contentPath,
                ContentRootFileProvider = new PhysicalFileProvider(contentPath),
                WebRootPath = webRootPath,
                WebRootFileProvider = new PhysicalFileProvider(webRootPath),
                EnvironmentName = config["ASPNETCORE_ENVIRONMENT"],
                ApplicationName = Assembly.GetAssembly(typeof(IdentityServer.Startup)).FullName,
            };

            /* Add required services into DI container */
            var services = new ServiceCollection();
            var diagnosticListener = new DiagnosticListener("Microsoft.AspNetCore");
            services.AddSingleton<DiagnosticSource>(diagnosticListener);
            services.AddSingleton<DiagnosticListener>(diagnosticListener);
            services.AddSingleton<ObjectPoolProvider>(new DefaultObjectPoolProvider());
            services.AddSingleton<IWebHostEnvironment>(hostingEnvironment);
            services.AddSingleton<IHostEnvironment>(hostingEnvironment);
            services.AddSingleton<IConfiguration>(config);

            /* Instantiate standard ASP.NET Core Startup class */
            var startup = new IdentityServer.Startup();

            /* Add web app services into DI container */
            startup.ConfigureServices(services);

            /* Initialize DI container */
            var serviceProvider = services.BuildServiceProvider();

            /* Initialize Application builder */
            var appBuilder = new ApplicationBuilder(serviceProvider, new FeatureCollection());

            /* Configure the HTTP request pipeline */
            startup.Configure(appBuilder, hostingEnvironment);

            /* Build request handling function */
            var requestHandler = appBuilder.Build();

            /* Set DI container for HTTP Context */
            req.HttpContext.RequestServices = serviceProvider;

            /* Handle HTTP request */
            await requestHandler.Invoke(req.HttpContext);

            /* This dummy result does nothing, HTTP response is already set by requestHandler */
            return new EmptyResult();
        }
    }
}
