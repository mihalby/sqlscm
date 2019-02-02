[![Build Status](https://travis-ci.com/mihalby/sqlscm.svg?branch=master)](https://travis-ci.com/mihalby/sqlscm)
[![Docker Build](https://img.shields.io/docker/automated/mihalby/sqlscm.svg)](https://hub.docker.com/r/mihalby/sqlscm)
[![Docker Pulls](https://img.shields.io/docker/pulls/mihalby/sqlscm.svg)](https://hub.docker.com/r/mihalby/sqlscm)
# Source code control of programmatic and other objects for MS SQL server

## How it work
https://github.com/mihalby/sqlscm/wiki/How-it-work

## Build from source code
 ```
 git clone https://github.com/mihalby/sqlscm.git
 dotnet build "sqlscm.csproj" -c Release -o /app
 ```
## Precompiled
 Precompiled binaries for released versions are available in the https://github.com/mihalby/sqlscm/releases
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
Go to https://yourhost:8000/swagger and execute `/api/SCM/Start Start service` method on SCM controller. This action get all objects from databases, server objects and initialise
your repository in ProjectFolder. If git:remote is set git pull from remote. After each restart application you must execute action "start service" now.

## Docker
Docker images are available on [DockerHub.](https://hub.docker.com/r/mihalby/sqlscm)
You can launch a container for trying it out with
`$ docker pull mihalby/sqlscm:latest && docker run --privileged=true -e "TZ=YouTZ" -d -v /path_to_log:/app/logs -v /path_2_cfg:/app/cfg -v /path_2_project:/app/folders/project -p 8110:8000 --name mySQLSCM mihalby/sqlscm:latest`

## settings.json
- SSL:pfxPassword - password for pfx file.

- Main:TimeOut - getter timeout
- Main:GetAllObjects2FileCommand - this command get all objects small decsription defined types for export to json file.
- Main:GetGrants - this command get all grants to objects for export to json file.

- Servers - servers array, each server contains:
  Name - server name
  ConStr - connection string
  DataBases - database names array

- Folders:ProjectFolder - path to you local git repository. 
`!!!Important - this folder may be exist.`

- git:user.email - your git user email
- git:user.name - your git username
- git:remote - url to you remote repo. Support http, https or ssh. If you are use ssh you need generate or export ssh keys.

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
   /ServerName1
     /J
     /DataBaseName1
      /P
      /FN
	  /U
	  /V
	 /DataBaseName2
      /P
      /FN
	  /U
	  /V 
   /ServerName2
    /J
	...
```
## settings.json sample
```
{
  "Main": {
    "TimeOut": 40,
    "GetAllObjects2FileCommand": "SELECT name,xtype,crdate FROM sysobjects WITH (nolock) WHERE xtype IN ('U','P','FN','V','TF') ORDER BY xtype,name",
    "GetGrants": "select  princ.name,princ.type_desc,perm.permission_name,perm.state_desc,perm.class_desc,object_name(perm.major_id) AS objectName from sys.database_principals princ left join sys.database_permissions perm on      perm.grantee_principal_id = princ.principal_id"
  },
  "SSL": {
    "pfxPassword": "123123"
  },
  "Servers": [
    {
      "Name": "Server1",
      "ConStr": "workstation id=sqlscm;packet size=4096;user id=user;data source=srv1;persist security info=False;password=pwd",
      "DataBases": [ "DB1_1", "DB1_2" ]
    },
    {
      "Name": "Server2",
      "ConStr": "workstation id=sqlscm;packet size=4096;user id=user;data source=srv2;persist security info=False;password=pwd",
      "DataBases": [ "DB1_1", "DB1_2" ]
    }
  ],
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
## Having questions? 
Mail [me](mail:mihalby@gmail.com)