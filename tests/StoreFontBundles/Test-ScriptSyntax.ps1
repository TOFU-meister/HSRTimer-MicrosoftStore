$ErrorActionPreference = 'Stop'

$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$script = Join-Path $repo 'scripts/New-StoreFontTestBundles.ps1'
$tokens = $null
$parseErrors = $null
$null = [System.Management.Automation.Language.Parser]::ParseFile(
    $script, [ref]$tokens, [ref]$parseErrors
)
if ($parseErrors.Count -gt 0) {
    $details = $parseErrors | ForEach-Object {
        '{0}:{1}: {2} ({3})' -f $script, $_.Extent.StartLineNumber, $_.Message, $_.ErrorId
    }
    throw ($details -join [Environment]::NewLine)
}
Write-Output "Font bundle script syntax passed on PowerShell $($PSVersionTable.PSVersion)."
