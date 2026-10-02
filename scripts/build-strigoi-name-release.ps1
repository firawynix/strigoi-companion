param(
    [string]$SourceStage = (Join-Path $PSScriptRoot '..\artifacts\store-suite\stage-game-x64'),
    [string]$Version = '0.8.1.0'
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifacts = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
$source = [IO.Path]::GetFullPath($SourceStage)
if (-not (Test-Path -LiteralPath (Join-Path $source 'AppxManifest.xml'))) { throw 'Pacote de origem não encontrado.' }
$output = [IO.Path]::GetFullPath((Join-Path $artifacts ('strigoi-name-release\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))))
if (-not $output.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Destino inválido.' }
$stage = Join-Path $output 'stage'
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $stage -Recurse -Force

$manifestPath = Join-Path $stage 'AppxManifest.xml'
[xml]$manifest = Get-Content -LiteralPath $manifestPath
$ns = [Xml.XmlNamespaceManager]::new($manifest.NameTable)
$ns.AddNamespace('a','http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$ns.AddNamespace('uap','http://schemas.microsoft.com/appx/manifest/uap/windows10')
$identity = $manifest.SelectSingleNode('/a:Package/a:Identity',$ns)
$display = $manifest.SelectSingleNode('/a:Package/a:Properties/a:DisplayName',$ns)
$visual = $manifest.SelectSingleNode('/a:Package/a:Applications/a:Application/uap:VisualElements',$ns)
if ($identity.Name -ne 'Firawynix.StrigoiCompanion' -or $display.InnerText -ne 'Strigoi Companion Assistant' -or -not $visual) { throw 'Manifesto de origem inesperado.' }
$identity.SetAttribute('Version',$Version)
$display.InnerText = 'Strigoi Companion'
$visual.SetAttribute('DisplayName','Strigoi Companion')
$manifest.Save($manifestPath)

$makeAppx = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Filter makeappx.exe -Recurse -File |
    Where-Object FullName -Match '\\x64\\' | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $makeAppx) { throw 'MakeAppx não encontrado.' }
$package = Join-Path $output 'Strigoi-Companion-0.8.1-x64.msix'
& $makeAppx pack /o /d $stage /p $package | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Falha ao empacotar.' }
Get-FileHash -LiteralPath $package -Algorithm SHA256 | Select-Object Path,Hash
