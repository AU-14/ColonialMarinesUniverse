[CmdletBinding()]
param(
    [switch] $SkipBuild,
    [switch] $VisualTrial,
    [switch] $WeaponsTrial,
    [switch] $LaserTrial,
    [switch] $AirCombatTrial,
    [switch] $ManpadTrial,
    [switch] $GroundTrial,
    [switch] $EffectsTrial,
    [switch] $RecordPreview,
    [switch] $PerformanceTrial,
    [switch] $DisableSandboxForLocalTrial,
    [switch] $HiddenClients,
    [string] $ResourceDirectory,
    [string] $OutputDirectory,
    [ValidateRange(0, 2)] [int] $Clients = 2,
    [ValidateRange(1024, 65535)] [int] $Port = 1215
)

$ErrorActionPreference = 'Stop'
$fighterRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fighterOutput = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $fighterRoot 'bin/FighterTest' }
$fighterResources = if ($ResourceDirectory) { (Resolve-Path -LiteralPath $ResourceDirectory).Path } else { Join-Path $fighterRoot 'Content.CMU/Resources' }
$fighterBuildProperties = Join-Path $PSScriptRoot 'FighterBuild.props'
$fighterBuildTargets = Join-Path $PSScriptRoot 'FighterBuild.targets'
New-Item -ItemType Directory -Path $fighterOutput -Force | Out-Null

if (-not $SkipBuild) {
    foreach ($fighterProject in @('Content.Client', 'Content.Server')) {
        & dotnet build (Join-Path $fighterRoot "$fighterProject/$fighterProject.csproj") -c Debug "-p:OutputPath=$fighterOutput/" "-p:DirectoryBuildPropsPath=$fighterBuildProperties" "-p:DirectoryBuildTargetsPath=$fighterBuildTargets" -v:minimal
        if ($LASTEXITCODE -ne 0) { throw "$fighterProject build failed." }
    }
    # Resolve the shared output's package versions using the harness that references both applications.
    & dotnet msbuild (Join-Path $fighterRoot 'Content.IntegrationTests/Content.IntegrationTests.csproj') '-t:ResolveReferences;_CopyFilesMarkedCopyLocal' -p:BuildProjectReferences=false "-p:OutputPath=$fighterOutput/" "-p:DirectoryBuildPropsPath=$fighterBuildProperties" "-p:DirectoryBuildTargetsPath=$fighterBuildTargets" -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Fighter runtime dependency staging failed.' }
}

$fighterHasher = [Security.Cryptography.SHA256]::Create()
try { $fighterResourceKey = [BitConverter]::ToString($fighterHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes("$fighterResources|$Port|$fighterOutput"))).Replace('-', '').Substring(0, 12) }
finally { $fighterHasher.Dispose() }
$fighterRuntime = Join-Path $fighterRoot "bin/FighterRuntime-$fighterResourceKey"
foreach ($fighterFolder in @('bin/Content.Server', 'bin/Content.Client', 'Content.CMU', 'RobustToolbox')) {
    New-Item -ItemType Directory -Path (Join-Path $fighterRuntime $fighterFolder) -Force | Out-Null
}
$fighterLinks = @{
    'Resources' = (Join-Path $fighterRoot 'Resources')
    'RobustToolbox/Resources' = (Join-Path $fighterRoot 'RobustToolbox/Resources')
    'Content.CMU/Resources' = $fighterResources
    'bin/FighterTest' = $fighterOutput
}
foreach ($fighterLink in $fighterLinks.GetEnumerator()) {
    $fighterLinkPath = Join-Path $fighterRuntime $fighterLink.Key
    if (-not (Test-Path -LiteralPath $fighterLinkPath)) {
        New-Item -ItemType Junction -Path $fighterLinkPath -Target $fighterLink.Value | Out-Null
    }
}
$fighterServer = [IO.Path]::GetFullPath((Join-Path $fighterRuntime 'bin/FighterTest/Content.Server.exe'))
$fighterClient = [IO.Path]::GetFullPath((Join-Path $fighterRuntime 'bin/FighterTest/Content.Client.exe'))
$fighterExisting = @(Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq $fighterServer })
if ($fighterExisting.Count -eq 0) {
    # Keep client and server modules separate even though their build output is shared.
    foreach ($fighterModule in @('Content.Server', 'Content.Server.Database', 'Content.Packaging', 'Content.Shared', 'Content.Shared.Database')) {
        Copy-Item -LiteralPath (Join-Path $fighterOutput "$fighterModule.dll") -Destination (Join-Path $fighterRuntime 'bin/Content.Server') -Force
    }
    $fighterArgs = @('--data-dir', ('"' + (Join-Path $fighterOutput 'server-data') + '"'),
        '--cvar', "net.port=$Port", '--cvar', 'game.defaultpreset=CMUFighterDev', '--cvar', 'game.map=CMUFighterTrijent',
        '--cvar', 'game.lobbyenabled=false', '--cvar', 'fighter.development=true', '--cvar', 'auth.mode=0')
    if ($AirCombatTrial) { $fighterArgs += @('--cvar', 'fighter.air_combat_development=true') }
    if ($ManpadTrial) { $fighterArgs += @('--cvar', 'fighter.manpad_development=true') }
    if ($GroundTrial) { $fighterArgs += @('--cvar', 'fighter.ground_development=true') }
    $fighterProcess = Start-Process -FilePath $fighterServer -WorkingDirectory $fighterRoot -ArgumentList $fighterArgs -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $fighterOutput 'server.log') -RedirectStandardError (Join-Path $fighterOutput 'server-error.log')
    Write-Output "Fighter server PID $($fighterProcess.Id), port $Port."
}

