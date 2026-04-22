#Requires -Version 5.1
<#
.SYNOPSIS
    Registers ScreenSaver.exe as a scheduled task that runs at user logon.
.DESCRIPTION
    Creates a Windows Task Scheduler task:
      - Trigger  : At user logon, with a 30-second delay
      - Action   : Run ScreenSaver.exe from its published location
      - Settings : Hidden, restart on failure, run only when logged on
    Run once after publishing (dotnet publish -r win-x64 --self-contained).
    Requires the script to run as the current user (no elevation needed for
    HKCU tasks, but Task Scheduler registration may need elevation on some configs).
#>

param(
    [string]$ExePath = "$PSScriptRoot\ScreenSaver\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\ScreenSaver.exe"
)

$TaskName    = "ScreenSaverCustom"
$Description = "Custom dual-monitor screensaver (Braun / Dieter Rams style)"

if (-not (Test-Path $ExePath)) {
    Write-Warning "Executable not found at: $ExePath"
    Write-Warning "Run: dotnet publish ScreenSaver\ScreenSaver.csproj -c Release -r win-x64 --self-contained"
    Write-Warning "Then re-run this script with the correct -ExePath."
    exit 1
}

# Remove existing task if it exists
if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Write-Host "Removed existing task '$TaskName'."
}

$action  = New-ScheduledTaskAction -Execute $ExePath
$trigger = New-ScheduledTaskTrigger -AtLogOn -RandomDelay (New-TimeSpan -Seconds 30)

# Delay property is not exposed by New-ScheduledTaskTrigger -AtLogOn directly;
# set it via the XML property on the CIM instance
$trigger.Delay = "PT30S"

$settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Hours 0) `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -MultipleInstances IgnoreNew `
    -Hidden

$principal = New-ScheduledTaskPrincipal `
    -UserId ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name) `
    -LogonType Interactive `
    -RunLevel Limited

Register-ScheduledTask `
    -TaskName   $TaskName `
    -Action     $action `
    -Trigger    $trigger `
    -Settings   $settings `
    -Principal  $principal `
    -Description $Description | Out-Null

Write-Host "Task '$TaskName' registered successfully."
Write-Host "  Exe : $ExePath"
Write-Host "  Trigger: At logon + 30 s delay"
