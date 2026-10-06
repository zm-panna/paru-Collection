param(
 [Parameter(Mandatory=$true)][string]$PublishFolder,
 [Parameter(Mandatory=$true)][string]$SiteFolder,
 [Parameter(Mandatory=$true)][string]$BackupFolder
)
$ErrorActionPreference='Stop'
$publish=(Resolve-Path $PublishFolder).Path
$site=(Resolve-Path $SiteFolder).Path
$backup=[IO.Path]::GetFullPath($BackupFolder)
if (!(Test-Path (Join-Path $publish 'Ecommerce.Web.dll'))) { throw 'Select the complete dotnet publish output folder.' }
if ($publish -eq $site -or $publish.StartsWith($site+[IO.Path]::DirectorySeparatorChar) -or $site.StartsWith($publish+[IO.Path]::DirectorySeparatorChar)) {throw 'Publish and site folders must be separate.'}
if ($backup -eq $site -or $backup.StartsWith($site+[IO.Path]::DirectorySeparatorChar) -or $backup -eq $publish -or $backup.StartsWith($publish+[IO.Path]::DirectorySeparatorChar)) {throw 'Backup folder must be outside both source and site.'}
$offline=Join-Path $site 'app_offline.htm'
if (Test-Path $offline) {throw 'Site already offline. Resolve the previous deployment first.'}
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$destination=Join-Path $backup $stamp
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Set-Content -Path $offline -Value '<html><body>Software update in progress. Please try again shortly.</body></html>' -Encoding UTF8
try {
 Start-Sleep -Seconds 5
 & robocopy $site $destination /E /R:2 /W:2 /XF app_offline.htm /NFL /NDL /NJH /NJS
 if ($LASTEXITCODE -ge 8) {throw 'Server backup failed.'}
 # Never mirror/delete server uploads, secrets, keys or configuration.
 & robocopy $publish $site /E /R:2 /W:2 /XD Docs App_Data logs .keys /XF appsettings*.json web.config app_offline.htm /NFL /NDL /NJH /NJS
 if ($LASTEXITCODE -ge 8) {throw 'File copy failed.'}
 Remove-Item $offline
 Write-Host "Updated. Backup: $destination. Check /health, login, products, Assistant and payment sandbox."
} catch {
 Write-Host "Update stopped; site remains offline. Restore from $destination, then remove app_offline.htm after recovery."
 throw
}
