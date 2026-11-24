[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$OutputDirectory,
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\2019.4.24f1\Editor\Unity.exe',
    [ValidateSet('embedded', 'embedded-raw')]
    [string]$Mode = 'embedded'
)

$ErrorActionPreference = 'Stop'
$source = Join-Path (Split-Path $PSScriptRoot) 'tools\StoreFontBundleProject'
# Windows PowerShell 5.1 emits the JSON array as one pipeline object.
# Assignment preserves its elements without adding another array around it.
$fonts = Get-Content -LiteralPath (Join-Path $source 'test-fonts.json') -Raw | ConvertFrom-Json
if ($fonts -isnot [array] -or $fonts.Count -ne 5) {
    throw 'Expected exactly five font definitions in test-fonts.json.'
}
foreach ($font in $fonts) {
    if ($font.file -isnot [string] -or [string]::IsNullOrWhiteSpace($font.file)) {
        throw 'Each font definition must have a non-empty string file property.'
    }
}
$stems = @{}
foreach ($font in $fonts) {
    $stem = [IO.Path]::GetFileNameWithoutExtension($font.file).ToLowerInvariant()
    if ($stems.ContainsKey($stem)) { throw "Duplicate font bundle name: $stem" }
    $stems[$stem] = $true
}
$UnityEditor = (Resolve-Path -LiteralPath $UnityEditor).Path
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw "Choose a new output directory: $output" }

$projects = Join-Path $output 'projects'
$logs = Join-Path $output 'logs'
$licenses = Join-Path $output 'licenses'
$build = Join-Path $output 'build'
New-Item -ItemType Directory -Path $projects, $logs, $licenses, $build | Out-Null

function New-FontProject([string]$Project) {
    $assets = Join-Path $Project 'Assets'
    New-Item -ItemType Directory -Path $assets | Out-Null
    Copy-Item -Recurse -LiteralPath (Join-Path $source 'Assets\Editor') -Destination $assets
    foreach ($directory in 'Packages', 'ProjectSettings') {
        New-Item -ItemType Directory -Path (Join-Path $Project $directory) | Out-Null
    }
    foreach ($file in 'Packages\manifest.json', 'ProjectSettings\ProjectVersion.txt') {
        Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $Project $file)
    }
}

function Invoke-FontUnity([string]$Project, [string]$Method, [string]$BundleDirectory, [string]$Log) {
    $arguments = @(
        '-batchmode', '-quit', '-buildTarget', 'Win64',
        '-projectPath', ('"{0}"' -f $Project),
        '-executeMethod', $Method,
        '-fontMode', $Mode,
        '-fontOutput', ('"{0}"' -f $BundleDirectory),
        '-logFile', ('"{0}"' -f $Log)
    )
    Write-Host "Unity log: $Log"
    $editor = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -Wait -PassThru
    if ($editor.ExitCode -ne 0) { throw "Unity exited with $($editor.ExitCode). See $Log" }
    if (-not (Test-Path -LiteralPath $Log -PathType Leaf)) { throw "Missing Unity log: $Log" }
}

