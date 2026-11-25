[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$BundleRunDirectory,
    [Parameter(Mandatory)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-SafeItem([string]$Path, [bool]$Directory) {
    $item = Get-Item -LiteralPath $Path -Force
    if ($item.PSProvider.Name -ne 'FileSystem' -or $item.PSIsContainer -ne $Directory) {
        throw "Unexpected item type: $Path"
    }
    $current = $item
    while ($null -ne $current) {
        if ($current.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Links and reparse points are not allowed: $($current.FullName)"
        }
        $current = if ($current -is [IO.FileInfo]) { $current.Directory } else { $current.Parent }
    }
    return $item
}

$repo = Split-Path $PSScriptRoot -Parent
$pack = Join-Path $repo 'tools/StoreFontPack'
$run = Get-SafeItem $BundleRunDirectory $true
$output = Get-SafeItem $OutputDirectory $true
$destination = Join-Path $output.FullName 'HSRTimer-MicrosoftStore-FontPack-v1.0.zip'
if (Test-Path -LiteralPath $destination) { throw "Output already exists: $destination" }

$verified = @(Import-Csv -LiteralPath (Join-Path $pack 'verified-bundles.csv'))
$report = Get-SafeItem (Join-Path $run.FullName 'bundle-hashes.csv') $false
$reported = @(Import-Csv -LiteralPath $report.FullName)
$fonts = Get-Content -LiteralPath (Join-Path $repo 'tools/StoreFontBundleProject/test-fonts.json') -Raw | ConvertFrom-Json
if ($verified.Count -ne 5 -or $reported.Count -ne 5 -or $fonts.Count -ne 5) {
    throw 'Expected five verified, reported and source entries.'
}

$payload = [ordered]@{}
$hashes = [ordered]@{}
$sources = foreach ($font in $fonts) {
    $name = [IO.Path]::GetFileNameWithoutExtension($font.file).ToLowerInvariant() + '-embedded.bundle'
    $expected = @($verified | Where-Object File -CEQ $name)
    $actual = @($reported | Where-Object File -CEQ $name)
    if ($expected.Count -ne 1 -or $actual.Count -ne 1 -or $expected[0].Group -cne $font.group -or
        $actual[0].Group -cne $font.group -or $actual[0].Bytes -cne $expected[0].Bytes -or
        $expected[0].SHA256 -notmatch '^[0-9a-f]{64}$' -or $actual[0].SHA256 -ne $expected[0].SHA256) {
        throw "Bundle report does not match the verified build: $name"
    }
    $group = Get-SafeItem (Join-Path $run.FullName ("group-" + $font.group)) $true
    $inputFile = Get-SafeItem (Join-Path $group.FullName $name) $false
    if ($inputFile.Length -ne [long]$expected[0].Bytes -or
        (Get-FileHash -LiteralPath $inputFile.FullName -Algorithm SHA256).Hash -ne $expected[0].SHA256) {
        throw "Bundle size or SHA256 mismatch: $name"
    }
    $entry = "fonts/$name"
    $payload.Add($entry, $inputFile.FullName)
    $hashes.Add($entry, $expected[0].SHA256.ToUpperInvariant())

    [pscustomobject]@{
        Bundle = $name
        BundleBytes = $expected[0].Bytes
        BundleSHA256 = $expected[0].SHA256
        SourceFile = $font.file
        SourceVersion = $font.version
        SourceURL = $font.url
        SourceSHA256 = $font.sha256
        License = $font.license
        LicenseFile = 'licenses/' + $font.licenseFile
        LicenseURL = $font.licenseUrl
    }
}

foreach ($groupName in 'a', 'b') {
    $group = Get-SafeItem (Join-Path $run.FullName "group-$groupName") $true
    $allowed = @($verified | Where-Object Group -CEQ $groupName | ForEach-Object File)
    foreach ($item in Get-ChildItem -LiteralPath $group.FullName -Force) {
        if ($item.PSIsContainer -or $allowed -cnotcontains $item.Name -or
            ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Unexpected group member: $($item.Name)"
        }
    }
}

$licenseHashes = @{
    'licenses/Noto-OFL.txt' = '6a73f9541c2de74158c0e7cf6b0a58ef774f5a780bf191f2d7ec9cc53efe2bf2'
    'licenses/Roboto-LICENSE.txt' = 'c71d239df91726fc519c6eb72d318ec65820627232b2f796219e87dcf35d0ab4'
    'licenses/RobotoMono-OFL.txt' = '50ab8dd54680d3473f649c9db86fece88434d097c7834475c1c72d2f8c429215'
}
foreach ($name in 'README.ja.txt', 'README.en.txt', 'README.zh-Hans.txt', 'licenses/ATTRIBUTION.txt',
    'licenses/Noto-OFL.txt', 'licenses/Roboto-LICENSE.txt', 'licenses/RobotoMono-OFL.txt') {
    $file = Get-SafeItem (Join-Path $pack $name) $false
    if ($file.Length -eq 0) { throw "Required document is empty: $name" }
    $payload.Add($name, $file.FullName)
    $digest = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    if ($licenseHashes.ContainsKey($name) -and $digest -ne $licenseHashes[$name]) {
        throw "License SHA256 mismatch: $name"
    }
    $hashes.Add($name, $digest)
}

$tempCreated = $false
$temp = Join-Path $output.FullName ('.fontpack-' + [guid]::NewGuid().ToString('N') + '.tmp')
try {
    $fileStream = [IO.File]::Open($temp, [IO.FileMode]::CreateNew)
    $tempCreated = $true
    try {
        $zip = [IO.Compression.ZipArchive]::new($fileStream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($entry in $payload.Keys) {
                $source = Get-SafeItem $payload[$entry] $false
                [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $source.FullName, $entry)
            }
            $newline = [string][char]10
            $texts = [ordered]@{ 'FONT-SOURCES.csv' = (($sources | ConvertTo-Csv -NoTypeInformation) -join $newline) + $newline }
            $sha = [Security.Cryptography.SHA256]::Create()
            try {
                $sourceBytes = [Text.Encoding]::UTF8.GetBytes($texts['FONT-SOURCES.csv'])
                $hashes.Add('FONT-SOURCES.csv', [BitConverter]::ToString($sha.ComputeHash($sourceBytes)).Replace('-', ''))
                $lines = foreach ($name in $hashes.Keys) { "$($hashes[$name])  $name" }
                $texts.Add('SHA256SUMS.txt', ($lines -join $newline) + $newline)
                $checksumBytes = [Text.Encoding]::UTF8.GetBytes($texts['SHA256SUMS.txt'])
                $hashes.Add('SHA256SUMS.txt', [BitConverter]::ToString($sha.ComputeHash($checksumBytes)).Replace('-', ''))
            }
            finally { $sha.Dispose() }
            foreach ($name in $texts.Keys) {
                $stream = $zip.CreateEntry($name).Open()
                try {
                    $bytes = [Text.Encoding]::UTF8.GetBytes($texts[$name])
                    $stream.Write($bytes, 0, $bytes.Length)
                }
                finally { $stream.Dispose() }
            }
        }
        finally { $zip.Dispose() }
    }
    finally { $fileStream.Dispose() }

    $zip = [IO.Compression.ZipFile]::OpenRead($temp)
    try {
        $seen = @{}
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName
            if ($hashes.Keys -cnotcontains $name -or $seen.ContainsKey($name)) {
                throw "Unexpected or duplicate ZIP member: $name"
            }
            $seen[$name] = $true
            $stream = $entry.Open()
            try {
                if ((Get-FileHash -InputStream $stream -Algorithm SHA256).Hash -ne $hashes[$name]) {
                    throw "ZIP member SHA256 mismatch: $name"
                }
            }
            finally { $stream.Dispose() }
        }
        if ($seen.Count -ne $hashes.Count) { throw 'ZIP members are missing.' }
    }
    finally { $zip.Dispose() }

    # This overload fails if another process created the destination.
    [IO.File]::Move($temp, $destination)
    Get-FileHash -LiteralPath $destination -Algorithm SHA256
}
finally {
    if ($tempCreated -and [IO.File]::Exists($temp)) { [IO.File]::Delete($temp) }
}
