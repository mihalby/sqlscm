# Source code control of programmatic and other objects for MS SQL server

## Build from source code
 ```
 git clone https://github.com/mihalby/sqlscm.git
 dotnet build "sqlscm.csproj" -c Release -o /app
 ```
## Run
```
 cd /app
 dotnet sqlscm.dll
```
Manage you app from swagger UI https://yourhost:8000/swagger
### before run
In you app folder(/app) create cfg(/app/cfg) directory. Generate new certificate file(pfx format) for ssl use. And move it or you existing pfx to cfg folder.  You find sample pfx file in this repo /cfg/aspncer.pfx, password for this file is 123123. 
Create log4net.config file in /cfg directory, you find example /cfg/log4net.config.
Create settings.json file in /app/cfg folder. Copy to you app folder `SqlSCM.xml` file for correct swagger UI work.

## First run
Go to https://yourhost:8000/swagger and execute `/api/SCM/Init GetFull` method on SCM controller. This action get all objects from db and initialise
your repository in ProjectFolder.After this step you cat start service for periodicaly poll db for changes - `/api/SCM/Start Start service`.
After each restart appication you must execute action "start service" now.

## settings.json
- SSL:pfxPassword - password for pfx file.

- DB:ConStr - connection string to you database
- DB:GetCommand - command for get objects from db - `do not change it!!`
- DB:FullGetCommand - command for get all objects
- DB:GetAllObjects2FileCommand - this command get all objects small decsription defined types for export to json file.
- DB:TimeOut - getter timeout

- Folders:ProjectFolder - path to you local git repository. 
`!!!Important - this folder may be exist and contain subfolders P and FN.`

- git:user.email - your git user email
- git:user.name - your git username
- git:remote - url to you remote repo. Support http, https or ssh. If you use ssh you need generate or export ssh keys.

## sample folder and files structure
```
/App
  SqlSCM.xml #for swagger
  /cfg
    settings.json
    aspncer.pfx
    log4net.config
  /logs
  /Project
    /P
    /FN
```
## settings.json sample
```
{
  "SSL": {
    "pfxPassword": "123123"
  },
  "DB": {
    "ConStr": "workstation id=sqlscm;packet size=4096;user id=username;data source=server;persist security info=False;initial catalog=DB;password=pwd",
    "GetCommand": "select Name,object_definition(object_id) AS Body, type AS DBType from sys.objects WHERE (type = 'P' OR type = 'FN') AND modify_date>=@ADate",
    "FullGetCommand": "select Name,object_definition(object_id) AS Body, type AS DBType from sys.objects WHERE (type = 'P' OR type = 'FN')",
    "GetAllObjects2FileCommand":"SELECT name,xtype,crdate FROM sysobjects WITH (nolock) WHERE xtype IN ('U','P','FN','V','TF') ORDER BY xtype,name",
    "TimeOut": "40"
  },
  "Jwt": {
    "key": "jwt key"
  },
  "Folders": {
    "ProjectFolder": "./Project"
  },
  "git": {
    "user.email": "test@test.us",
    "user.name": "testUser",
    "remote": "none"
  }

}
```
## log4net.config sample
```
<log4net>
  <appender name="Console" type="log4net.Appender.ConsoleAppender">
    <layout type="log4net.Layout.PatternLayout">
      <!-- Pattern to output the caller's file name and line number -->
      <conversionPattern value="%date %5level %logger.%method [%line] - MESSAGE: %message%newline %exception" />
    </layout>
  </appender>
  <appender name="RollingFile" type="log4net.Appender.RollingFileAppender">
    <file value="./logs/sqlscm.log" />
    <appendToFile value="true" />
    <maximumFileSize value="100KB" />
    <maxSizeRollBackups value="2" />
    <layout type="log4net.Layout.PatternLayout">
      <conversionPattern value="%date %5level %logger.%method [%line] - MESSAGE: %message %property{ReqResponse}%newline %exception" />
    </layout>
  </appender>
  <appender name="TraceAppender" type="log4net.Appender.TraceAppender">
    <layout type="log4net.Layout.PatternLayout">
      <conversionPattern value="%date %5level %property{ClientIP} %logger.%method [%line] - MESSAGE: %message%newline %exception" />
    </layout>
  </appender>
  <appender name="ConsoleAppender" type="log4net.Appender.ManagedColoredConsoleAppender">
    <mapping>
      <level value="ERROR" />
      <foreColor value="Red" />
    </mapping>
    <mapping>
      <level value="WARN" />
      <foreColor value="Yellow" />
    </mapping>
    <mapping>
      <level value="INFO" />
      <foreColor value="White" />
    </mapping>
    <mapping>
      <level value="DEBUG" />
      <foreColor value="Green" />
    </mapping>
    <layout type="log4net.Layout.PatternLayout">
      <conversionPattern value="%date %5level %logger.%method [%line] - MESSAGE: %message%newline %exception" />
    </layout>
  </appender>
  <root>
    <level value="DEBUG" />
    <appender-ref ref="RollingFile" />
    <appender-ref ref="TraceAppender" />
    <appender-ref ref="ConsoleAppender" />
  </root>
</log4net>
```
