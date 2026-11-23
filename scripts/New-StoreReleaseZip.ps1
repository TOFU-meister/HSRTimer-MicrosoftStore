[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $BepInExDir,

    [string] $OutputDirectory = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') {
    throw 'Run this script on Windows with references from the installed Store game.'
}

$RepoRoot = Split-Path -Parent $PSScriptRoot
$BepInExDir = (Resolve-Path -LiteralPath $BepInExDir).Path
$OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$Project = Join-Path $RepoRoot 'src/HSRTimer/HSRTimer.csproj'
[xml] $ProjectXml = Get-Content -LiteralPath $Project -Raw
$VersionNode = $ProjectXml.SelectSingleNode("/Project/PropertyGroup[TargetFramework='net6.0']/Version")
$Version = $VersionNode.InnerText
if ($Version -cnotmatch '^0\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)-pre$') {
    throw 'The Store project version must use 0.x.x-pre.'
}
$ZipName = "HSRTimer-MicrosoftStore-v$Version.zip"
$FinalZip = Join-Path $OutputDirectory $ZipName
if (Test-Path -LiteralPath $FinalZip) {
    throw "The output ZIP already exists: $FinalZip"
}

$TempRoot = Join-Path ([IO.Path]::GetTempPath()) ('HSRTimer-release-' + [Guid]::NewGuid().ToString('N'))
$BuildDir = Join-Path $TempRoot 'build'
$PackageDir = Join-Path $TempRoot 'package'
$TempZip = Join-Path $TempRoot $ZipName
try {
    New-Item -ItemType Directory -Path $BuildDir, $PackageDir | Out-Null
    & dotnet build $Project -c Release -t:Rebuild -o $BuildDir '-p:StoreIl2Cpp=true' '-p:StorePreview=false' "-p:BepInExDir=$BepInExDir"
    if ($LASTEXITCODE -ne 0) { throw 'The Store Release build failed.' }

    $Dll = Join-Path $BuildDir 'HSRTimer.dll'
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($Dll).ProductVersion -cne $Version) {
        throw "The built DLL does not report Store release version $Version."
    }

    Copy-Item -LiteralPath $Dll -Destination $PackageDir
    Copy-Item -LiteralPath (Join-Path $RepoRoot 'LICENSE') -Destination $PackageDir
    $Readme = [IO.File]::ReadAllText((Join-Path $RepoRoot 'docs/releases/README-store-template.txt'))
    [IO.File]::WriteAllText((Join-Path $PackageDir 'README.txt'), $Readme.Replace('{{VERSION}}', $Version))

    $Expected = @('HSRTimer.dll', 'LICENSE', 'README.txt')
    $Files = @($Expected | ForEach-Object { Join-Path $PackageDir $_ })
    Compress-Archive -LiteralPath $Files -DestinationPath $TempZip

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $Archive = [IO.Compression.ZipFile]::OpenRead($TempZip)
    try {
        if ($Archive.Entries.Count -ne $Expected.Count -or
            (Compare-Object -ReferenceObject $Expected -DifferenceObject @($Archive.Entries.FullName) -CaseSensitive) -or
            @($Archive.Entries | Where-Object Length -le 0).Count -gt 0) {
            throw 'The ZIP must contain exactly HSRTimer.dll, LICENSE, and README.txt at its root.'
        }
    }
    finally { $Archive.Dispose() }

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    [IO.File]::Move($TempZip, $FinalZip)
    Get-FileHash -LiteralPath $FinalZip -Algorithm SHA256
}
finally {
    if (Test-Path -LiteralPath $TempRoot) {
        Remove-Item -LiteralPath $TempRoot -Recurse -Force
    }
}
