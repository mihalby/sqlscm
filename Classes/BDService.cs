using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;


namespace SqlSCM.Classes
{
    public class DBObject
    {
        public string DBType;
        public string Body;
        public string Name;
        public string Schema;
    }
    public class BDService
    {
        private IConfiguration _configuration;
        private ILogger _logger;
        private string workDir;
                
        public BDService(IConfiguration configuration,ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
            workDir = Path.Combine(AppContext.BaseDirectory, Path.Combine(_configuration.GetSection("Folders")["ProjectFolder"].Split('/')));
        }

        public string GetObjectsToFiles()
        {
            string ret = "";
            //string workDir = Path.Combine(AppContext.BaseDirectory, _configuration.GetSection("Folders")["ProjectFolder"]);
            //_configuration.GetSection("Folders")["ProjectFolder"];
            using (SqlConnection connection = new SqlConnection(_configuration.GetSection("DB")["ConStr"]))
            {
                connection.Open();

                var sql =_configuration.GetSection("DB")["GetCommand"];

                var lastrun = System.DateTime.Now.AddHours(-1);
                if (File.Exists(Path.Combine(AppContext.BaseDirectory, "cfg", "lastrun")))
                {
                    lastrun = new DateTime(long.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "cfg", "lastrun"))));
                }
                
                lastrun =lastrun.AddMinutes(-2);

                DynamicParameters parameter = new DynamicParameters();

                parameter.Add("@ADate", lastrun, System.Data.DbType.DateTime);
                _logger.LogWarning("LastRun " + lastrun.ToLongTimeString());

                var objects = connection.Query<DBObject>(sql,parameter).ToArray();
                
                foreach(var obj in objects)
                {
                    ret += " ("+obj.DBType+")" + obj.Name;
                    File.WriteAllText(Path.Combine(workDir, obj.DBType.Trim(),obj.Name.Trim()),obj.Body);
                }

                sql= _configuration.GetSection("DB")["GetAllObjects2FileCommand"];
                var objList= connection.Query(sql).ToArray();
                

                File.WriteAllText(Path.Combine(workDir,"allobjects.json"), JsonConvert.SerializeObject(objList,Formatting.Indented));

                sql = _configuration.GetSection("DB")["GetGrants"];
                var grantsList = connection.Query(sql).ToArray();
                File.WriteAllText(Path.Combine(workDir, "grants.json"), JsonConvert.SerializeObject(grantsList, Formatting.Indented));

