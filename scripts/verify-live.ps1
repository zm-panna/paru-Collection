param([string]$Server='localhost',[string]$OllamaModel='', [string]$OllamaUrl='http://localhost:11434/')
. "$PSScriptRoot/common.ps1"
# Uses Windows authentication and a new dedicated test database. Never point tests at production.
$acceptanceDatabase='Paru_Acceptance_'+[Guid]::NewGuid().ToString('N')
$env:ERP_SQL_TEST_CONNECTION="Server=$Server;Database=$acceptanceDatabase;Trusted_Connection=True;TrustServerCertificate=True"
$env:ERP_BROWSER_TESTS='1'
if ($OllamaModel) { $env:ERP_OLLAMA_TEST_MODEL=$OllamaModel; $env:ERP_OLLAMA_TEST_URL=$OllamaUrl }
try {
  Write-Host "Testing dedicated database: $acceptanceDatabase"
  & "$PSScriptRoot/install-pdf.ps1"
  Invoke-DotNet test EcommerceERP.sln -c Release -m:1 --logger trx
  Write-Host "Tests finished. Retained test databases: $acceptanceDatabase and ${acceptanceDatabase}_Workflow"
} finally {
  Remove-Item Env:ERP_SQL_TEST_CONNECTION -ErrorAction SilentlyContinue
  Remove-Item Env:ERP_BROWSER_TESTS -ErrorAction SilentlyContinue
  Remove-Item Env:ERP_OLLAMA_TEST_MODEL -ErrorAction SilentlyContinue
  Remove-Item Env:ERP_OLLAMA_TEST_URL -ErrorAction SilentlyContinue
}
