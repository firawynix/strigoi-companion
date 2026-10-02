param(
    [Parameter(Mandatory = $true)][string]$MakeNsis,
    [string]$Version = '0.7.5'
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$payload = Join-Path $projectRoot "artifacts\releases\Strigoi-Companion-$Version"
$installerDirectory = Join-Path $projectRoot 'artifacts\installers'
New-Item -ItemType Directory -Force -Path $installerDirectory | Out-Null
Push-Location $projectRoot
try {
    [xml]$project = Get-Content (Join-Path $projectRoot 'src\Strigoi.Companion\Strigoi.Companion.csproj')
    if ($project.Project.PropertyGroup.Version -ne $Version) { throw 'Project and installer versions differ.' }
    & dotnet publish src\Strigoi.Companion -c Release -r win-x64 --self-contained true -o $payload
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'installer\LEIA-ME.txt') -Destination (Join-Path $payload 'LEIA-ME.txt')
    $removalList = Join-Path $installerDirectory 'uninstall-files.nsh'
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $payload -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath($payload, $file.FullName)
        if ($relative.StartsWith('..') -or $relative.Contains('$') -or $relative.Contains('"')) { throw "Unsafe package name: $relative" }
        $lines.Add('Delete "$INSTDIR\' + $relative + '"')
    }
    foreach ($folder in Get-ChildItem -LiteralPath $payload -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending) {
        $relative = [IO.Path]::GetRelativePath($payload, $folder.FullName)
        $lines.Add('RMDir "$INSTDIR\' + $relative + '"')
    }
    [IO.File]::WriteAllLines($removalList, $lines, [Text.UTF8Encoding]::new($true))
    $output = Join-Path $installerDirectory "Strigoi-Companion-Setup-$Version.exe"
    $icon = Join-Path $projectRoot 'src\Strigoi.Companion\Assets\companion.ico'
    & $MakeNsis /V3 "/DVERSION=$Version" "/DPAYLOAD=$payload" "/DOUTPUT=$output" "/DUNINSTALL_FILES=$removalList" "/DAPPICON=$icon" (Join-Path $projectRoot 'installer\companion.nsi')
    if ($LASTEXITCODE -ne 0) { throw 'NSIS compilation failed.' }
    Get-FileHash -LiteralPath $output -Algorithm SHA256 | Format-List Path,Hash
}
finally { Pop-Location }
