param(
    [Parameter(Mandatory = $true)][ValidateSet('Company', 'Public')][string]$Edition,
    # Company only: check another folder instead of the configured update location (e.g. a test copy).
    [string]$Directory
)
# Checks what was actually published, not what was prepared, and lists what still needs updating.
# Read-only: it never copies, publishes or edits anything outside .build.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$node = (Get-Command node -ErrorAction Stop).Source
$buildFile = Join-Path $root ".build/last-$Edition.json"
if (-not (Test-Path -LiteralPath $buildFile)) { throw 'No prepared build of this edition. Nothing to verify.' }
$build = Get-Content -LiteralPath $buildFile -Raw -Encoding UTF8 | ConvertFrom-Json
$config = Get-Content -LiteralPath $build.Config -Raw -Encoding UTF8 | ConvertFrom-Json
$version = $build.Version
$installerName = "radioMAT-Setup-$version-x64.exe"
$files = @($installerName, "$installerName.podpis", "$installerName.sha256")
$checker = Join-Path $env:USERPROFILE 'matcode/narzedzia/sprawdz-firmowe.js'
$notesCheck = Join-Path $PSScriptRoot 'Check-ReleaseNotes.cjs'
$verify = Join-Path $PSScriptRoot 'Verify-Release.cjs'
$todo = New-Object System.Collections.Generic.List[string]

function Assert-SameFiles([string]$Folder, [string[]]$Names) {
    foreach ($name in $Names) {
        $published = Join-Path $Folder $name
        if (-not (Test-Path -LiteralPath $published -PathType Leaf)) { throw "Published file missing: $name" }
        if ((Get-FileHash -LiteralPath $published).Hash -ne (Get-FileHash -LiteralPath (Join-Path $build.Output $name)).Hash) {
            throw "Published file differs from the prepared package: $name"
        }
    }
    Write-Output "OK: $($Names.Count) published files are identical to the prepared package."
}

