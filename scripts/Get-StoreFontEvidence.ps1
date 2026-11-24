[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $GameDir,
    [string] $OutputPath = 'hsrtimer-font-evidence.txt'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $GameDir).ProviderPath
$destination = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
if (-not $destination.EndsWith('.txt', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The report output path must end in .txt.'
}
$report = [System.Collections.Generic.List[string]]::new()
$report.Add('HSRTimer Store font evidence (no game files modified)')
$files = @(
    'GameAssembly.dll',
    'UnityPlayer.dll',
    'BepInEx/interop/UnityEngine.TextRenderingModule.dll',
    'BepInEx/interop/UnityEngine.CoreModule.dll',
    'BepInEx/dummy/UnityEngine.TextRenderingModule.dll',
    'BepInEx/core/BepInEx.Unity.IL2CPP.dll',
    'BepInEx/core/Il2CppInterop.Runtime.dll',
    'BepInEx/core/Il2CppInterop.Generator.dll',
    'BepInEx/core/Cpp2IL.Core.dll',
    'BepInEx/core/LibCpp2IL.dll'
)
$metadataPaths = @(Get-ChildItem -LiteralPath $root -Directory -Filter '*_Data' |
    ForEach-Object { Join-Path $_.Name 'il2cpp_data/Metadata/global-metadata.dat' })
if ($metadataPaths.Count -eq 0) {
    $report.Add('Game *_Data directory missing. Pass the folder containing the game executable.')
}
foreach ($relative in ($files + $metadataPaths)) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $report.Add("$relative missing")
        continue
    }

    $item = Get-Item -LiteralPath $path
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    $report.Add("$relative size=$($item.Length) SHA256=$hash")
    if ($relative.EndsWith('global-metadata.dat')) {
        $reader = [System.IO.BinaryReader]::new([System.IO.File]::OpenRead($path))
        try {
            if ($reader.BaseStream.Length -ge 8) {
                $signature = $reader.ReadUInt32()
                $version = $reader.ReadInt32()
                $report.Add(('  header magic=0x{0:X8}, version field={1}' -f $signature, $version))
            }
        }
        finally { $reader.Dispose() }
    }
    elseif ($relative -like 'BepInEx/*') {
        try {
            $name = [System.Reflection.AssemblyName]::GetAssemblyName($path)
            $report.Add("  assembly version=$($name.Version), product version=$($item.VersionInfo.ProductVersion)")
        }
        catch { $report.Add('  CLR assembly identity unavailable') }
    }
    else {
        $report.Add("  file version=$($item.VersionInfo.FileVersion), product version=$($item.VersionInfo.ProductVersion)")
    }
}

$log = Join-Path $root 'BepInEx/LogOutput.log'
if (Test-Path -LiteralPath $log -PathType Leaf) {
    $report.Add('Selected log lines (review before sharing)')
    Select-String -LiteralPath $log -Pattern 'BepInEx [0-9]|Unity version|Cpp2IL|LibCpp2IL|InteropManager|\[Font Diagnostics\]' |
        Select-Object -Last 160 |
        ForEach-Object { $report.Add($_.Line.Replace($root, '<game>')) }
}

$report | Set-Content -LiteralPath $destination -Encoding UTF8
Write-Output $destination
