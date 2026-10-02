param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$identity = Get-Content -LiteralPath (Join-Path $root 'installer\store-identity.json') -Raw | ConvertFrom-Json
$version = $identity.version
$versionShort = $version -replace '\.0$', ''
$sdk = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Filter makeappx.exe -Recurse -File |
    Where-Object FullName -Match '\\x64\\' | Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $sdk) { throw 'MakeAppx não encontrado.' }
$publish = Join-Path $root "artifacts\store\publish-$versionShort"
$stage = Join-Path $root "artifacts\store\stage-$versionShort"
$output = Join-Path $root "artifacts\store\Strigoi-Companion-$versionShort-x64.msix"
dotnet publish (Join-Path $root 'src\Strigoi.Companion') -c $Configuration -r win-x64 --self-contained true -p:DebugType=none -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Falha na publicação do aplicativo.' }
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'app'),(Join-Path $stage 'Assets') | Out-Null
Get-ChildItem -LiteralPath $publish -Force | Copy-Item -Destination (Join-Path $stage 'app') -Recurse -Force
Add-Type -AssemblyName System.Drawing
$iconPath = Join-Path $root 'src\Strigoi.Companion\Assets\companion.ico'
$icon = [Drawing.Icon]::new($iconPath)
try {
    $sourceImage = $icon.ToBitmap()
    foreach ($size in @(50,44,150)) {
        $bitmap = [Drawing.Bitmap]::new($size,$size)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.DrawImage($sourceImage,0,0,$size,$size)
            $name = switch ($size) { 50 {'StoreLogo.png'} 44 {'Square44x44Logo.png'} 150 {'Square150x150Logo.png'} }
            $bitmap.Save((Join-Path $stage "Assets\$name"),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { if ($sourceImage) { $sourceImage.Dispose() }; $icon.Dispose() }
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap uap10 rescap">
  <Identity Name="$($identity.identityName)" Publisher="$($identity.publisher)" Version="$version" ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>Strigoi Companion</DisplayName>
    <PublisherDisplayName>$($identity.publisherDisplayName)</PublisherDisplayName>
    <Description>Familiar animado para acompanhar jogos</Description>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Resources><Resource Language="pt-BR" /></Resources>
  <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" /></Dependencies>
  <Applications>
    <Application Id="StrigoiCompanion" Executable="app\Strigoi.Companion.exe" uap10:RuntimeBehavior="packagedClassicApp" uap10:TrustLevel="mediumIL">
      <uap:VisualElements DisplayName="Strigoi Companion" Description="Familiar animado para acompanhar jogos" BackgroundColor="transparent" Square44x44Logo="Assets\Square44x44Logo.png" Square150x150Logo="Assets\Square150x150Logo.png" />
    </Application>
  </Applications>
  <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
</Package>
"@
[IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'),$manifest,[Text.UTF8Encoding]::new($false))
& $sdk pack /o /d $stage /p $output
if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar MSIX.' }
Get-Item -LiteralPath $output | Select-Object FullName,Length
