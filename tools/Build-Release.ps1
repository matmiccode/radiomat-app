param(
    [ValidateSet('Company', 'Public')][string]$Edition,
    [string]$Config,
    [string]$Compiler,
    [string]$SigningKey,
    [string]$BrandChecker = (Join-Path $env:USERPROFILE 'matcode\narzedzia\sprawdz-firmowe.js'),
    [switch]$DryRun,
    [switch]$PublishOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'Prepare-Edition.ps1')
if ($DryRun -and $PublishOnly) { throw 'Choose either -DryRun or -PublishOnly.' }
$build = New-EditionBuild -Root $root -Edition $Edition -Config $Config
$project = Join-Path $root 'Radiomat.csproj'
$version = $build.Version
if (-not $DryRun -and -not $PublishOnly -and (-not $SigningKey -or -not (Test-Path -LiteralPath $SigningKey -PathType Leaf))) {
    throw 'Pass -SigningKey with the external Ed25519 key for this edition. No installer was created.'
}
if (-not (Test-Path -LiteralPath $BrandChecker -PathType Leaf)) { throw 'Required package identity checker not found. Pass -BrandChecker.' }
$node = (Get-Command node -ErrorAction Stop).Source
# Each edition has its own "Co nowego"; stop before compiling if it mentions the other edition.
$releaseNotes = Join-Path $root "RELEASE-NOTES.$Edition.md"
& $node (Join-Path $PSScriptRoot 'Check-ReleaseNotes.cjs') $Edition $releaseNotes
if ($LASTEXITCODE -ne 0) { throw 'Release notes check failed. No build was started.' }
function Assert-PackageIdentity([string[]]$Paths) {
    $checkerArgs = @($BrandChecker)
    if ($Edition -eq 'Public') { $checkerArgs += '--publiczne' }
    $checkerArgs += $Paths
    & $node @checkerArgs
    if ($LASTEXITCODE -ne 0) { throw 'Package identity check failed. Installer creation stopped.' }
}
if ($DryRun) {
    Assert-PackageIdentity @($build.Config, $build.Props, $build.InstallerScript, $build.Template)
    Write-Output "Dry-run ready (no compilation, installer or publication): $($build.Root)"
    return
}
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
if (-not $Compiler -and -not $PublishOnly) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    $Compiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $PublishOnly -and (-not $Compiler -or -not (Test-Path -LiteralPath $Compiler -PathType Leaf))) { throw 'Inno Setup 6 compiler not found. Pass -Compiler with the path to ISCC.exe.' }
$publish = $build.Publish
$taskAppData = Join-Path $root '.build\appdata'
New-Item -ItemType Directory -Path $taskAppData -Force | Out-Null
$original = @{}
foreach ($name in @('DOTNET_CLI_HOME', 'APPDATA', 'NUGET_PACKAGES', 'DOTNET_CLI_TELEMETRY_OPTOUT')) {
    $original[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    $env:DOTNET_CLI_HOME = $root
    $env:APPDATA = $taskAppData
    $env:NUGET_PACKAGES = Join-Path $root '.nuget'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $buildArgs = @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
        '-p:PublishTrimmed=false', '-p:DebugType=None', '-p:DebugSymbols=false',
        "-p:Edition=$Edition", "-p:EditionConfigPath=$($build.Config)", "-p:EditionPropsPath=$($build.Props)",
        "-p:BaseIntermediateOutputPath=obj\editions\$Edition\", "-p:BaseOutputPath=bin\editions\$Edition\", '-o', $publish)
    & $dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }
    Copy-Item -LiteralPath $build.Config -Destination (Join-Path $publish 'wydanie.json')
    $licenses = Join-Path $publish 'licenses'
    New-Item -ItemType Directory -Path $licenses -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $root '.nuget\naudio\2.2.1\license.txt') -Destination (Join-Path $licenses 'NAudio.txt')
    Copy-Item -LiteralPath (Join-Path $root 'Data\README.md') -Destination (Join-Path $licenses 'Station-data.md')
    $licenseTemplate = [IO.File]::ReadAllText((Join-Path $root 'LICENSE'))
    $editionData = Get-Content -LiteralPath $build.Config -Raw -Encoding UTF8 | ConvertFrom-Json
    $licenseText = $licenseTemplate.Replace('Copyright (c) 2026 MATCODE', ('Copyright (c) 2026 ' + $editionData.Publisher))
    [IO.File]::WriteAllText((Join-Path $licenses 'radioMAT.txt'), $licenseText, (New-Object Text.UTF8Encoding($false)))
    Copy-Item -LiteralPath (Join-Path $env:NUGET_PACKAGES 'nsec.cryptography\25.4.0\LICENSE') -Destination (Join-Path $licenses 'NSec.txt')
    Copy-Item -LiteralPath (Join-Path $env:NUGET_PACKAGES 'nsec.cryptography\25.4.0\NOTICE') -Destination (Join-Path $licenses 'NSec-NOTICE.txt')
    $sodium = Get-ChildItem -LiteralPath (Join-Path $env:NUGET_PACKAGES 'libsodium') -Directory | Sort-Object Name -Descending | Select-Object -First 1
    if (-not $sodium) { throw 'Missing libsodium dependency license.' }
    Copy-Item -LiteralPath (Join-Path $sodium.FullName 'LICENSE') -Destination (Join-Path $licenses 'libsodium.txt')
    $frameworks = (Get-Content -LiteralPath (Join-Path $publish 'radioMAT.runtimeconfig.json') -Raw | ConvertFrom-Json).runtimeOptions.includedFrameworks
    $coreVersion = ($frameworks | Where-Object { $_.name -eq 'Microsoft.NETCore.App' }).version
    $desktopVersion = ($frameworks | Where-Object { $_.name -eq 'Microsoft.WindowsDesktop.App' }).version
    $corePackage = Join-Path $env:NUGET_PACKAGES "microsoft.netcore.app.runtime.win-x64\$coreVersion"
    $desktopPackage = Join-Path $env:NUGET_PACKAGES "microsoft.windowsdesktop.app.runtime.win-x64\$desktopVersion"
    Copy-Item -LiteralPath (Join-Path $corePackage 'LICENSE.TXT') -Destination (Join-Path $licenses 'Microsoft.NET-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $corePackage 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $licenses 'Microsoft.NET-THIRD-PARTY-NOTICES.txt')
    Copy-Item -LiteralPath (Join-Path $desktopPackage 'LICENSE') -Destination (Join-Path $licenses 'Microsoft.WindowsDesktop-LICENSE.txt')
    Assert-PackageIdentity @($publish, $build.InstallerScript, $build.Template)
    [IO.File]::WriteAllText((Join-Path $root ".build\last-$Edition.json"), ($build | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
    if ($PublishOnly) {
        Write-Output "Compiled package ready (no installer or publication): $($build.Root)"
        return
    }
    & $Compiler '/Q' $build.InstallerScript
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $installer = Join-Path $build.Output "radioMAT-Setup-$version-x64.exe"
    & $node (Join-Path $PSScriptRoot 'Sign-Release.cjs') $installer $SigningKey $build.Config $releaseNotes
    if ($LASTEXITCODE -ne 0) { throw 'Signing failed. Installer is not ready for publication.' }
    Write-Output "Signed update package ready (no publication): $installer"
} finally {
    foreach ($name in $original.Keys) { [Environment]::SetEnvironmentVariable($name, $original[$name], 'Process') }
}
