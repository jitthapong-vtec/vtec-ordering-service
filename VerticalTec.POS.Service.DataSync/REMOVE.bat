@ECHO OFF
:again 
   set /p scname=Please enter service name to remove 
   IF "%scname%" == "" (
	goto again
   ) ELSE (
	goto remove
   )
:remove
   "c:\windows\system32\sc" delete %scname%
   echo remove %scname% success
   PAUSE