if ($Edition -eq 'Company') {
    $destination = if ($Directory) { $Directory } else { $config.UpdateLocation }
    $manifestPath = Join-Path $destination 'najnowsza.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'najnowsza.json is missing in the published folder.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($manifest.wersja -ne $version) { throw "najnowsza.json announces $($manifest.wersja), expected $version." }
    Assert-SameFiles $destination ($files + 'najnowsza.json')
    & $node $verify $destination $build.Config
    if ($LASTEXITCODE -ne 0) { throw 'Published package verification failed (signature, checksum or description).' }
    & $node $notesCheck Company $manifestPath
    if ($LASTEXITCODE -ne 0) { throw 'Published description mentions the other edition.' }
    & $node $checker $destination $build.InstallerScript $build.Template
    if ($LASTEXITCODE -ne 0) { throw 'Brand check of the published folder failed.' }
} else {
    $repo = ([Uri]$config.UpdateLocation).AbsolutePath -replace '^/repos/', '' -replace '/releases/latest$', ''
    $tag = "v$version"
    $visibility = & gh repo view $repo --json isPrivate --jq .isPrivate
    if ($LASTEXITCODE -ne 0 -or $visibility -ne 'false') { throw "$repo must be public." }
    $origin = (& git -C $root remote get-url origin) -replace '^https://github\.com/', '' -replace '\.git$', ''
    $developmentPrivate = & gh repo view $origin --json isPrivate --jq .isPrivate
    if ($LASTEXITCODE -ne 0 -or $developmentPrivate -ne 'true') { throw "Development repository $origin must stay private." }
    Write-Output "OK: $repo is public, $origin stays private."
    $releaseJson = & gh api "repos/$repo/releases/tags/$tag"
    if ($LASTEXITCODE -ne 0) { throw "Cannot read release $tag of $repo." }
    $release = ($releaseJson -join "`n") | ConvertFrom-Json
    if ($release.draft -or $release.prerelease) { throw "Release $tag is a draft or prerelease." }
    $names = @($release.assets | ForEach-Object { $_.name })
    if ($names.Count -ne 3 -or @($files | Where-Object { $names -notcontains $_ }).Count -gt 0) {
        throw "Release $tag must contain exactly the installer, .podpis and .sha256."
    }
    $latest = & gh api "repos/$repo/releases/latest" --jq .tag_name
    if ($LASTEXITCODE -ne 0 -or $latest -ne $tag) { throw "Latest release of $repo is $latest, not ${tag}: the updater would offer $latest." }
    $download = Join-Path $root ".build/verify-published-$tag"
    if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download -Recurse -Force }
    New-Item -ItemType Directory -Path $download -Force | Out-Null
    try {
        & gh release download $tag --repo $repo --dir $download
        if ($LASTEXITCODE -ne 0) { throw "Cannot download release $tag." }
        Assert-SameFiles $download $files
        Copy-Item -LiteralPath (Join-Path $build.Output 'najnowsza.json') -Destination (Join-Path $download 'najnowsza.json')
        & $node $verify $download $build.Config
        if ($LASTEXITCODE -ne 0) { throw 'Downloaded release verification failed (signature or checksum).' }
        $body = Join-Path $download 'release-body.md'
        [IO.File]::WriteAllText($body, [string]$release.body, (New-Object Text.UTF8Encoding($false)))
        & $node $notesCheck Public $body
        if ($LASTEXITCODE -ne 0) { throw "Description of release $tag mentions the other edition: fix it with gh release edit." }
    } finally { Remove-Item -LiteralPath $download -Recurse -Force -ErrorAction SilentlyContinue }
    $pages = & gh api "repos/$repo/pages/builds/latest" --jq .status
    if ($LASTEXITCODE -ne 0 -or $pages -eq 'errored') { throw "GitHub Pages build of $repo failed or cannot be read." }
    if ($pages -ne 'built') { $todo.Add("GitHub Pages is still '$pages': run this check again in a few minutes.") }
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    foreach ($page in @($config.PublisherUrl,($config.PublisherUrl.TrimEnd('/') + '/en/'))) {
        $html = (Invoke-WebRequest -Uri ($page + '?check=' + [Guid]::NewGuid().ToString('N')) -UseBasicParsing).Content
        if ($html -match ('github\.com/' + [regex]::Escape($origin) + '(["/#?])')) { throw "$page links to the private repository $origin." }
        if ($html -notmatch [regex]::Escape("releases/download/$tag/$installerName")) {
            $todo.Add("Website $page does not offer $installerName yet: update the download link in docs/ (and in $repo).")
        }
    }
    Write-Output 'OK: website pages reachable, no links to the private repository.'
}

# What still needs updating after this release (reminders, not errors).
$central = Join-Path $env:USERPROFILE 'matcode/WYDANIA.md'
if (Test-Path -LiteralPath $central) {
    $row = Get-Content -LiteralPath $central -Encoding UTF8 | Where-Object { $_ -like '| radioMAT *' } | Select-Object -First 1
    $column = if ($Edition -eq 'Company') { 3 } else { 4 }
    $cell = if ($row) { ($row -split '\|')[$column + 1] } else { '' }
    if ($cell -notmatch [regex]::Escape("$version (")) {
        $todo.Add("matcode/WYDANIA.md: the radioMAT row does not show $Edition $version yet (version, date, gaps); commit and push in matcode.")
    }
} else { $todo.Add('MATCODE central (matcode/WYDANIA.md) not found: update the radioMAT row where it lives.') }
$state = (Get-Content -LiteralPath (Join-Path $root 'PROGRESS.md') -Raw -Encoding UTF8) -split '\(Ten blok' | Select-Object -First 1
if ($state -notmatch [regex]::Escape($version)) { $todo.Add("PROGRESS.md: the STAN block does not mention $version; overwrite it and add an entry to HISTORIA.md.") }
$todo.Add('Mailbox: close messages about this release (node ~/dyktator/skrypty/skrzynka.js przeczytane <id>).')

$record = [ordered]@{ edition = $Edition; version = $version; verified = (Get-Date).ToString('s'); todo = @($todo) }
[IO.File]::WriteAllText((Join-Path $root ".build/published-$Edition-$version.json"), ($record | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
Write-Output "VERIFIED: published $Edition $version matches the prepared, signed package."
Write-Output 'STILL TO UPDATE:'
$todo | ForEach-Object { Write-Output "  - $_" }
