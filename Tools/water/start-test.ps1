[CmdletBinding()]
param([switch] $SkipBuild)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runDirectory = Join-Path $repository 'bin\water-test'
$overlay = Join-Path $repository 'Content.CMU\Resources'
$serverExecutable = Join-Path $repository 'bin\Content.Server\Content.Server.exe'
$clientExecutable = Join-Path $repository 'bin\Content.Client\Content.Client.exe'
$serverConfig = Join-Path $PSScriptRoot 'server.toml'
$dataDirectory = Join-Path $runDirectory 'data'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$server = @(Get-CimInstance Win32_Process | Where-Object {
    $_.ExecutablePath -eq $serverExecutable -and $_.CommandLine -like '*water-test*'
})
$client = @(Get-CimInstance Win32_Process | Where-Object {
    $_.ExecutablePath -eq $clientExecutable -and $_.CommandLine -like '*127.0.0.1:1221*'
})

Push-Location $repository
try {
    if (-not $SkipBuild -and $server.Count -eq 0 -and $client.Count -eq 0) {
        foreach ($project in @('Content.Server', 'Content.Client')) {
            dotnet build "$project/$project.csproj" --no-restore -v quiet
            if ($LASTEXITCODE -ne 0) { throw "$project build failed." }
        }
    }

    if ($server.Count -eq 0) {
        $serverLog = Join-Path $runDirectory "server-$stamp.log"
        $serverProcess = Start-Process -FilePath $serverExecutable -WorkingDirectory $repository -WindowStyle Hidden -PassThru `
            -ArgumentList @('--mount-dir', ('"' + $overlay + '"'),
                '--config-file', ('"' + $serverConfig + '"'),
                '--data-dir', ('"' + $dataDirectory + '"')) `
            -RedirectStandardOutput $serverLog `
            -RedirectStandardError (Join-Path $runDirectory "server-$stamp.err.log")
        Write-Output "Water test server PID: $($serverProcess.Id). Logs: $runDirectory"

        # The status endpoint answers only after initialization; wait before auto-connecting.
        $ready = $false
        for ($attempt = 0; $attempt -lt 180; $attempt++) {
            if ($serverProcess.HasExited) { throw "Water test server exited. See $serverLog" }
            try {
                $status = Invoke-RestMethod -Uri 'http://127.0.0.1:1221/status' -TimeoutSec 1
                if ($status.name -eq 'CMU Water Effects Test Arena') { $ready = $true; break }
            }
            catch { }
            Start-Sleep -Seconds 1
        }
        if (-not $ready) { throw "Water test server did not become ready. See $serverLog" }
    }

    if ($client.Count -eq 0) {
        $clientProcess = Start-Process -FilePath $clientExecutable -WorkingDirectory $repository -PassThru `
            -ArgumentList @('--mount-dir', ('"' + $overlay + '"'), '--connect',
                '--connect-address', '127.0.0.1:1221', '--username', 'WaterTester',
                '--cvar', 'cmu.3d.enabled=false') `
            -RedirectStandardOutput (Join-Path $runDirectory "client-$stamp.log") `
            -RedirectStandardError (Join-Path $runDirectory "client-$stamp.err.log")
        Write-Output "Water test client PID: $($clientProcess.Id). Connecting straight into CMUWaterTest."
    }
    else { Write-Output "Water test client is already running (PID $($client[0].ProcessId))." }
}
finally { Pop-Location }
