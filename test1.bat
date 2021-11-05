powershell.exe -windowstyle hidden start-process powershell.exe -windowstyle hidden "C:\Users\P00ko\Desktop\PROJECTS\Microscope\MicroscopeApp2.ps1" -verb runas 
::reg remove "HKLM\Software\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\notepad.exe" 
::      /v "Debugger" /t REG_SZ /d "\"c:\windows\Notepad2.exe\" /z" /f	  
::Will need to add/remove the key to allow to start amscope.exe without an inifinite recursive call....
::start mascopeCopy.exe
::
::powershell.exe start-process "C:\Users\P00ko\source\repos\ConsoleApp1\ConsoleApp1\bin\Release\netcoreapp3.1\ConsoleApp1.exe" ::-verb runas
