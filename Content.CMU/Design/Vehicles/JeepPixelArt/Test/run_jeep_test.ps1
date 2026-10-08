<#
  Local jeep test: starts a localhost server on the jeep test map (Sandbox, no lobby) and a client
  that joins it. Build Content.Server and Content.Client first. Logs go to bin/jeep-test-run.
#>
param([string] $Username = 'JeepTester')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..')).Path
$overlay = Join-Path $repo 'Content.CMU\Resources'
$serverExe = Join-Path $repo 'bin\Content.Server\Content.Server.exe'
$clientExe = Join-Path $repo 'bin\Content.Client\Content.Client.exe'
$run = Join-Path $repo 'bin\jeep-test-run'
New-Item -ItemType Directory -Force -Path $run | Out-Null
$stamp = Get-Date -Format 'HHmmss'

Get-CimInstance Win32_Process | Where-Object {
    ($_.ExecutablePath -eq $serverExe) -or ($_.ExecutablePath -eq $clientExe -and $_.CommandLine -like '*127.0.0.1:1222*') } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

$srv = Start-Process -FilePath $serverExe -WorkingDirectory $repo -WindowStyle Hidden -PassThru `
    -ArgumentList @('--mount-dir', "`"$overlay`"", '--config-file', "`"$(Join-Path $PSScriptRoot 'server.toml')`"",
                    '--data-dir', "`"$(Join-Path $run 'data')`"") `
    -RedirectStandardOutput (Join-Path $run "server-$stamp.log") -RedirectStandardError (Join-Path $run "server-$stamp.err.log")

for ($i = 0; $i -lt 300; $i++) {
    if ($srv.HasExited) { throw "server exited, see $run\server-$stamp.log" }
    try { $s = Invoke-RestMethod -Uri 'http://127.0.0.1:1222/status' -TimeoutSec 1; if ($s.name -eq 'CMU Jeep Test') { break } } catch { }
    Start-Sleep -Seconds 1
}
Write-Output "server up after ${i}s (log server-$stamp.log)"

Start-Process -FilePath $clientExe -WorkingDirectory $repo `
    -ArgumentList @('--mount-dir', "`"$overlay`"", '--connect', '--connect-address', '127.0.0.1:1222',
                    '--username', $Username, '--cvar', 'cmu.ui_configured=true') `
    -RedirectStandardOutput (Join-Path $run "client-$stamp.log") -RedirectStandardError (Join-Path $run "client-$stamp.err.log") | Out-Null
Write-Output "client started (log client-$stamp.log)"
