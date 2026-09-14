# 正規作法：建置／除錯前先釋放被鎖定的 exe（勿只隱藏 MSB3026／3027）
$ErrorActionPreference = 'Continue'
$names = @('CampusAI.Api', 'CampusAI.McpRagServer')
foreach ($n in $names) {
    $procs = Get-Process -Name $n -ErrorAction SilentlyContinue
    if ($procs) {
        $procs | ForEach-Object { Write-Host "Stopping $($_.ProcessName) (PID $($_.Id))" }
        $procs | Stop-Process -Force
    } else {
        Write-Host "$n not running"
    }
}
Start-Sleep -Seconds 1
Write-Host "Done. You can Build in Visual Studio now."
