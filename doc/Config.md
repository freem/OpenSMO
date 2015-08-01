Config.ini
==========
OpenSMO's configuration is stored in a file called "Config.ini". It should be
placed in the same folder as the executable. The server will not run if it
cannot find the file and it does not have certain fields filled out.

Comments start with "//".

`// this is an example comment.`

`Server_Name` (string)
--------------------
Server display name.

`Server_MOTD` (string)
--------------------
Server message of the day.

`Server_IP` (string)
------------------
IP address of the server.

`Server_Port` (int)
-----------------
Port number to run the server on. The default is 8765.

`Server_FPS` (int)
----------------
Number of frames per second to run the server at.
The example Config.ini has 120 as a default value.

`Server_ReadTimeout` (int)
------------------------
(??)
The example Config.ini has 250 as a default value.

`Server_LogFile` (string)
-----------------------
Filename to write the server log to.

`Server_HigherPriority` (bool)
----------------------------
Server runs at a higher priority if true.

`Server_Offset` (int)
-------------------
Advanced setting, don't change unless necessary. Default is 128.

`Server_Version` (int)
--------------------
SMO Protocol version number?
Advanced setting, don't change unless necessary. Default is 128.

`Server_MaxPlayers` (int)
-----------------------
Maximum number of players allowed on the server.
Advanced setting, don't change unless necessary. Default is 255.

`Allow_Registration` (bool)
-------------------------
Allow registrations on server if true.

`Allow_Spectators` (bool)
-----------------------
Allow spectators in games if true.

`Database_UseCommit` (bool)
-------------------------
Performs SQLite commits if true?

`Database_CommitTime` (int)
-------------------------
Amount of time to wait for a SQLite database commit?

`MySql_Host` (string)
-------------------
Hostname for MySQL server.

`MySql_User` (string)
-------------------
Username for MySQL server.

`MySql_Password` (string)
-----------------------
Password for user on MySQL server.

`MySql_Database` (string)
-----------------------
Database name on MySQL server.

`MySql_Timeout` (string)
----------------------
Default MySQL command timeout.

`RTS_Enabled` (bool)
------------------
If true, the Remote Terminal Server is enabled.

`RTS_IP` (string)
---------------
Sets the IP address for the Remote Terminal Server.

`RTS_Port` (int)
--------------
Sets the port number for the Remote Terminal Server.

`RTS_ChatLines` (int)
-------------------
Sets the number of chat lines to show on the RTS.

`RTS_Trusted` (string)
--------------------
Sets a trusted IP for running commands on the RTS.

`Game_FullComboIsAA` (bool)
-------------------------
If true, Full Combo'ing a song gives a grade of AA (GradeTier_03).
Default from example Config.ini is true.

`Game_ShadowBan` (bool)
---------------------
If true, (??).
Default from example Config.ini is true.

`Game_AntiCheat` (bool)
---------------------
If true, (??).
Default from example Config.ini is false.
