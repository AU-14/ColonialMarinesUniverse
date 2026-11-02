# Builds the Linux server package that setup-server.sh installs.
# Run from anywhere on your Windows PC:
#   powershell -ExecutionPolicy Bypass -File Content.CMU\Tools\LiveServer\build-server.ps1
# Output: release\SS14.Server_linux-x64.zip at the repository root.
$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
Push-Location $repo
try {
    # The client zip is bundled into the server (Hybrid ACZ), so players download content from the server itself.
    dotnet run --project Content.Packaging client --no-wipe-release
    if ($LASTEXITCODE -ne 0) { throw "Client packaging failed." }

    dotnet run --project Content.Packaging server --hybrid-acz --platform linux-x64
    if ($LASTEXITCODE -ne 0) { throw "Server packaging failed." }

    $zip = Join-Path $repo "release\SS14.Server_linux-x64.zip"
    Write-Host ""
    Write-Host "Built $zip"
    Write-Host "Upload it with setup-server.sh to your VPS, for example:"
    Write-Host "  scp `"$zip`" Content.CMU\Tools\LiveServer\setup-server.sh root@YOUR_VPS_IP:~/"
}
finally {
    Pop-Location
}