$bundleNames = @{}
$index = 0
foreach ($font in $fonts) {
    $index++
    Write-Host ("[{0}/{1}] Starting standalone build: {2}" -f $index, $fonts.Count, $font.file)
    $stem = [IO.Path]::GetFileNameWithoutExtension($font.file).ToLowerInvariant()
    $project = Join-Path $projects $stem
    New-FontProject $project
    $fontFolder = Join-Path $project 'Assets\Fonts'
    New-Item -ItemType Directory -Path $fontFolder | Out-Null
    $fontPath = Join-Path $fontFolder $font.file
    Invoke-WebRequest -UseBasicParsing -Uri $font.url -OutFile $fontPath
    if ((Get-Item -LiteralPath $fontPath).Length -ne $font.bytes -or
        (Get-FileHash -LiteralPath $fontPath -Algorithm SHA256).Hash -ne $font.sha256) {
        throw "Font failed size or SHA256 validation: $($font.file)"
    }

    $licensePath = Join-Path $licenses $font.licenseFile
    if (-not (Test-Path -LiteralPath $licensePath)) {
        Invoke-WebRequest -UseBasicParsing -Uri $font.licenseUrl -OutFile $licensePath
    }

    $name = "$stem-$Mode.bundle"
    $fontBuild = Join-Path $project 'Build'
    $log = Join-Path $logs "$stem.log"
    Invoke-FontUnity $project 'StoreFontBundleBuilder.BuildIsolatedFont' $fontBuild $log
    if (-not (Select-String -LiteralPath $log -Pattern "Editor verified standalone: $name" -SimpleMatch -Quiet)) {
        throw "Unity did not verify the standalone bundle: $name. See $log"
    }
    $path = Join-Path $fontBuild $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing bundle: $path. See $log" }
    $bundleNames[$font.file] = (Copy-Item -LiteralPath $path -Destination $build -PassThru).FullName
    Copy-Item -LiteralPath "$path.manifest" -Destination $build
    Write-Host ("[{0}/{1}] Standalone build verified: {2}" -f $index, $fonts.Count, $name)
}

Write-Host "Starting concurrent Editor verification for $($fonts.Count) bundles."
$verificationProject = Join-Path $output 'verify'
New-FontProject $verificationProject
$log = Join-Path $output 'Unity.log'
Invoke-FontUnity $verificationProject 'StoreFontBundleBuilder.VerifyIsolatedFonts' $build $log
$expectedVerification = "Editor verified $($fonts.Count) isolated bundles open together."
if (-not (Select-String -LiteralPath $log -Pattern $expectedVerification -SimpleMatch -Quiet)) {
    throw "Unity did not record successful bundle verification. See $log"
}

Write-Host "Concurrent Editor verification passed. Collecting bundles and CSV reports."
$result = foreach ($font in $fonts) {
    $group = Join-Path $output ("group-" + $font.group)
    if (-not (Test-Path -LiteralPath $group)) { New-Item -ItemType Directory -Path $group | Out-Null }
    $bundle = Copy-Item -LiteralPath $bundleNames[$font.file] -Destination $group -PassThru
    [pscustomobject]@{
        Group = $font.group
        File = $bundle.Name
        Bytes = $bundle.Length
        SHA256 = (Get-FileHash -LiteralPath $bundle.FullName -Algorithm SHA256).Hash
    }
}
$result | Export-Csv -LiteralPath (Join-Path $output 'bundle-hashes.csv') -NoTypeInformation -Encoding UTF8
$result

# Prior compressed output sizes from the stage 9 Windows run. Raw output has no comparable baseline.
$previousBytes = @{
    'notosanscjkjp-regular-embedded.bundle' = 30127272
    'notosanscjkjp-bold-embedded.bundle' = 30127291
    'notosanscjksc-regular-embedded.bundle' = 14732889
    'roboto-regular-embedded.bundle' = 302793
    'robotomono-regular-embedded.bundle' = 84507
}
$comparison = foreach ($font in $fonts) {
    $name = [IO.Path]::GetFileName($bundleNames[$font.file])
    $item = $result | Where-Object File -eq $name
    $previous = $previousBytes[$name]
    $saved = if ($previous) { $previous - $item.Bytes }
    [pscustomobject]@{
        Font = $font.file
        SourceBytes = $font.bytes
        PreviousBytes = $previous
        NewBytes = $item.Bytes
        SavedBytes = $saved
        SavedPercent = $(if ($previous) { [math]::Round(100 * $saved / $previous, 2) })
        SHA256 = $item.SHA256
        FontCount = 1
        DependencyCount = 0
        StandaloneEditor = 'Passed'
        ConcurrentEditor = 'Passed'
    }
}
$comparison | Export-Csv -LiteralPath (Join-Path $output 'bundle-comparison.csv') -NoTypeInformation -Encoding UTF8
Write-Host "Completed $($result.Count) bundles. Reports: $output"
