using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SqlSCM
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            var HostBuilder = WebHost.CreateDefaultBuilder(args);
            /*
            var q = 0;
            while( q ==0)
            {
                q = 0;
            }
            */
            
            var config = new ConfigurationBuilder()
                  .SetBasePath(Directory.GetCurrentDirectory())
                  .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "cfg", "settings.json"), optional: false, reloadOnChange: true)
                  .AddCommandLine(args)
                  .Build();
            HostBuilder.UseConfiguration(config);
            var pfxPWD=config.GetSection("SSL")["pfxPassword"];

            HostBuilder
                /*
               .ConfigureAppConfiguration((builderContext, config) =>
               {
                   IHostingEnvironment env = builderContext.HostingEnvironment;
                   try
                   {
                       config.AddJsonFile(@".\cfg\settings.json", optional: false, reloadOnChange: true);
                   }
                   catch (Exception ex)
                   {
                       throw new Exception("Settings not found\n" + ex.Message);
                   }
               })
               */

              .ConfigureLogging((hostingContext, logging) =>
              {
                // The ILoggingBuilder minimum level determines the
                // the lowest possible level for logging. The log4net
                // level then sets the level that we actually log at.
                logging.AddLog4Net(Path.Combine(AppContext.BaseDirectory, "cfg","log4net.config"));
                  logging.SetMinimumLevel(LogLevel.Debug);
              })

               .ConfigureKestrel((context, options) =>
               {

                   options.Listen(IPAddress.Any, 8000, listenOptions =>
                   {

                       listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                       listenOptions.UseHttps(Path.Combine(AppContext.BaseDirectory, "cfg","aspncer.pfx"), pfxPWD);


                   });
               })
               .UseStartup<Startup>();

            return HostBuilder;
        }
    }
}
