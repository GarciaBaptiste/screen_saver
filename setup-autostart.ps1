#Requires -Version 5.1
<#
.SYNOPSIS
    Enregistre (ou supprime) le screensaver dans le Planificateur de tâches Windows.

.PARAMETER ExePath
    Chemin complet vers ScreenSaver.exe.
    Par défaut : dossier publish self-contained win-x64 dans le répertoire du script.

.PARAMETER Remove
    Si présent, supprime la tâche planifiée au lieu de la créer.

.EXAMPLE
    .\setup-autostart.ps1
    .\setup-autostart.ps1 -ExePath "C:\Tools\ScreenSaver\ScreenSaver.exe"
    .\setup-autostart.ps1 -Remove
#>
param(
    [string]$ExePath = (Join-Path $PSScriptRoot "ScreenSaver\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\ScreenSaver.exe"),
    [switch]$Remove
)

$TaskName = "ScreenSaverCustom"

if ($Remove) {
    if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
        Write-Host "Tâche '$TaskName' supprimée."
    } else {
        Write-Host "Tâche '$TaskName' introuvable — rien à supprimer."
    }
    exit 0
}

if (-not (Test-Path $ExePath)) {
    Write-Error "Executable introuvable : $ExePath`nPubliez d'abord : dotnet publish -r win-x64 --self-contained -c Release"
    exit 1
}

$Action   = New-ScheduledTaskAction -Execute $ExePath
$Trigger  = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$Settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Hours 0) `
    -MultipleInstances IgnoreNew
$Principal = New-ScheduledTaskPrincipal `
    -UserId $env:USERNAME `
    -LogonType Interactive `
    -RunLevel Limited

Register-ScheduledTask `
    -TaskName  $TaskName `
    -Action    $Action `
    -Trigger   $Trigger `
    -Settings  $Settings `
    -Principal $Principal `
    -Force | Out-Null

Write-Host "Tâche '$TaskName' enregistrée. Le screensaver démarrera automatiquement à la prochaine ouverture de session."
Write-Host "Pour supprimer : .\setup-autostart.ps1 -Remove"
