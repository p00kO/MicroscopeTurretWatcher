#1) Check that AmScope isn't running:
Set-ExecutionPolicy unrestricted
Add-Type -AssemblyName System.Windows.Forms
$procTest = Get-Process -Name amscope -ErrorAction SilentlyContinue
if($procTest){
    Write-Output Fizzle snaps! 
    [System.Windows.Forms.MessageBox]::Show('Hmmmm... Looks like you already have a microscope application already running. Please close it and try again', 
    'TechInsights MicroscopseApp', 
    [System.Windows.Forms.MessageBoxButtons]::OK,
    [System.Windows.Forms.MessageBoxIcon]::Information)
    exit
}

#2) Start the microscope program and restore the reg key to 
# Set the location to the registry
Set-Location -Path 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion\Image File Execution Options'

# Remove the key
Remove-Item -Path 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\amscope.exe' -Force -Verbose

# Start Microscope app process
$micApp= Start-Process "C:\Program Files\AmScope\AmScope\x64\amscope.exe" -PassThru

# Restore the Key
Get-Item -Path 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion\Image File Execution Options' | New-Item -Name 'amscope.exe' -Force
New-ItemProperty -Path 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\amscope.exe' -Name 'Debugger' -Value "C:\Users\P00ko\source\repos\WindowsFormsApp1\test1.bat" -PropertyType String -Force

$ID=$micApp.Id
$client = Start-Process "C:\Users\P00ko\source\repos\WindowsFormsApp1\WindowsFormsApp1\bin\Release\WindowsFormsApp1.exe" -ArgumentList "$ID" -PassThru

$micApp.WaitForExit()
Stop-Process($client.Id)