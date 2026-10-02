param([string]$WorkAssistantRoot = (Join-Path $PSScriptRoot '..\..\firaw-work-assistant'))

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workRoot = [IO.Path]::GetFullPath($WorkAssistantRoot)
$out = [IO.Path]::GetFullPath((Join-Path $root 'artifacts\store-suite'))
$artifacts = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
if (-not $out.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Diretório de saída inválido.' }
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null

$gameProject = Join-Path $root 'src\Strigoi.Companion\Strigoi.Companion.csproj'
$workProject = Join-Path $workRoot 'Firaw.WorkAssistant.csproj'
if (-not (Test-Path -LiteralPath $workProject)) { throw 'Projeto Firaw não encontrado.' }
[xml]$gameXml = Get-Content -LiteralPath $gameProject
[xml]$workXml = Get-Content -LiteralPath $workProject
$gameVersion = [string]$gameXml.Project.PropertyGroup.Version
$workVersion = [string]$workXml.Project.PropertyGroup.Version
$gameIdentity = Get-Content -LiteralPath (Join-Path $root 'installer\store-identity.json') -Raw | ConvertFrom-Json
$publisher = [string]$gameIdentity.publisher
$publisherName = [string]$gameIdentity.publisherDisplayName

$sdk = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Filter makeappx.exe -Recurse -File |
    Where-Object FullName -Match '\\x64\\' | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $sdk) { throw 'MakeAppx não encontrado.' }

foreach ($arch in @('x64','x86')) {
    foreach ($entry in @(@('game',$gameProject),@('work',$workProject))) {
        $name = $entry[0]
        $destination = Join-Path $out "publish-$name-$arch"
        & dotnet publish $entry[1] -c Release -r "win-$arch" --self-contained true -p:DebugType=none -o $destination
        if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar $name $arch." }
    }
}

Add-Type -AssemblyName System.Drawing
function New-Logos([string]$kind,[string]$assets) {
    $source = if ($kind -eq 'game') {
        $icon = [Drawing.Icon]::new((Join-Path $root 'src\Strigoi.Companion\Assets\companion.ico'))
        try { $icon.ToBitmap() } finally { $icon.Dispose() }
    } else {
        [Drawing.Image]::FromFile((Join-Path $workRoot 'assets\huginn-muninn.png'))
    }
    try {
        foreach ($size in @(50,44,150)) {
            $bitmap = [Drawing.Bitmap]::new($size,$size)
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.DrawImage($source,0,0,$size,$size)
                $name = switch ($size) { 50 {'StoreLogo.png'} 44 {'Square44x44Logo.png'} 150 {'Square150x150Logo.png'} }
                $bitmap.Save((Join-Path $assets $name),[Drawing.Imaging.ImageFormat]::Png)
            } finally { $graphics.Dispose(); $bitmap.Dispose() }
        }
    } finally { $source.Dispose() }
}

function New-Package([string]$kind,[string]$arch) {
    $stage = Join-Path $out "stage-$kind-$arch"
    $app = Join-Path $stage 'app'
    $assets = Join-Path $stage 'Assets'
    New-Item -ItemType Directory -Force -Path $app,$assets | Out-Null
    $primary = Join-Path $out "publish-$kind-$arch"
    $other = if ($kind -eq 'game') {'work'} else {'game'}
    $module = if ($kind -eq 'game') {'WorkAssistant'} else {'GameCompanion'}
    Copy-Item -Path (Join-Path $primary '*') -Destination $app -Recurse -Force
    $moduleDir = Join-Path $app $module
    New-Item -ItemType Directory -Path $moduleDir | Out-Null
    Copy-Item -Path (Join-Path $out "publish-$other-$arch\*") -Destination $moduleDir -Recurse -Force
    New-Logos $kind $assets

    if ($kind -eq 'game') {
        $identity = [string]$gameIdentity.identityName
        $version = "$gameVersion.0"
        $display = 'Strigoi Companion Assistant'
        $description = 'Familiar para jogos com assistente de trabalho integrado'
        $appId = 'StrigoiCompanion'
        $executable = 'Strigoi.Companion.exe'
        $filename = "Strigoi-Companion-Assistant-$gameVersion-$arch.msix"
    } else {
        $identity = 'Firawynix.FirawWorkAssistant'
        $version = "$workVersion.0"
        $display = 'Firaw Work Assistant'
        $description = 'Assistente de trabalho com Familiar para jogos integrado'
        $appId = 'WorkAssistant'
        $executable = 'Firaw.WorkAssistant.exe'
        $filename = "Firaw-Work-Assistant-$workVersion-$arch.msix"
    }
    $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap uap10 rescap">
  <Identity Name="$identity" Publisher="$publisher" Version="$version" ProcessorArchitecture="$arch" />
  <Properties>
    <DisplayName>$display</DisplayName>
    <PublisherDisplayName>$publisherName</PublisherDisplayName>
    <Description>$description</Description>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Resources><Resource Language="pt-BR" /></Resources>
  <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" /></Dependencies>
  <Applications>
    <Application Id="$appId" Executable="app\$executable" uap10:RuntimeBehavior="packagedClassicApp" uap10:TrustLevel="mediumIL">
      <uap:VisualElements DisplayName="$display" Description="$description" BackgroundColor="transparent" Square44x44Logo="Assets\Square44x44Logo.png" Square150x150Logo="Assets\Square150x150Logo.png" />
    </Application>
  </Applications>
  <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
</Package>
"@
    [IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'),$manifest,[Text.UTF8Encoding]::new($false))
    $package = Join-Path $out $filename
    & $sdk pack /o /d $stage /p $package
    if ($LASTEXITCODE -ne 0) { throw "Falha no pacote $filename." }
    Get-FileHash -LiteralPath $package -Algorithm SHA256 | Select-Object Path,Hash
}

New-Package game x64
New-Package work x64
New-Package work x86
$bundleDir = Join-Path $out 'work-bundle-input'
New-Item -ItemType Directory -Path $bundleDir | Out-Null
Copy-Item -LiteralPath (Join-Path $out "Firaw-Work-Assistant-$workVersion-x64.msix"),(Join-Path $out "Firaw-Work-Assistant-$workVersion-x86.msix") -Destination $bundleDir
$bundle = Join-Path $out "Firaw-Work-Assistant-$workVersion.msixbundle"
& $sdk bundle /o /bv "$workVersion.0" /d $bundleDir /p $bundle
if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar bundle Firaw.' }
Get-FileHash -LiteralPath $bundle -Algorithm SHA256 | Select-Object Path,Hash
