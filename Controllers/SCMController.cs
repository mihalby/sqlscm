using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlSCM.Classes;

namespace SqlSCM.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SCMController : ControllerBase
    {
        private IConfiguration _configuration;
        private IManualHostedService _tService;
        private ILogger _logger;
        private string folder;
        
        public SCMController(IConfiguration configuration, IManualHostedService tService, ILogger<SCMController> logger) :base()
        {
            _configuration = configuration;
            _tService = tService;
            _logger = logger;
            folder = Path.Combine(AppContext.BaseDirectory, Path.Combine(_configuration.GetSection("Folders")["ProjectFolder"].Split('/') ));
        }
        // GET api/values
        /// <summary>
        /// Execute git command
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        [HttpGet("GitExec")]
        public ActionResult<IEnumerable<string>> GitExec(string command)
        {
            var cmd = "git "+command;
            return new string[] 
                 { cmd,
                   ShellHelper.Cmd(cmd, folder,_logger) };
            
            //_tService.StopAsync(new System.Threading.CancellationToken());
            /*
            var db = new BDService(_configuration);
            db.GetObjectsToFiles();

            var p=System.Diagnostics.Process.Start("");
            */

            //return new string[] { "value1", "value2" };
        }

        /// <summary>
        /// GetFull 
        /// </summary>
        /// <returns></returns>
        [HttpGet("Init")]
        public string Init()
        {
            try
            {
                _tService.StopAsync(new System.Threading.CancellationToken());
                //var folder = Path.Combine(AppContext.BaseDirectory, _configuration.GetSection("Folders")["ProjectFolder"].Replace(".\\", ""));
                var service = new BDService(_configuration,_logger);
                service.GetFullObjectsToFiles();
                var cmd= "git config --global core.autocrlf false";
                ShellHelper.Cmd(cmd, folder,_logger);
                cmd = "git init";
                ShellHelper.Cmd(cmd, folder, _logger);

                cmd= string.Format("git config --global user.email \"{0}\"",_configuration.GetSection("git")["user.email"]);
                ShellHelper.Cmd(cmd, folder, _logger);

                cmd = string.Format("git config --global user.name \"{0}\"", _configuration.GetSection("git")["user.name"]);
                ShellHelper.Cmd(cmd, folder, _logger);


                cmd = "git add -A .";
                ShellHelper.Cmd(cmd, folder, _logger);
                cmd = "git commit -m First";

                 var ret=ShellHelper.Cmd(cmd, folder, _logger);

                //_tService.StartManual(new System.Threading.CancellationToken());

                return ret;

            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            return "Init";
        }

        /// <summary>
        /// Stop service
        /// </summary>
        /// <returns></returns>
        [HttpGet("Stop")]
        public string Stop()
        {
            try
            {
                _tService.StopAsync(new System.Threading.CancellationToken());
            }
            catch(Exception ex)
            {
                return ex.Message;
            }
            return "Service stopped";
        }

        /// <summary>
        /// Start service
        /// </summary>
        /// <returns></returns>
        [HttpGet("Start")]
        public string Start()
        {
            try
            {
                //var folder = Path.Combine(AppContext.BaseDirectory, _configuration.GetSection("Folders")["ProjectFolder"].Replace(".\\", ""));  
                var cmd = "git config --global core.autocrlf false";
                ShellHelper.Cmd(cmd, folder, _logger);
                cmd = "git init";
                ShellHelper.Cmd(cmd, folder, _logger);

                cmd = string.Format("git config --global user.email \"{0}\"", _configuration.GetSection("git")["user.email"]);
                ShellHelper.Cmd(cmd, folder, _logger);

                cmd = string.Format("git config --global user.name \"{0}\"", _configuration.GetSection("git")["user.name"]);
                ShellHelper.Cmd(cmd, folder, _logger);

                try
                {
                    System.IO.File.Delete(Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "known_hosts"));
                }
                catch(Exception ex)
                {
                    _logger.LogWarning(ex.Message);
                }

                var remote = _configuration.GetSection("git")["remote"];
                if (remote.Contains("git@"))
                {
                    var pattern = "(?<=git@)(.*)(?=:)";
                    var host = System.Text.RegularExpressions.Regex.Match(remote,pattern).Value;
                    if (host != "")
                    {
                        cmd = string.Format("ssh-keyscan -H {0} >> ~/.ssh/known_hosts", host);
                        ShellHelper.Cmd(cmd, folder, _logger);
                    }
                    else
                    {
                        _logger.LogWarning("remote not found:" + remote);
                    }
                }
                

                _tService.StartManual(new System.Threading.CancellationToken());
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            return "Service started";
        }
        /// <summary>
        /// Get bgworker status
        /// </summary>
        /// <returns></returns>
        [HttpGet("IsUp")]
        public string IsUp()
        {
            
            return _tService.IsUp().ToString();
        }

        /// <summary>
        /// Get current ssh keypair
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetSSHKeyPair")]
        public string[] GetSSHKey()
        {
            _logger.LogInformation("Get open key:"+ Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa.pub"));
            return new string[] {
              System.IO.File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("HOME"),".ssh","id_rsa.pub")),
              System.IO.File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("HOME"),".ssh","id_rsa"))
            };
        }

        /// <summary>
        /// SSH generate new keypair
        /// </summary>
        /// <returns>Open key</returns>
        [HttpGet("SSHGen")]
        public string SSHGen()
        {
            try
            {
                System.IO.File.Copy(Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa.pub"), Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa.pub.old"), true);
                System.IO.File.Copy(Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa"), Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa.old"), true);
                System.IO.File.Delete(Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa.pub"));
                System.IO.File.Delete(Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa"));
            }
            catch(Exception ex)
            {
                _logger.LogWarning(ex.Message);
            }

            var cmd = @"ssh-keygen -t rsa -N """" -f /root/.ssh/id_rsa";
            ShellHelper.Cmd(cmd, folder, _logger);

            return System.IO.File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa.pub"));
        }

        /// <summary>
        /// Load and copy id_rsa and id_rsa.pub from cfg directory to ./ssh/
        /// Existing files replaced.
        /// </summary>
        /// <returns>If load is  good - return Ok. If return warning you must see log.</returns>
        [HttpGet("LoadSSHKeys")]
        public string LoadSSHKeys()
        {
            try
            {
                System.IO.File.Copy(Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa.pub"), Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa.pub.old"), true);
                System.IO.File.Copy(Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa"), Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa.old"), true);

                System.IO.File.Copy(Path.Combine(AppContext.BaseDirectory, "cfg", "id_rsa"), Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa"), true);
                System.IO.File.Copy(Path.Combine(AppContext.BaseDirectory, "cfg", "id_rsa.pub"), Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa.pub"), true);

                System.IO.File.Delete(Path.Combine(AppContext.BaseDirectory, "cfg", "id_rsa"));
                System.IO.File.Delete(Path.Combine(AppContext.BaseDirectory, "cfg", "id_rsa.pub"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex.Message);
                return "Warning";
            }


            return "Ok";
        }


    }
}
