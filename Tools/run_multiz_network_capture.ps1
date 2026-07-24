[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OutputDirectory,

    [string] $RepositoryRoot,

    [ValidateRange(1, 300)]
    [int] $ServerWarmupSeconds = 45,

    [ValidateRange(1, 300)]
    [int] $ClientWarmupSeconds = 45,

    [ValidateRange(1, 300)]
    [int] $CaptureSeconds = 45,

    [ValidateRange(1024, 65535)]
    [int] $Port = 12121,

    [ValidateRange(0, 5)]
    [float] $FakeLagSeconds = 0
)

$ErrorActionPreference = 'Stop'
$repoRoot = if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
} else {
    (Resolve-Path $RepositoryRoot).Path
}
$output = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    [IO.Path]::GetFullPath($OutputDirectory)
} else {
    [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
}
[IO.Directory]::CreateDirectory($output) | Out-Null

function Start-DotnetProcess {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = 'dotnet'
    $start.WorkingDirectory = $repoRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.EnvironmentVariables['ALSOFT_DRIVERS'] = 'null'

    # Windows PowerShell 5.1 runs on .NET Framework, where ArgumentList is null.
    # These capture arguments do not contain embedded quotes, so quote each one
    # explicitly for ProcessStartInfo.Arguments.
    $start.Arguments = ($Arguments | ForEach-Object {
        if ($_ -match '"') {
            throw "Process argument contains an unsupported quote: $_"
        }

        '"' + $_ + '"'
    }) -join ' '

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $previousInputEncoding = [Console]::InputEncoding
    try {
        # .NET Framework constructs the redirected StreamWriter from the
        # console encoding and otherwise emits its UTF-8 BOM as process input.
        [Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
        if (-not $process.Start()) {
            throw "Failed to start dotnet $($Arguments -join ' ')"
        }
    }
    finally {
        [Console]::InputEncoding = $previousInputEncoding
    }

    [pscustomobject]@{
        Process = $process
        StandardOutput = $process.StandardOutput.ReadToEndAsync()
        StandardError = $process.StandardError.ReadToEndAsync()
    }
}

function Send-ProcessInput {
    param(
        [Parameter(Mandatory)]
        $Capture,

        [Parameter(Mandatory)]
        [string] $Command
    )

    # Keep server-console input independent of the caller's output encoding.
    $bytes = [Text.Encoding]::UTF8.GetBytes($Command + [Environment]::NewLine)
    $Capture.Process.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length)
    $Capture.Process.StandardInput.BaseStream.Flush()
}

function Stop-CapturedProcess {
    param(
        [Parameter(Mandatory)]
        $Capture,

        [Parameter(Mandatory)]
        [string] $Command
    )

    if ($Capture.Process.HasExited) {
        return
    }

    Send-ProcessInput $Capture $Command
    if ($Capture.Process.WaitForExit(5000)) {
        return
    }

    $Capture.Process.CloseMainWindow() | Out-Null
    if ($Capture.Process.WaitForExit(10000)) {
        return
    }

    $Capture.Process.Kill()
    $Capture.Process.WaitForExit()
}

$server = $null
$client = $null
$startedAt = [DateTimeOffset]::UtcNow

try {
    $server = Start-DotnetProcess @(
        (Join-Path $repoRoot 'bin\Content.Server\Content.Server.dll'),
        '--cvar', "net.port=$Port",
        '--cvar', 'auth.mode=2',
        '--cvar', 'game.lobbyenabled=false',
        '--cvar', 'game.map=USSBushRedux',
        '--cvar', "net.fakelagmin=$FakeLagSeconds",
        '--cvar', 'net.fakelagrand=0'
    )

    Start-Sleep -Seconds $ServerWarmupSeconds
    if ($server.Process.HasExited) {
        throw "Server exited before capture with code $($server.Process.ExitCode)."
    }

    Send-ProcessInput $server 'cmu_znet_stats arm'

    $client = Start-DotnetProcess @(
        (Join-Path $repoRoot 'bin\Content.Client\Content.Client.dll'),
        '--connect',
        '--connect-address', "127.0.0.1:$Port",
        '--username', 'CMUNetCapture',
        '--cvar', 'display.windowmode=0',
        '--cvar', 'display.width=640',
        '--cvar', 'display.height=480',
        '--cvar', 'display.vsync=false',
        '--cvar', 'display.max_fps=30',
        '--cvar', "net.fakelagmin=$FakeLagSeconds",
        '--cvar', 'net.fakelagrand=0'
    )

    Start-Sleep -Seconds $ClientWarmupSeconds
    if ($client.Process.HasExited) {
        throw "Client exited during warmup with code $($client.Process.ExitCode)."
    }

    Send-ProcessInput $server 'cmu_znet_stats reset'
    Start-Sleep -Seconds $CaptureSeconds
    if ($client.Process.HasExited) {
        throw "Client exited before capture with code $($client.Process.ExitCode)."
    }

    Send-ProcessInput $server 'cmu_znet_stats'
    Start-Sleep -Seconds 2
}
finally {
    if ($client -ne $null) {
        Stop-CapturedProcess $client 'quit'
    }

    if ($server -ne $null) {
        Stop-CapturedProcess $server 'shutdown'
    }

    if ($server -ne $null) {
        [IO.File]::WriteAllText(
            (Join-Path $output 'server.stdout.log'),
            $server.StandardOutput.GetAwaiter().GetResult())
        [IO.File]::WriteAllText(
            (Join-Path $output 'server.stderr.log'),
            $server.StandardError.GetAwaiter().GetResult())
    }

    if ($client -ne $null) {
        [IO.File]::WriteAllText(
            (Join-Path $output 'client.stdout.log'),
            $client.StandardOutput.GetAwaiter().GetResult())
        [IO.File]::WriteAllText(
            (Join-Path $output 'client.stderr.log'),
            $client.StandardError.GetAwaiter().GetResult())
    }
}

$finishedAt = [DateTimeOffset]::UtcNow
$manifest = [ordered]@{
    schemaVersion = 1
    startedAtUtc = $startedAt
    finishedAtUtc = $finishedAt
    durationSeconds = ($finishedAt - $startedAt).TotalSeconds
    configuration = [ordered]@{
        serverWarmupSeconds = $ServerWarmupSeconds
        clientWarmupSeconds = $ClientWarmupSeconds
        captureSeconds = $CaptureSeconds
        port = $Port
        fakeLagSeconds = $FakeLagSeconds
    }
    processes = [ordered]@{
        serverExitCode = $server.Process.ExitCode
        clientExitCode = $client.Process.ExitCode
    }
}

$manifest |
    ConvertTo-Json -Depth 6 |
    Set-Content -Encoding utf8 (Join-Path $output 'manifest.json')

Write-Host "Multi-Z real-network capture complete: $output"