$fighterDeadline = [DateTime]::UtcNow.AddSeconds(120)
do {
    $fighterReady = @(Get-NetUDPEndpoint -LocalPort $Port -ErrorAction SilentlyContinue).Count -gt 0
    if ($fighterReady -and $fighterExisting.Count -eq 0) {
        $fighterReady = Select-String -LiteralPath (Join-Path $fighterOutput 'server.log') -Pattern '-> Ready' -Quiet
    }
    if ($fighterReady) { break }
    if ($fighterExisting.Count -eq 0 -and $fighterProcess.HasExited) { throw "Fighter server exited. See $fighterOutput/server.log." }
    Start-Sleep -Milliseconds 500
} while ([DateTime]::UtcNow -lt $fighterDeadline)
if (-not $fighterReady) { throw "Fighter server did not start listening. See $fighterOutput/server.log." }

if ($Clients -gt 0 -and -not (Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq $fighterClient })) {
    foreach ($fighterModule in @('Content.Client', 'Content.Shared', 'Content.Shared.Database')) {
        Copy-Item -LiteralPath (Join-Path $fighterOutput "$fighterModule.dll") -Destination (Join-Path $fighterRuntime 'bin/Content.Client') -Force
    }
}
for ($fighterIndex = 0; $fighterIndex -lt $Clients; $fighterIndex++) {
    $fighterRole = if ($fighterIndex -eq 0) { 'Pilot' } elseif ($AirCombatTrial) { 'EnemyPilot' } else { 'Observer' }
    $fighterExistingClient = @(Get-CimInstance Win32_Process | Where-Object {
        $_.ExecutablePath -eq $fighterClient -and $_.CommandLine -match "--username Fighter$fighterRole"
    })
    if ($fighterExistingClient.Count -gt 0) { continue }
    $fighterArgs = @('--self-contained', '--connect', '--connect-address', "127.0.0.1:$Port", '--username', "Fighter$fighterRole",
        '--cvar', 'display.windowmode=0', '--cvar', 'display.width=1280', '--cvar', 'display.height=800')
    if ($VisualTrial) { $fighterArgs += @('--cvar', 'cmu.zlevels.client_diagnostics=true', '+fighter_trial') }
    if ($WeaponsTrial) { $fighterArgs += @('--cvar', 'fighter.weapon_trial=true', '+fighter_trial') }
    if ($LaserTrial) { $fighterArgs += @('--cvar', 'fighter.laser_trial=true', '+fighter_trial') }
    if ($AirCombatTrial) { $fighterArgs += @('--cvar', 'fighter.air_combat_trial=true', '+fighter_trial') }
    if ($ManpadTrial) { $fighterArgs += @('--cvar', 'fighter.manpad_trial=true', '+fighter_trial') }
    if ($GroundTrial) { $fighterArgs += '+fighter_trial' }
    if ($EffectsTrial) { $fighterArgs += @('--cvar', 'fighter.effects_trial=true') }
    if ($RecordPreview) { $fighterArgs += @('--cvar', 'fighter.record_preview=true') }
    if ($PerformanceTrial) { $fighterArgs += @('--cvar', 'fighter.performance_trial=true', '+fighter_trial') }
    $fighterPreviousSandbox = $env:ROBUST_DISABLE_SANDBOX
    try {
        if ($DisableSandboxForLocalTrial) { $env:ROBUST_DISABLE_SANDBOX = '1' }
        $fighterWindowStyle = if ($HiddenClients) { 'Hidden' } else { 'Normal' }
        $fighterProcess = Start-Process -FilePath $fighterClient -WorkingDirectory $fighterRoot -ArgumentList $fighterArgs -WindowStyle $fighterWindowStyle -PassThru `
            -RedirectStandardOutput (Join-Path $fighterOutput "$fighterRole.log") -RedirectStandardError (Join-Path $fighterOutput "$fighterRole-error.log")
    } finally { $env:ROBUST_DISABLE_SANDBOX = $fighterPreviousSandbox }
    Write-Output "Fighter $fighterRole client PID $($fighterProcess.Id)."
}
