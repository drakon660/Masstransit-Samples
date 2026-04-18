#Requires -Version 5.1
# Updates all NuGet packages except MassTransit.* via dotnet-outdated.

$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet-outdated -ErrorAction SilentlyContinue)) {
    Write-Host 'Installing dotnet-outdated-tool...'
    dotnet tool install --global dotnet-outdated-tool
} else {
    Write-Host 'Updating dotnet-outdated-tool...'
    dotnet tool update --global dotnet-outdated-tool
}

Push-Location $PSScriptRoot
try {
    dotnet outdated --upgrade --exclude MassTransit
}
finally {
    Pop-Location
}
