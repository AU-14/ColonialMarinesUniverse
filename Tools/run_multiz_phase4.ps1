[CmdletBinding()]
param(
    [ValidateRange(1, 1000000)]
    [int] $WarmupTicks = 60,

    [ValidateRange(1, 1000000)]
    [int] $CaptureTicks = 300,

    [ValidateRange(1, 128)]
    [int] $Players = 8,

    [ValidateRange(1, 10000)]
    [int] $PvsSamples = 30,

    [ValidateRange(0, 10000000)]
    [int] $SoakTicks = 18000,

    [ValidateRange(1, 1000000)]
    [int] $SoakCheckpointTicks = 900,

    [switch] $Trace,
    [switch] $SkipBuild,
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$stamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss')
$artifactDirectory = Join-Path $repoRoot "artifacts\multiz-phase4\$stamp"
$evidencePath = Join-Path $artifactDirectory 'evidence.json'
$runLogPath = Join-Path $artifactDirectory 'evidence.log'
$tracePath = Join-Path $artifactDirectory 'cpu.nettrace'

New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null

function Invoke-Checked {
    param(
        [Parameter(Mandatory)]
        [string] $FilePath,

        [Parameter(Mandatory)]
        [string[]] $Arguments,

        [Parameter(Mandatory)]
        [string] $LogPath
    )

    Write-Host "> $FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments 2>&1 | Tee-Object -FilePath $LogPath
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "Command failed with exit code $exitCode. See $LogPath"
    }
}

Push-Location $repoRoot
try {
    $startedAt = [DateTimeOffset]::UtcNow
    $head = (git rev-parse HEAD).Trim()
    $gitStatus = @(git status --short)
    $dotnetInfo = @(dotnet --info)

    $processor = Get-CimInstance Win32_Processor |
        Select-Object -First 1 Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed
    $computer = Get-CimInstance Win32_ComputerSystem |
        Select-Object -First 1 Manufacturer, Model, TotalPhysicalMemory
    $graphics = @(Get-CimInstance Win32_VideoController |
        Select-Object Name, DriverVersion, AdapterRAM)

    if (-not $SkipTests) {
        Invoke-Checked -FilePath 'dotnet' -Arguments @(
            'test',
            'Content.Tests\Content.Tests.csproj',
            '-c', 'DebugOpt',
            '--filter',
            'FullyQualifiedName~Content.Tests.Shared._CMU14.ZLevels|FullyQualifiedName~Content.Tests.Client._CMU14.ZLevels',
            '-m:1',
            '/nr:false',
            '--logger', 'console;verbosity=minimal'
        ) -LogPath (Join-Path $artifactDirectory 'unit-tests.log')

        Invoke-Checked -FilePath 'dotnet' -Arguments @(
            'test',
            'Content.IntegrationTests\Content.IntegrationTests.csproj',
            '-c', 'DebugOpt',
            '--filter',
            'FullyQualifiedName~Content.IntegrationTests._CMU14.ZLevels',
            '-m:1',
            '/nr:false',
            '--logger', 'console;verbosity=minimal'
        ) -LogPath (Join-Path $artifactDirectory 'integration-tests.log')
    }

    if (-not $SkipBuild) {
        Invoke-Checked -FilePath 'dotnet' -Arguments @(
            'build',
            'Content.Benchmarks\Content.Benchmarks.csproj',
            '-c', 'Release',
            '-m:1',
            '/nr:false'
        ) -LogPath (Join-Path $artifactDirectory 'build.log')
    }

    $evidenceArguments = @(
        (Join-Path $repoRoot 'bin\Content.Benchmarks\Content.Benchmarks.dll'),
        '--multiz-evidence',
        '--output', $evidencePath,
        '--warmup-ticks', $WarmupTicks,
        '--capture-ticks', $CaptureTicks,
        '--players', $Players,
        '--pvs-samples', $PvsSamples,
        '--soak-ticks', $SoakTicks,
        '--soak-checkpoint-ticks', $SoakCheckpointTicks
    )

    if ($Trace) {
        $localTrace = Join-Path $repoRoot 'artifacts\phase4-tools\dotnet-trace.exe'
        if (Test-Path $localTrace) {
            $traceTool = $localTrace
        }
        else {
            $traceCommand = Get-Command dotnet-trace -ErrorAction SilentlyContinue
            if ($null -eq $traceCommand) {
                throw 'dotnet-trace is unavailable. Install it with: dotnet tool install dotnet-trace --tool-path artifacts\phase4-tools'
            }

            $traceTool = $traceCommand.Source
        }

        $traceArguments = @(
            'collect',
            '--profile', 'dotnet-sampled-thread-time',
            '--format', 'Speedscope',
            '--output', $tracePath,
            '--show-child-io',
            '--',
            'dotnet'
        ) + $evidenceArguments

        Invoke-Checked -FilePath $traceTool -Arguments $traceArguments -LogPath $runLogPath
        Invoke-Checked -FilePath $traceTool -Arguments @(
            'report',
            $tracePath,
            'topN',
            '--number', '30'
        ) -LogPath (Join-Path $artifactDirectory 'cpu-top30-exclusive.txt')
        Invoke-Checked -FilePath $traceTool -Arguments @(
            'report',
            $tracePath,
            'topN',
            '--number', '30',
            '--inclusive'
        ) -LogPath (Join-Path $artifactDirectory 'cpu-top30-inclusive.txt')
    }
    else {
        Invoke-Checked -FilePath 'dotnet' -Arguments $evidenceArguments -LogPath $runLogPath
    }

    $finishedAt = [DateTimeOffset]::UtcNow
    $artifactHashes = @{}
    Get-ChildItem -File $artifactDirectory | ForEach-Object {
        $artifactHashes[$_.Name] = (Get-FileHash -Algorithm SHA256 $_.FullName).Hash
    }

    $manifest = [ordered]@{
        schemaVersion = 1
        startedAtUtc = $startedAt
        finishedAtUtc = $finishedAt
        durationSeconds = ($finishedAt - $startedAt).TotalSeconds
        git = [ordered]@{
            head = $head
            dirty = $gitStatus.Count -ne 0
            status = $gitStatus
        }
        configuration = [ordered]@{
            warmupTicks = $WarmupTicks
            captureTicks = $CaptureTicks
            players = $Players
            pvsSamples = $PvsSamples
            soakTicks = $SoakTicks
            soakCheckpointTicks = $SoakCheckpointTicks
            trace = [bool] $Trace
        }
        machine = [ordered]@{
            processor = $processor
            computer = $computer
            graphics = $graphics
            dotnetInfo = $dotnetInfo
        }
        artifacts = $artifactHashes
    }

    $manifest |
        ConvertTo-Json -Depth 8 |
        Set-Content -Encoding utf8 (Join-Path $artifactDirectory 'manifest.json')

    Write-Host "Phase 4 evidence complete: $artifactDirectory"
}
finally {
    Pop-Location
}
