$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot
function Invoke-DotNet { & dotnet @args; if ($LASTEXITCODE -ne 0) { throw "dotnet command failed (exit $LASTEXITCODE)." } }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET 10 SDK, then reopen PowerShell.' }
