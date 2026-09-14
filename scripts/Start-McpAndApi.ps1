# 一次啟動 MCP RAG + API（本機）
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$env:NUGET_PACKAGES = "$env:USERPROFILE\.nuget\packages"

Write-Host "Starting MCP RAG Server on :5099 ..."
Start-Process powershell -ArgumentList '-NoExit', '-Command', "cd '$root\mcp-rag-server'; `$env:NUGET_PACKAGES='$env:NUGET_PACKAGES'; dotnet run"

Start-Sleep -Seconds 3

Write-Host "Starting API on :5088 ..."
Start-Process powershell -ArgumentList '-NoExit', '-Command', "cd '$root\backend'; `$env:NUGET_PACKAGES='$env:NUGET_PACKAGES'; dotnet run --urls http://localhost:5088"

Write-Host "Done. Frontend: cd frontend; npx vite --host --port 5173"
