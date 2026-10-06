param([string]$Server='localhost',[string]$Database='ParuEcommerceERP',[Parameter(Mandatory=$true)][string]$BackupDirectory,[switch]$Differential)
. "$PSScriptRoot/common.ps1"
if ($Database -notmatch '^[A-Za-z][A-Za-z0-9_]{0,100}$') { throw 'Invalid database name.' }
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) { throw 'Install Microsoft SQL command-line tools.' }
$Stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$BackupFile=(Join-Path $BackupDirectory "$Database-$Stamp.bak").Replace("'","''")
$Options=if ($Differential) {'DIFFERENTIAL, CHECKSUM'} else {'CHECKSUM'}
& sqlcmd -S $Server -E -C -b -Q "BACKUP DATABASE [$Database] TO DISK=N'$BackupFile' WITH $Options;"
if ($LASTEXITCODE -ne 0) { throw 'SQL backup failed. Path must exist on the SQL Server host and be writable by its service account.' }
$Uploads=Join-Path $ProjectRoot 'src/Ecommerce.Web/wwwroot/uploads'
if (Test-Path $Uploads) { Compress-Archive -Path "$Uploads/*" -DestinationPath (Join-Path $BackupDirectory "uploads-$Stamp.zip") -Force }
$Documents=Join-Path $ProjectRoot 'src/Ecommerce.Web/App_Data'
if ((Test-Path $Documents) -and (Get-ChildItem $Documents -File -Recurse | Select-Object -First 1)) { Compress-Archive -Path "$Documents/*" -DestinationPath (Join-Path $BackupDirectory "documents-receipts-$Stamp.zip") -Force }
Write-Host 'Backup completed. Copy database and uploads backups to a separate protected location.'
