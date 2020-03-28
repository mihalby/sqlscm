using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SqlSCM.Classes
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Diagnostics;
    using System.IO;

    public static class ShellHelper
    {
        public static string Cmd(this string cmd, string workDir, ILogger logger)
        {
            try
            {
                if (Path.DirectorySeparatorChar != '/') return Cmd_win(cmd, workDir, logger);

                var escapedArgs = cmd.Replace("\"", "\\\"");

                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WorkingDirectory = workDir,
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{escapedArgs}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
                process.Start();

                logger.LogInformation(string.Format("Execute command -{0} {1}- in folder {2}", process.StartInfo.FileName, process.StartInfo.Arguments, workDir));
                string result = process.StandardOutput.ReadToEnd();


                process.WaitForExit();
                logger.LogInformation(string.Format("Execute command result -{0} {1}- exit code {2} result {3}",
                process.StartInfo.FileName, process.StartInfo.Arguments, process.ExitCode, result));

                return result;

            }
            catch (Exception ex)
            {
                logger.LogCritical(string.Format("Cmd {0} workdir", cmd, workDir));
                throw new Exception(string.Format("Cmd {0} workdir", cmd, workDir));
            }

            
            
        }

        public static string Cmd_win(this string cmd, string workDir, ILogger logger)
        {
            //var escapedArgs = cmd.Replace("\"", "\\\"");
            workDir = workDir.Replace("\"", "\\\"");
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = workDir,
                    FileName = "cmd.exe",
                    Arguments = "/C "+cmd,//escapedArgs,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            logger.LogInformation(string.Format("Execute command -{0} {1}-",process.StartInfo.FileName, process.StartInfo.Arguments));
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            logger.LogInformation(string.Format("Execute command result -{0} {1}- exit code {2} result {3}", 
                process.StartInfo.FileName, process.StartInfo.Arguments,process.ExitCode, result));
            return result;
        }

    }
}
