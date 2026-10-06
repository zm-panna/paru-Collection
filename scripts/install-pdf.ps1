. "$PSScriptRoot/common.ps1"
Invoke-DotNet build src/Ecommerce.Web -c Release
& "$ProjectRoot/src/Ecommerce.Web/bin/Release/net10.0/playwright.ps1" install chromium
if ($LASTEXITCODE -ne 0) { throw 'Chromium install failed.' }
Write-Host 'Install Chromium under the same account that runs IIS/app, or configure a shared PLAYWRIGHT_BROWSERS_PATH with read/execute permissions.'
