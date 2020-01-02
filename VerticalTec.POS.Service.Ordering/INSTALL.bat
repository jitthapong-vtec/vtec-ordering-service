@ECHO OFF
:again 
   set /p scname=Please enter service name 
   IF "%scname%" == "" (
	goto again
   ) ELSE (
	goto install
   )
:install
   "c:\windows\system32\sc" create %scname% binpath= "%~dp0VerticalTec.POS.Service.Ordering.exe" type= own start= auto
   echo Install %scname% success
   PAUSE