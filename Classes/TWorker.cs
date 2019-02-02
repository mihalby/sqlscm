using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSCM.Classes
{

    public interface IManualHostedService: IHostedService
    {
        Task StartManual(CancellationToken cancellationToken);
        bool IsUp();
    }

    public class TimedHostedService : IManualHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private Timer _timer;
        private IConfiguration _configuration;
        private bool imWork;

        
        public TimedHostedService(ILogger<TimedHostedService> logger,IConfiguration configuration)
        {
            
            _logger = logger;
            _configuration = configuration;
            imWork = false;
        }

        public Task StartManual(CancellationToken ct)
        {
            _timer = new Timer(DoWork, null, TimeSpan.Zero,
                TimeSpan.FromSeconds(double.Parse(_configuration.GetSection("Main")["TimeOut"])));

            return Task.CompletedTask;
        }
       
        public Task StartAsync(CancellationToken cancellationToken)

        {
            _logger.LogInformation("Timed Background Service is wait command to start.");

            /*
            _timer = new Timer(DoWork, null, TimeSpan.Zero,
                TimeSpan.FromSeconds(double.Parse(_configuration.GetSection("DB")["TimeOut"])));
            */
            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            if (imWork) return;

            imWork = true;

            _logger.LogInformation("Timed Background Service is working");

            try
            {
                var dbService = new BDService(_configuration, _logger);


                var comment = dbService.GetObjectsToFilesV2();

                //var s = dbService.AddObjectsToGit(comment);

                //_logger.LogInformation(s);


                //var lastRun = Path.Combine(AppContext.BaseDirectory, "cfg", "lastrun");
                //long t = System.DateTime.Now.Ticks;
                //File.WriteAllText(lastRun, t.ToString());
            }
            catch(Exception ex)
            {
                _logger.LogError("DoWork "+ex.Message);
            }
            finally
            {
                imWork = false;
            }

        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Timed Background Service is stopping.");

           
            _timer?.Change(Timeout.Infinite, 0);
            _timer.Dispose();
            _timer = null;
            

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        public bool IsUp()
        {
            if (_timer != null) return true;
              
            return false;
        }
    }

    
}
