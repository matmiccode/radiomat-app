function New-EditionBuild {
    param([string]$Root, [string]$Edition, [string]$Config)
    if (-not $Edition) { throw 'Choose -Edition Company or -Edition Public. No build was started.' }
    $publicConfig = Get-Content -LiteralPath (Join-Path $Root 'Branding\Public\edition.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($Edition -eq 'Company') {
        if (-not $Config -or -not $Config.EndsWith('.local.txt', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Company builds require -Config pointing to an ignored *.local.txt JSON file.'
        }
        if (-not (Test-Path -LiteralPath $Config -PathType Leaf)) { throw 'Local configuration not found. No build was started.' }
        try { $data = Get-Content -LiteralPath $Config -Raw -Encoding UTF8 | ConvertFrom-Json }
        catch { throw 'Local configuration is not valid JSON. No build was started.' }
    } else {
        if ($Config) { throw 'Public builds use Branding/Public/edition.json, not a local override.' }
        $data = $publicConfig
    }
    $fields = @('DisplayName', 'Publisher', 'AppId', 'AppUserModelId', 'InstallDirectory',
        'SettingsDirectory', 'RunValueName', 'InstanceName', 'SupportUri', 'UpdateLocation', 'UpdatePublicKey')
    foreach ($field in $fields) {
        $value = $data.$field
        if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value) -or $value -match '[\x00-\x1f"{}]') {
            if ($field -ne 'AppId' -or $value -isnot [string] -or $value -notmatch '^\{[0-9a-fA-F-]{36}\}$') {
                throw "Missing or invalid edition field: $field. No build was started."
            }
        }
    }
    if ($data.DisplayName -ne 'radioMAT') { throw 'The application name must be radioMAT.' }
    try { $keyBytes = [Convert]::FromBase64String($data.UpdatePublicKey) }
    catch { throw 'UpdatePublicKey must be a base64 Ed25519 public key.' }
    if ($keyBytes.Length -ne 32) { throw 'UpdatePublicKey must contain 32 bytes.' }
    $parsedId = [Guid]::Empty
    if (-not [Guid]::TryParseExact($data.AppId, 'B', [ref]$parsedId) -or $parsedId -eq [Guid]::Empty) { throw 'Invalid AppId.' }
    foreach ($field in @('InstallDirectory', 'SettingsDirectory', 'RunValueName', 'InstanceName')) {
        if ($data.$field -notmatch '^[\p{L}\p{N}][\p{L}\p{N} ._-]{0,79}$' -or $data.$field -match '\.\.|[. ]$' -or
            $data.$field -match '^(?i:CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)') { throw "Invalid component name in edition field: $field." }
    }
    if ($data.AppUserModelId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$') { throw 'Invalid AppUserModelId.' }
    if ($Edition -eq 'Company') {
        if ($data.UpdatePublicKey -eq $publicConfig.UpdatePublicKey) { throw 'Each edition requires a separate update key.' }
        foreach ($field in @('AppId', 'AppUserModelId', 'InstallDirectory', 'SettingsDirectory', 'RunValueName', 'InstanceName')) {
            if ($data.$field -eq $publicConfig.$field) { throw "Company edition must have its own $field." }
        }
        if ($data.SupportUri -notmatch '^mailto:[^\s@?]+@[^\s@?]+$') { throw 'Company SupportUri must be a mailto contact without parameters.' }
        if (-not [IO.Path]::IsPathRooted($data.UpdateLocation) -or $data.UpdateLocation -match '^[a-zA-Z]+://') {
            throw 'Company UpdateLocation must be an absolute folder path.'
        }
        foreach ($property in $data.PSObject.Properties) {
            if ([string]$property.Value -match '(?i)matcode|matmiccode|buycoffee|postaw kaw|buy me a coffee') {
                throw "Public branding is not allowed in local edition field: $($property.Name)."
            }
        }
    }
    # Copy an allowlist of fields. Arbitrary local properties never enter a package.
    $values = [ordered]@{}
    foreach ($field in $fields) { $values[$field] = $data.$field }
    if ($Edition -eq 'Public') {
        if ($data.UpdateLocation -notmatch '^https://api\.github\.com/repos/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/releases/latest$' -or
            $data.PublisherUrl -notmatch '^https://' -or $data.SupportUri -notmatch '^https://' -or $data.CoffeeUri -notmatch '^https://') {
            throw 'Invalid public update, website or support URL.'
        }
        foreach ($field in @('CoffeeUri', 'CoffeeLabelPl', 'CoffeeLabelEn', 'CoffeeGlyph')) {
            if ([string]::IsNullOrWhiteSpace($data.$field)) { throw "Missing public edition field: $field." }
            $values[$field] = $data.$field
        }
    }
    $values['PublisherUrl'] = if ($Edition -eq 'Public') { $data.PublisherUrl } else { '' }
    $values['IsPublic'] = ($Edition -eq 'Public')
    $version = ([xml][IO.File]::ReadAllText((Join-Path $Root 'Radiomat.csproj'))).SelectSingleNode('/Project/PropertyGroup/Version').InnerText
    $buildId = [Guid]::NewGuid().ToString('N')
    $buildRoot = Join-Path $Root "bin\publish\$version\$Edition\$buildId"
    $publish = Join-Path $buildRoot 'app'
    $configPath = Join-Path $buildRoot 'wydanie.json'
    $propsPath = Join-Path $buildRoot 'wydanie.props'
    $issPath = Join-Path $buildRoot 'radioMAT.iss'
    $output = Join-Path $Root "dist\$Edition"
    $utf8 = New-Object Text.UTF8Encoding($false)
    New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
    [IO.File]::WriteAllText($configPath, ($values | ConvertTo-Json), $utf8)
    $publisherXml = [Security.SecurityElement]::Escape($data.Publisher)
    [IO.File]::WriteAllText($propsPath, "<Project><PropertyGroup><Company>$publisherXml</Company><Copyright>$publisherXml</Copyright></PropertyGroup></Project>`n", $utf8)
    $defines = [ordered]@{
        AppVersion = $version
        PublicEdition = $(if ($Edition -eq 'Public') { '1' } else { '0' })
        InstallerAppId = ('{' + $data.AppId)
        Publisher = $data.Publisher
        PublisherUrl = $values['PublisherUrl']
        AppUserModelId = $data.AppUserModelId
        InstallDirectory = $data.InstallDirectory
        RunValueName = $data.RunValueName
        PublishDirectory = $publish
        OutputDirectory = $output
        IconFile = (Join-Path $Root 'Assets\radioMAT.ico')
    }
    $lines = foreach ($key in $defines.Keys) {
        if ([string]$defines[$key] -match '["\r\n]') { throw "Unsupported quote or newline in installer value: $key." }
        '#define ' + $key + ' "' + $defines[$key] + '"'
    }
    $template = Join-Path $Root 'installer\radioMAT.iss'
    $lines += '#include "' + $template + '"'
    [IO.File]::WriteAllText($issPath, (($lines -join "`r`n") + "`r`n"), (New-Object Text.UTF8Encoding($true)))
    return @{ Version = $version; Root = $buildRoot; Publish = $publish; Config = $configPath;
        Props = $propsPath; InstallerScript = $issPath; Template = $template; Output = $output }
}
