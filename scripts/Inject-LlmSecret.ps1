# Inject LLM API key into local User Secrets (not into repo/Git)
# Run from repo root in an OPEN terminal (do not only double-click):
#   .\scripts\Inject-LlmSecret.ps1

$ErrorActionPreference = "Stop"

function Pause-End([string]$Message) {
    Write-Host ""
    Write-Host $Message -ForegroundColor Yellow
    Read-Host "Press Enter to close"
}

try {
    chcp 65001 | Out-Null
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
} catch {}

$backend = Join-Path $PSScriptRoot "..\backend"
if (-not (Test-Path $backend)) {
    Write-Host "ERROR: backend folder not found: $backend" -ForegroundColor Red
    Pause-End "Fix path then run again."
    exit 1
}
$backend = (Resolve-Path $backend).Path

Write-Host ""
Write-Host "Campus AI Agent - Secret Injection (User Secrets)" -ForegroundColor Cyan
Write-Host "Key goes into local user secrets, NOT into the repo." -ForegroundColor DarkGray
Write-Host "Get Gemini key: https://aistudio.google.com/api-keys" -ForegroundColor DarkGray
Write-Host ""

$provider = Read-Host "Provider (1=Gemini recommended, 2=OpenAI) [1]"
if ([string]::IsNullOrWhiteSpace($provider)) { $provider = "1" }

$secretName = switch ($provider) {
    "2" { "OpenAI:ApiKey" }
    default { "Gemini:ApiKey" }
}

Write-Host "Config key: $secretName" -ForegroundColor Yellow
Write-Host "Paste the key below, then press Enter." -ForegroundColor DarkGray
# Plain Read-Host: more reliable when pasting in Windows Terminal / Cursor
$plain = Read-Host "API key"

if ([string]::IsNullOrWhiteSpace($plain)) {
    Write-Host "ERROR: Empty key. Nothing was saved." -ForegroundColor Red
    Pause-End "Run the script again."
    exit 1
}

Push-Location $backend
try {
    Write-Host "Saving with: dotnet user-secrets set ..." -ForegroundColor DarkGray
    $out = & dotnet user-secrets set $secretName $plain 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: dotnet user-secrets failed." -ForegroundColor Red
        Write-Host ($out | Out-String)
        Pause-End "See error above."
        exit 1
    }

    Write-Host ""
    Write-Host "SUCCESS: injected $secretName (value not shown)." -ForegroundColor Green
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1) Restart backend:  cd backend ; dotnet run"
    Write-Host "  2) Open http://localhost:5088/api/health"
    Write-Host "     Expect: llmEnabled=true , secretSource=user_secrets"
    Write-Host "  3) Keep frontend running: cd frontend ; npm run dev"
    Write-Host "     UI: http://localhost:5173"
    Write-Host ""
    Write-Host "Verify key NAME exists (may also print value locally - do not share):" -ForegroundColor DarkGray
    Write-Host "  cd backend ; dotnet user-secrets list"
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Pause-End "Script failed."
    exit 1
}
finally {
    Pop-Location
    $plain = $null
}

Pause-End "Done. You can close this window after reading."
