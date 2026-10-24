# Builds the release packages into artifacts/release:
#   UsageWidget-<version>-win-x64.zip             framework-dependent (needs the .NET 10 Desktop Runtime)
#   UsageWidget-<version>-win-x64-standalone.zip  self-contained (no runtime needed, larger)
# plus SHA256SUMS.txt. Run from anywhere: powershell -File tools/publish.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src/UsageWidget/UsageWidget.csproj'
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = 'C:\Program Files\dotnet\dotnet.exe' }

$out = Join-Path $root 'artifacts/release'
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force $out | Out-Null

& $dotnet test (Join-Path $root 'tests/UsageWidget.Tests') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed; no release built.' }

# Single-file compression is only allowed for self-contained builds.
$builds = @(
    @{ Name = "UsageWidget-$version-win-x64"; Args = @('--self-contained', 'false') },
    @{ Name = "UsageWidget-$version-win-x64-standalone"; Args = @('--self-contained', 'true',
        '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true') }
)
foreach ($build in $builds) {
    $dir = Join-Path $out $build.Name
    & $dotnet publish $project -c Release -r win-x64 @($build.Args) -p:PublishSingleFile=true -p:DebugType=none -o $dir
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $($build.Name)" }
    Copy-Item (Join-Path $root 'README.md'), (Join-Path $root 'LICENSE') $dir
    Compress-Archive -Path (Join-Path $dir '*') -DestinationPath "$dir.zip" -Force
}

Get-ChildItem $out -Filter *.zip | ForEach-Object {
    "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower())  $($_.Name)"
} | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding ascii
Get-ChildItem $out -File | Format-Table Name, Length
