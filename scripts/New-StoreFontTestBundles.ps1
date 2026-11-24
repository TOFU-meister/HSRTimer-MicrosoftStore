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
$UnityEditor = (Resolve-Path -LiteralPath $UnityEditor).Path
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw "Choose a new output directory: $output" }

$project = Join-Path $output 'UnityProject'
$fontFolder = Join-Path $project 'Assets\Fonts'
$licenses = Join-Path $output 'licenses'
$build = Join-Path $output 'build'
New-Item -ItemType Directory -Path $fontFolder, $licenses, $build | Out-Null
Copy-Item -Recurse -LiteralPath (Join-Path $source 'Assets\Editor') -Destination (Join-Path $project 'Assets')
foreach ($directory in 'Packages', 'ProjectSettings') {
    New-Item -ItemType Directory -Path (Join-Path $project $directory) | Out-Null
}
foreach ($file in 'Packages\manifest.json', 'ProjectSettings\ProjectVersion.txt') {
    Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $project $file)
}

foreach ($font in $fonts) {
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
}

$log = Join-Path $output 'Unity.log'
$arguments = @(
    '-batchmode', '-quit', '-buildTarget', 'Win64',
    '-projectPath', ('"{0}"' -f $project),
    '-executeMethod', 'StoreFontBundleBuilder.Build',
    '-fontMode', $Mode,
    '-fontOutput', ('"{0}"' -f $build),
    '-logFile', ('"{0}"' -f $log)
)
Write-Host "Unity log: $log"
$editor = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -Wait -PassThru
if ($editor.ExitCode -ne 0) { throw "Unity exited with $($editor.ExitCode). See $log" }

$expectedVerification = "Editor verified $($fonts.Count) bundles open together without dependencies."
if (-not (Select-String -LiteralPath $log -Pattern $expectedVerification -SimpleMatch -Quiet)) {
    throw "Unity did not record successful bundle verification. See $log"
}

$bundleNames = @{}
foreach ($font in $fonts) {
    $stem = [IO.Path]::GetFileNameWithoutExtension($font.file).ToLowerInvariant()
    $path = Join-Path $build "$stem-$Mode.bundle"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing bundle: $path. See $log" }
    $bundleNames[$font.file] = $path
}
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
