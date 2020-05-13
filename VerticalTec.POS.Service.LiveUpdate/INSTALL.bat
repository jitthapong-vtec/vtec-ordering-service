@ECHO OFF
"c:\windows\system32\sc" create vtecliveupdate binpath= "%~dp0VtecLiveUpdateService.exe" type= own start= auto
   echo Install vtecliveupdate success
   PAUSE