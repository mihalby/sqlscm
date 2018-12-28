# Source code control of programmatic and other objects for MS SQL server

## Build from source code
 ```
 git clone https://github.com/mihalby/sqlscm.git
 dotnet build "sqlscm.csproj" -c Release -o /app
 ```
## Run
In you app folder(/app) create cfg(/app/cfg) directory. Generate new certificate file(pfx format) for ssl use. And move it or you existing pfx to cfg folder.  You find sample pfx file in this repo /cfg/aspncer.pfx, password for this file is 123123. 
Create log4net.config file in /cfg directory, you find example /cfg/log4net.config.
Create settings.json file in /app/cfg folder.

## settings.json
SSL:pfxPassword - password for pfx file.

DB:ConStr - connection string to you database
DB:GetCommand - command for get objects from db - do not change it!!
DB:FullGetCommand - command for get all objects
DB:GetAllObjects2FileCommand - this command get all objects small decsription defined types for export to json file.

Folders:ProjectFolder - path to you local git repository. !!!Important - this folder may be exist and contain subfolders P and FN.

git:user.email - your git user email
git:user.name - your git username
git:remote - url to you remote repo. Support http, https or ssh. If you use ssh you need generate or export ssh keys.
