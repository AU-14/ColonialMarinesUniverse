param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Content.Client', 'Content.Server')]
    [string] $Project
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

# A machine-local named mutex prevents the client and server launchers from
# compiling the large shared project at the same time. Include the repository
# path in the name so another checkout does not block this one.
$sha256 = [Security.Cryptography.SHA256]::Create()
try {
    $repoBytes = [Text.Encoding]::UTF8.GetBytes($repoRoot.ToLowerInvariant())
    $repoHash = [BitConverter]::ToString($sha256.ComputeHash($repoBytes)).Replace('-', '').Substring(0, 16)
}
finally {
    $sha256.Dispose()
}

$mutex = [Threading.Mutex]::new($false, "Local\CMUBuild_$repoHash")
$ownsMutex = $false

try {
    Write-Host "Waiting for the repository build slot..."
    try {
        $ownsMutex = $mutex.WaitOne()
    }
    catch [Threading.AbandonedMutexException] {
        # The previous launcher was interrupted while building. The mutex is
        # now owned by this process, so it is safe to continue.
        $ownsMutex = $true
    }

    Write-Host "Building $Project with one MSBuild worker..."
    Push-Location $repoRoot
    try {
        $buildArguments = @(
            'build'
            "$Project\$Project.csproj"
            '--configuration', 'Release'
            '--disable-build-servers'
            '-m:1'
            '/nodeReuse:false'
            '/p:BuildInParallel=false'
            '/p:UseSharedCompilation=false'
        )

        & dotnet @buildArguments
        if ($LASTEXITCODE -ne 0) {
            throw "$Project build failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    if ($ownsMutex) {
        $mutex.ReleaseMutex()
    }

    $mutex.Dispose()
}

Write-Host "Starting $Project without rebuilding..."
Push-Location $repoRoot
try {
    $runArguments = @(
        'run'
        '--project', $Project
        '--configuration', 'Release'
        '--no-build'
        '--no-restore'
    )

    & dotnet @runArguments
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
