$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$originalScript = Join-Path $repo 'scripts/New-StoreFontPack.ps1'
$tokens = $null
$parseErrors = $null
$null = [System.Management.Automation.Language.Parser]::ParseFile($originalScript, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw ($parseErrors -join [Environment]::NewLine) }

$root = Join-Path ([IO.Path]::GetTempPath()) ('hsrtimer-pack-test-' + [guid]::NewGuid().ToString('N'))
try {
    $scripts = Join-Path $root 'scripts'
    $tools = Join-Path $root 'tools'
    $project = Join-Path $tools 'StoreFontBundleProject'
    $run = Join-Path $root 'bundle run'
    $out = Join-Path $root 'output'
    $pack = Join-Path $tools 'StoreFontPack'
    New-Item -ItemType Directory -Path $scripts, $project, $out | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $run 'group-a'), (Join-Path $run 'group-b') | Out-Null
    Copy-Item -LiteralPath $originalScript -Destination $scripts
    Copy-Item -LiteralPath (Join-Path $repo 'tools/StoreFontPack') -Destination $tools -Recurse
    Copy-Item -LiteralPath (Join-Path $repo 'tools/StoreFontBundleProject/test-fonts.json') -Destination $project
    $script = Join-Path $scripts 'New-StoreFontPack.ps1'
    $trusted = Join-Path $pack 'verified-bundles.csv'
    $report = Join-Path $run 'bundle-hashes.csv'
    $fixture = foreach ($entry in Import-Csv -LiteralPath $trusted) {
        $file = Join-Path (Join-Path $run ("group-" + $entry.Group)) $entry.File
        [IO.File]::WriteAllText($file, "Mock fixture: $($entry.File)")
        $entry.Bytes = (Get-Item -LiteralPath $file).Length
        $entry.SHA256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        $entry
    }
    $fixture | Export-Csv -LiteralPath $report -NoTypeInformation

    function Expect-Rejection([string]$Expected) {
        try {
            & $script -BundleRunDirectory $run -OutputDirectory $out | Out-Null
            throw 'Invalid pack inputs unexpectedly succeeded.'
        }
        catch {
            if ($_.Exception.Message -notlike "*$Expected*") { throw }
        }
    }

    Expect-Rejection 'does not match the verified build'
    # Only the fixture copy gets a trust manifest for mock bytes.
    $fixture | Export-Csv -LiteralPath $trusted -NoTypeInformation
    $result = & $script -BundleRunDirectory $run -OutputDirectory $out
    $zipPath = $result.Path
    $extract = Join-Path $root 'extracted'
    [IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $extract)
    $checks = @(Get-Content -LiteralPath (Join-Path $extract 'SHA256SUMS.txt'))
    if ($checks.Count -ne 13) { throw 'Unexpected checksum entry count.' }
    foreach ($check in $checks) {
        $hash, $relative = $check -split '  ', 2
        $file = Join-Path $extract $relative
        if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $hash) { throw "Bad checksum: $relative" }
    }
    if (@(Get-ChildItem -LiteralPath $extract -Recurse -File).Count -ne 14) { throw 'Unexpected ZIP contents.' }
    $sourceRows = @(Import-Csv -LiteralPath (Join-Path $extract 'FONT-SOURCES.csv'))
    if ($sourceRows.Count -ne 5) { throw 'Source metadata missing.' }
    Expect-Rejection 'Output already exists'
    Remove-Item -LiteralPath $zipPath

    $extra = Join-Path $run 'group-a/settings.ini'
    [IO.File]::WriteAllText($extra, 'must not ship')
    Expect-Rejection 'Unexpected group member'
    Remove-Item -LiteralPath $extra

    $first = Join-Path (Join-Path $run 'group-a') $fixture[0].File
    $old = [IO.File]::ReadAllBytes($first)
    [IO.File]::WriteAllBytes($first, [byte[]](1, 2, 3))
    Expect-Rejection 'Bundle size or SHA256 mismatch'
    Remove-Item -LiteralPath $first
    Expect-Rejection $fixture[0].File
    [IO.File]::WriteAllBytes($first, $old)

    $brokenReport = @($fixture[0], $fixture[0]) + $fixture[2..4]
    $brokenReport | Export-Csv -LiteralPath $report -NoTypeInformation
    Expect-Rejection 'does not match the verified build'
    $fixture | Export-Csv -LiteralPath $report -NoTypeInformation
    if ($PSVersionTable.PSEdition -eq 'Core' -and [Environment]::OSVersion.Platform -eq [PlatformID]::Unix) {
        $target = Join-Path $root 'linked.bundle'
        Move-Item -LiteralPath $first -Destination $target
        New-Item -ItemType SymbolicLink -Path $first -Target $target | Out-Null
        Expect-Rejection 'Links and reparse points are not allowed'
        Remove-Item -LiteralPath $first
        Move-Item -LiteralPath $target -Destination $first
    }
    $license = Join-Path $pack 'licenses/Roboto-LICENSE.txt'
    [IO.File]::WriteAllText($license, 'invalid license')
    Expect-Rejection 'License SHA256 mismatch'

    if (@(Get-ChildItem -LiteralPath $out -Force).Count -ne 0) { throw 'Failed packaging left an output.' }
    Write-Output "Font pack tests passed on PowerShell $($PSVersionTable.PSVersion)."
}
finally { if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force } }
