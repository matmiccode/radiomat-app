param(
    [Parameter(Mandatory = $true)][ValidateSet('Company', 'Public')][string]$Edition,
    [switch]$UserAcceptance,
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$buildFile = Join-Path $root ".build/last-$Edition.json"
if (-not (Test-Path -LiteralPath $buildFile)) { throw 'Prepare this edition with Build-Release.ps1 first.' }
$build = Get-Content -LiteralPath $buildFile -Raw -Encoding UTF8 | ConvertFrom-Json
& node (Join-Path $PSScriptRoot 'Verify-Release.cjs') $build.Output $build.Config
if ($LASTEXITCODE -ne 0) { throw 'Package verification failed.' }
$config = Get-Content -LiteralPath $build.Config -Raw -Encoding UTF8 | ConvertFrom-Json
$manifest = Get-Content -LiteralPath (Join-Path $build.Output 'najnowsza.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$installer = Join-Path $build.Output $manifest.instalator
if ($manifest.wersja -ne $build.Version) { throw 'The prepared package version changed. Rebuild.' }
$checker = Join-Path $env:USERPROFILE 'matcode/narzedzia/sprawdz-firmowe.js'
$checkArguments = @($checker)
if ($Edition -eq 'Public') { $checkArguments += '--publiczne' }
$checkArguments += @($build.Publish, $build.InstallerScript, $build.Template)
& node @checkArguments
if ($LASTEXITCODE -ne 0) { throw 'Package identity check failed.' }
if (-not $Publish) {
    Write-Output "DRY-RUN: verified $Edition $($build.Version); no file was published."
    return
}
if (-not $UserAcceptance) { throw 'Publication requires completed manual acceptance and -UserAcceptance.' }
if ($Edition -eq 'Company') {
    $destination = $config.UpdateLocation
    if (-not [IO.Path]::IsPathRooted($destination) -or $destination -match '^[a-zA-Z]+://') { throw 'Invalid release directory.' }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($name in @($manifest.instalator, ($manifest.instalator + '.podpis'), ($manifest.instalator + '.sha256'))) {
        $source = Join-Path $build.Output $name
        $target = Join-Path $destination $name
        if (Test-Path -LiteralPath $target) {
            if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $source).Hash) {
                throw 'A different file already uses this release version. Do not replace a released installer.'
            }
        } else { Copy-Item -LiteralPath $source -Destination $target }
    }
    $temporary = Join-Path $destination ('latest-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    Copy-Item -LiteralPath (Join-Path $build.Output 'najnowsza.json') -Destination $temporary
    Move-Item -LiteralPath $temporary -Destination (Join-Path $destination 'najnowsza.json') -Force
    [IO.File]::WriteAllText((Join-Path $root ".build/company-published-$($build.Version).json"), '{"published":true}', (New-Object Text.UTF8Encoding($false)))
    Write-Output "Published Company $($build.Version)."
} else {
    if (-not (Test-Path -LiteralPath (Join-Path $root ".build/company-published-$($build.Version).json"))) {
        throw 'Publish and accept the Company edition first.'
    }
    & node (Join-Path $PSScriptRoot 'Prepare-PublicSource.cjs')
    if ($LASTEXITCODE -ne 0) { throw 'Public source preparation failed.' }
    & node $checker '--publiczne' (Join-Path $root '.build/public-source')
    if ($LASTEXITCODE -ne 0) { throw 'Public source identity check failed.' }
    $repo = ([Uri]$config.UpdateLocation).AbsolutePath -replace '^/repos/', '' -replace '/releases/latest$', ''
    $private = & gh repo view $repo --json isPrivate --jq .isPrivate
    if ($LASTEXITCODE -ne 0) { throw 'Cannot verify public repository visibility. Nothing was published.' }
    if ($private -ne 'false') {
        throw 'The release repository is private. Arrange a public source snapshot and repository first; do not expose development history automatically.'
    }
    & gh release create "v$($build.Version)" $installer "$installer.podpis" "$installer.sha256" --repo $repo --title "radioMAT $($build.Version)" --notes-file (Join-Path $root 'RELEASE-NOTES.md')
    if ($LASTEXITCODE -ne 0) { throw 'Public release creation failed.' }
    Write-Output "Published Public $($build.Version)."
}
