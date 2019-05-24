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
using Microsoft.SqlServer.Management.Smo.Agent;

namespace SqlSCM.Classes
{
    public class DBObject
    {
        public string DBType;
        public string Body;
        public string Name;
        public string Schema;
    }
    public class DBServer
    {
        public string Name { get; set; }
        public string ConStr { get; set; }
        public List<string> DataBases { get; set; }
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
                ret += GetJobsToFilesV2(connection,lastrun,"");

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

        public string GetObjectsToFilesV2()
        {
            string ret = "";
                        

            var srv = _configuration.GetSection("Servers").Get<DBServer[]>().ToArray();
            
            foreach(var x in srv)
            {
                _logger.LogDebug(x.Name);

                var lastrun = System.DateTime.Now.AddHours(-1);
                
                if (File.Exists(Path.Combine(AppContext.BaseDirectory, "cfg", "lastrun_"+x.Name)))
                {
                    lastrun = new DateTime(long.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "cfg", "lastrun_"+x.Name))));
                }

                lastrun = lastrun.AddMinutes(-2);

                try
                {
                    using (SqlConnection connection = new SqlConnection(x.ConStr))
                    {
                        connection.Open();
                        if (HasChanges(connection, lastrun))
                        {
                            ret += GetJobsToFilesV2(connection, lastrun, x.Name);
                            ret += GetLinkedServersToFilesV2(connection, lastrun, x.Name);
                            foreach (var db in x.DataBases)
                            {
                                ret += GetProceduresToFileV2(connection, lastrun, x.Name, db);
                                ret += GetFunctionsToFileV2(connection, lastrun, x.Name, db);
                                ret += GetViewsToFileV2(connection, lastrun, x.Name, db);

                                GetAllObjectsAndGrantsV2(connection, lastrun, x.Name, db);

                            }
                        }
                    }

                    long t = System.DateTime.Now.Ticks;
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "cfg", "lastrun_" + x.Name), t.ToString());
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex.Message);
                }
            }
            
            return ret;
        }

        private void GetAllObjectsAndGrantsV2(SqlConnection connection, System.DateTime lastrun, string serverName, string dbName)
        {
            _logger.LogInformation(@"Begin get grants and objects def from " + serverName + " " + dbName);
            var workPath = Path.Combine(workDir, serverName, dbName);
            connection.ChangeDatabase(dbName);

            var sql = _configuration.GetSection("Main")["GetAllObjects2FileCommand"];
            var objList = connection.Query(sql).ToArray();

            File.WriteAllText(Path.Combine(workPath, "allobjects.json"), JsonConvert.SerializeObject(objList, Formatting.Indented));

            sql = _configuration.GetSection("Main")["GetGrants"];
            var grantsList = connection.Query(sql).ToArray();
            File.WriteAllText(Path.Combine(workPath, "grants.json"), JsonConvert.SerializeObject(grantsList, Formatting.Indented));

            _logger.LogInformation(@"Complete get grants and objects def from " + serverName + " " + dbName);

        }

        private string GetProceduresToFileV2(SqlConnection connection, System.DateTime lastrun,string serverName,string dbName)
        {
            _logger.LogInformation(@"Begin get SP from " + serverName+" "+dbName);
            var ret = "";
            var workPath = Path.Combine(workDir, serverName, dbName);
            try
            {

                if (Directory.Exists(Path.Combine(workPath, "P")))
                {
                    lastrun = lastrun.AddMinutes(-2);
                }
                else
                {
                    ret = "All SP";
                    Directory.CreateDirectory(Path.Combine(workPath, "P"));
                    lastrun = new System.DateTime(1900, 1, 1);

                }


                var serverConnection = new ServerConnection(connection);
                var server = new Server(serverConnection);

                var sps = server.Databases[dbName].StoredProcedures.Cast<StoredProcedure>()
                    .Where(x => x.DateLastModified >= lastrun);

                foreach (var sp in sps)
                {
                    if (ret != "All SP" | ret != "Many SPs")
                    {
                        try
                        {
                            ret += ret + "(SP) " + sp.Name;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Many sps exported");
                            ret = "Many SPs";
                        }
                    }
                    var ddl = sp.Script(
                        new ScriptingOptions()
                        {
                            SchemaQualify = true,
                            DriAll = true,
                            Permissions = true
                        }
                        ).Cast<string>().ToArray();

                    File.WriteAllLines(Path.Combine(workPath,"P", sp.Name+"_"+sp.Schema), ddl);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError("GetSPToFile " + ex.Message);
            }
            _logger.LogInformation(@"Complete get SP from " + serverName + " " + dbName);
            return ret;
        }

        private string GetFunctionsToFileV2(SqlConnection connection, System.DateTime lastrun, string serverName,string dbName)
        {
            _logger.LogInformation(@"Begin get FN from " + serverName + " " + dbName);
            var ret = "";
            var workPath = Path.Combine(workDir, serverName, dbName);
            try
            {

                if (Directory.Exists(Path.Combine(workPath, "FN")))
                {
                    lastrun = lastrun.AddMinutes(-2);
                }
                else
                {
                    ret = "All SP";
                    Directory.CreateDirectory(Path.Combine(workPath, "FN"));
                    lastrun = new System.DateTime(1900, 1, 1);

                }


                var serverConnection = new ServerConnection(connection);
                var server = new Server(serverConnection);

                var sps = server.Databases[dbName].UserDefinedFunctions.Cast<UserDefinedFunction>()
                    .Where(x => x.DateLastModified >= lastrun);

                foreach (var sp in sps)
                {
                    if (ret != "All FN" | ret != "Many FNs")
                    {
                        try
                        {
                            ret += ret + "(FN) " + sp.Name;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Many FNs exported");
                            ret = "Many FNs";
                        }
                    }
                    var ddl = sp.Script(
                        new ScriptingOptions()
                        {
                            SchemaQualify = true,
                            DriAll = true,
                            Permissions = true
                        }
                        ).Cast<string>().ToArray();

                    File.WriteAllLines(Path.Combine(workPath, "FN", sp.Name + "_" + sp.Schema), ddl);
                }
                
            }
            catch (Exception ex)
            {
                _logger.LogError("GetSPToFile " + ex.Message);
            }
            _logger.LogInformation(@"Complete get FN from " + serverName + " " + dbName);
            return ret;
        }

        private string GetTablesToFileV2(SqlConnection connection, System.DateTime lastrun,string serverName, string dbName)
        {
            var ret = "";
            var workPath = Path.Combine(workDir, serverName, dbName);

            try
            {

                if (Directory.Exists(Path.Combine(workPath, "U")))
                {
                    lastrun = lastrun.AddMinutes(-2);
                }
                else
                {
                    ret = "All Tables";
                    Directory.CreateDirectory(Path.Combine(workPath, "U"));
                    lastrun = new System.DateTime(1900, 1, 1);

                }


                var serverConnection = new ServerConnection(connection);
                var server = new Server(serverConnection);

                var sps = server.Databases[dbName].Tables.Cast<Table>()
                    .Where(x => x.DateLastModified >= lastrun);

                foreach (var sp in sps)
                {
                    if (ret != "All Tables" | ret != "Many Tables")
                    {
                        try
                        {
                            ret += ret + "(T) " + sp.Name;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Many Tables exported");
                            ret = "Many Tables";
                        }
                    }
                    var ddl = sp.Script(
                        new ScriptingOptions()
                        {
                            SchemaQualify = true,
                            DriAll = true,
                            Permissions = true
                        }
                        ).Cast<string>().ToArray();

                    File.WriteAllLines(Path.Combine(workPath, "U", sp.Name + "_" + sp.Schema), ddl);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError("GetTablesToFile " + ex.Message);
            }

            return ret;
        }

        private string GetViewsToFileV2(SqlConnection connection, System.DateTime lastrun, string serverName, string dbName)
        {
            _logger.LogInformation(@"Begin get V from " + serverName + " " + dbName);
            var ret = "";
            var workPath = Path.Combine(workDir, serverName, dbName);
            try
            {

                if (Directory.Exists(Path.Combine(workPath, "V")))
                {
                    lastrun = lastrun.AddMinutes(-2);
                }
                else
                {
                    ret = "All Tables";
                    Directory.CreateDirectory(Path.Combine(workPath, "V"));
                    lastrun = new System.DateTime(1900, 1, 1);

                }


                var serverConnection = new ServerConnection(connection);
                var server = new Server(serverConnection);

                var sps = server.Databases[dbName].Views.Cast<View>()
                    .Where(x => x.DateLastModified >= lastrun);

                foreach (var sp in sps)
                {
                    if (ret != "All View" | ret != "Many Views")
                    {
                        try
                        {
                            ret += ret + "(V) " + sp.Name;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Many Views exported");
                            ret = "Many Views";
                        }
                    }
                    var ddl = sp.Script(
                        new ScriptingOptions()
                        {
                            SchemaQualify = true,
                            DriAll = true,
                            Permissions = true
                        }
                        ).Cast<string>().ToArray();

                    File.WriteAllLines(Path.Combine(workPath, "V", sp.Name + "_" + sp.Schema), ddl);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError("GetViewsToFile " + ex.Message);
            }
            _logger.LogInformation(@"Complete get V from " + serverName + " " + dbName);
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

        public string GetJobsToFilesV2(SqlConnection connection,System.DateTime lastrun,string serverName)
        {
            _logger.LogInformation(@"Begin get jobs from " + serverName);
            var ret = "";
            var workPath = Path.Combine(workDir, serverName);
            try
            {

                if (Directory.Exists(Path.Combine(workPath, "J")))
                {
                    lastrun = lastrun.AddMinutes(-2);
                }
                else
                {
                    ret = "All jobs";
                    Directory.CreateDirectory(Path.Combine(workPath, "J"));
                    lastrun = new System.DateTime(1900, 1, 1);

                }

                
                var serverConnection = new ServerConnection(connection);
                var server = new Server(serverConnection);
                
                var jobs = server.JobServer.Jobs.Cast<Job>().Where(x => x.DateLastModified >= lastrun & x.DeleteLevel==CompletionAction.Never);

                foreach (var job in jobs)
                {
                    if (ret != "All jobs" | ret != "Many jobs")
                    {
                        try
                        {
                            ret += ret + "(J) " + job.Name;
                        }
                        catch(Exception ex)
                        {
                            _logger.LogWarning("Many jobs exported");
                            ret = "Many jobs";
                        }
                    }
                    var ddl = job.Script(
                        new ScriptingOptions()
                        {
                            DriAll = true,
                            AgentAlertJob = true,
                            AgentNotify = true,
                            AllowSystemObjects = true
                        }
                        ).Cast<string>().ToArray();

                    File.WriteAllLines(Path.Combine(workPath, "J", job.JobID.ToString()), ddl);
                }
                
            }
            catch (Exception ex)
            {
                _logger.LogError("GetJobsToFile " + ex.Message);
            }
            _logger.LogInformation(@"Complete get jobs from " + serverName);

            return ret;
        }

        public string GetLinkedServersToFilesV2(SqlConnection connection, System.DateTime lastrun, string serverName)
        {
            _logger.LogInformation(@"Begin get linked servers from " + serverName);
            var ret = "";
            var workPath = Path.Combine(workDir, serverName);
            try
            {

                if (Directory.Exists(Path.Combine(workPath, "LS")))
                {
                    lastrun = lastrun.AddMinutes(-2);
                }
                else
                {
                    ret = "All linked servers";
                    Directory.CreateDirectory(Path.Combine(workPath, "LS"));
                    lastrun = new System.DateTime(1900, 1, 1);

                }


                var serverConnection = new ServerConnection(connection);
                var server = new Server(serverConnection);

                var linkedServers = server.LinkedServers.Cast<LinkedServer>().Where(x => x.DateLastModified >= lastrun);
                    
                foreach (var ls in linkedServers)
                {
                    if (ret != "All linked servers" | ret != "Many LS")
                    {
                        try
                        {
                            ret += ret + "(LS) " + ls.Name;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Many jobs exported");
                            ret = "Many LS";
                        }
                    }
                    var ddl = ls.Script(
                        new ScriptingOptions()
                        {
                            DriAll = true,
                            AllowSystemObjects = true
                        }
                        ).Cast<string>().ToArray();

                    File.WriteAllLines(Path.Combine(workPath, "LS", ls.ID.ToString()), ddl);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError("GetLinkedServersToFile " + ex.Message);
            }
            _logger.LogInformation(@"Complete get linked servers from " + serverName);

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

        private bool HasChanges(SqlConnection connection,DateTime lastrun)
        {
            _logger.LogInformation("begin check changes");
            var sql = "SELECT 1 FROM msdb.dbo.sysjobs WHERE date_modified > @ADate UNION SELECT 1 FROM sys.Servers WHERE modify_date > @ADate UNION  SELECT 1 FROM sys.objects WHERE modify_date > @ADate UNION  SELECT 1 FROM sys.views WHERE modify_date > @ADate;";

            DynamicParameters parameter = new DynamicParameters();

            try
            {

                parameter.Add("@ADate", lastrun, System.Data.DbType.DateTime);
                var objList = connection.Query<int>(sql, parameter).ToArray();
                if (objList.Length > 0)
                {
                    _logger.LogInformation("has changes");
                    return true;
                }
                else
                {
                    _logger.LogInformation("no changes");
                    return false;
                }
            }
            catch(Exception ex)
            {
                _logger.LogError("HasChanges " + ex.Message);
            }

            return true;
        }

    }
}