                ret+=GetTablesToFile(connection, lastrun);
                ret += GetViewsToFile(connection, lastrun);

            }

            return ret;
        }

        public void GetFullObjectsToFiles()
        {
            //string workDir = Path.Combine(AppContext.BaseDirectory, _configuration.GetSection("Folders")["ProjectFolder"]);
            //_configuration.GetSection("Folders")["ProjectFolder"];
            _logger.LogInformation("Connect to DB -" + _configuration.GetSection("DB")["ConStr"]);
            try
            {
                using (SqlConnection connection = new SqlConnection(_configuration.GetSection("DB")["ConStr"]))
                {
                    connection.Open();

                    var sql = _configuration.GetSection("DB")["FullGetCommand"];

                    var objects = connection.Query<DBObject>(sql).ToArray();

                    foreach (var obj in objects)
                    {
                        _logger.LogInformation("Write file -" + Path.Combine(workDir, obj.DBType.Trim(), obj.Name.Trim()));
                        File.WriteAllText(Path.Combine(workDir, obj.DBType.Trim(), obj.Name.Trim()), obj.Body);
                    }



                }
            }
            catch(Exception ex)
            {
                _logger.LogError("Error GetFullObjectsToFiles " + ex.Message);
                throw ex;
            }
        }

        public string AddObjectsToGit(string comment)
        {
            string ret="";
            string folder = Path.Combine(AppContext.BaseDirectory, _configuration.GetSection("Folders")["ProjectFolder"]);
            comment = System.DateTime.Now.ToString("yyyyMMdd") +" "+ comment;
            
            
            var cmd = "git status";
            var result =   ShellHelper.Cmd(cmd, folder, _logger);
            ret = "\n" + result;

            if (result.Contains("Initial commit"))
            {
                cmd = "git add -A .";
                result = ShellHelper.Cmd(cmd, folder, _logger);
                ret = "\n" + result;
                cmd = string.Format(@"git commit -m ""{0}""", comment);
                result = ShellHelper.Cmd(cmd, folder, _logger);
                ret += "\n" + result;

                return ret;
            }
                                    
            if (!result.Contains("nothing to commit") && result != "")
            {
                cmd = "git add -A .";
                result = ShellHelper.Cmd(cmd, folder, _logger);
                ret += "\n" + result;
                cmd = string.Format( @"git commit -m ""{0}""", comment);
                result = ShellHelper.Cmd(cmd, folder, _logger);
                ret += "\n" + result;
                //send to remote
                var remoteHTTP = _configuration.GetSection("git")["remote"];
                if (remoteHTTP.Contains("http")|| (remoteHTTP.Contains("git@")&&System.IO.File.Exists(Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".ssh", "id_rsa"))))
                {
                    cmd = string.Format(@"git push {0} HEAD", remoteHTTP);
                    result = ShellHelper.Cmd(cmd, folder, _logger);
                    ret += "\n" + result;
                }
            }

            //config core.autocrlf true
            return ret;
        }

        private string GetTablesToFile(SqlConnection connection, System.DateTime lastrun)
        {
            var ret = "";
            var sql = "SELECT name,'' AS Body, type AS DBType,SCHEMA_NAME(Schema_ID) AS [Schema] FROM sys.objects WITH (nolock) WHERE type = 'U' AND modify_date>=@ADate ORDER BY name";
            
            DynamicParameters parameter = new DynamicParameters();

            try
            {

                if (Directory.Exists(Path.Combine(workDir, "U")))
                {
                    lastrun = lastrun.AddMinutes(-2);
                }
                else
                {
                    ret = "All tables";
                    Directory.CreateDirectory(Path.Combine(workDir, "U"));
                    lastrun = new System.DateTime(1900, 1, 1);
                    
                }
                parameter.Add("@ADate", lastrun, System.Data.DbType.DateTime);

                var objList = connection.Query<DBObject>(sql,parameter).ToArray();

                foreach (var obj in objList)
                {
                    if(ret!="All tables")
                        ret += ret + "(T) " + obj.Name; 
                    var ddl = this.GetTableScript(obj.Name, obj.Schema);
                    File.WriteAllLines(Path.Combine(workDir, "U", obj.Schema+"_"+obj.Name), ddl);
                }
            }
            catch(Exception ex)
            {
                _logger.LogError("GetTablesToFile "+ex.Message);
            }

            return ret;
        }

        private string GetViewsToFile(SqlConnection connection, System.DateTime lastrun)
        {
            var ret = "";
            var sql = "SELECT name,'' AS Body, type AS DBType,SCHEMA_NAME(Schema_ID) AS [Schema] FROM sys.objects WITH (nolock) WHERE type = 'V' AND modify_date>=@ADate ORDER BY name";

            DynamicParameters parameter = new DynamicParameters();

            try
            {

                if (Directory.Exists(Path.Combine(workDir, "V")))
                {
                    lastrun = lastrun.AddMinutes(-2);
                }
                else
                {
                    ret = "All views";
                    Directory.CreateDirectory(Path.Combine(workDir, "V"));
                    lastrun = new System.DateTime(1900, 1, 1);

                }
                parameter.Add("@ADate", lastrun, System.Data.DbType.DateTime);

                var objList = connection.Query<DBObject>(sql, parameter).ToArray();

                foreach (var obj in objList)
                {
                    if (ret != "All views")
                        ret += ret + "(V) " + obj.Name;
                    var ddl = this.GetViewScript(obj.Name, obj.Schema);
                    File.WriteAllLines(Path.Combine(workDir, "V", obj.Schema + "_" + obj.Name), ddl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("GetViewsToFile " + ex.Message);
            }

            return ret;
        }



        public string[] GetTableScript(string table,string schema="dbo")
        {
            string[] st;
            using (SqlConnection connection = new SqlConnection(_configuration.GetSection("DB")["ConStr"]))
            {
                connection.Open();
                var serverConnection = new ServerConnection(connection);
                var server = new Server(serverConnection);
                st = server.Databases[serverConnection.CurrentDatabase]
                    .Tables[table,schema]
                    .Script(new ScriptingOptions
                    {
                        SchemaQualify = true,
                        DriAll = true,
                        Permissions =true
                    }
                    )
                    .Cast<string>()
                    .Select(s => s + "\n GO")
                    .ToArray();
                    
            }

            return st;
        }

        public string[] GetViewScript(string view, string schema = "dbo")
        {
            string[] st;
            using (SqlConnection connection = new SqlConnection(_configuration.GetSection("DB")["ConStr"]))
            {
                connection.Open();
                var serverConnection = new ServerConnection(connection);
                var server = new Server(serverConnection);
                st = server.Databases[serverConnection.CurrentDatabase]
                    .Views[view,schema]
                    .Script(new ScriptingOptions
                    {
                        SchemaQualify = true,
                        DriAll = true,
                        Permissions = true
                    }
                    )
                    .Cast<string>()
                    .Select(s => s + "\n GO")
                    .ToArray();

            }

            return st;
        }

    }
}
