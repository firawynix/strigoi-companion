param(
    [ValidateSet('work','game','both')][string]$Kind = 'work',
    [string]$WorkRoot = (Join-Path $PSScriptRoot '..\..\firaw-work-assistant')
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workRoot = [IO.Path]::GetFullPath($WorkRoot)
$config = Get-Content -LiteralPath (Join-Path $root 'installer\fusion-identities.json') -Raw | ConvertFrom-Json
$publisher = [string]$config.publisher
$publisherName = [string]$config.publisherDisplayName
$artifacts = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
$runName = Get-Date -Format 'yyyyMMdd-HHmmss'
$out = [IO.Path]::GetFullPath((Join-Path $artifacts "fusion-store\$runName"))
if (-not $out.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Diretório de saída inválido.' }
New-Item -ItemType Directory -Path $out -Force | Out-Null
$gameProject = Join-Path $root 'src\Strigoi.Companion\Strigoi.Companion.csproj'
$workProject = Join-Path $workRoot 'Firaw.WorkAssistant.csproj'
if (-not (Test-Path -LiteralPath $gameProject) -or -not (Test-Path -LiteralPath $workProject)) { throw 'Os dois projetos devem estar disponíveis.' }
$sdk = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Filter makeappx.exe -Recurse -File |
    Where-Object FullName -Match '\\x64\\' | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $sdk) { throw 'MakeAppx não encontrado.' }
Add-Type -AssemblyName System.Drawing

function New-Logos([string]$kind, [string]$assets) {
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

function New-Package([string]$kind, [string]$arch) {
    $identity = if ($kind -eq 'game') { $config.game } else { $config.work }
    if ([string]::IsNullOrWhiteSpace([string]$identity.identityName)) { throw "A identidade da Store para $kind ainda não foi reservada." }
    $project = if ($kind -eq 'game') { $gameProject } else { $workProject }
    $switch = if ($kind -eq 'game') { '-p:GameFusion=true' } else { '-p:WorkFusion=true' }
    $publish = Join-Path $out "publish-$kind-$arch"
    & dotnet publish $project -c Release -r "win-$arch" --self-contained true -p:DebugType=none $switch -o $publish | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar $kind $arch." }
    $stage = Join-Path $out "stage-$kind-$arch"
    $app = Join-Path $stage 'app'
    $assets = Join-Path $stage 'Assets'
    New-Item -ItemType Directory -Force -Path $app,$assets | Out-Null
    Copy-Item -Path (Join-Path $publish '*') -Destination $app -Recurse -Force
    New-Logos $kind $assets
    $display = [string]$identity.displayName
    $description = if ($kind -eq 'game') { 'Familiar para jogos com missões, notas, checklists e memória local' } else { 'Assistente de trabalho com captura local, memória e perguntas sobre janelas escolhidas' }
    $appId = if ($kind -eq 'game') { 'StrigoiCompanionAssistant' } else { 'FirawWorkCompanionAssistant' }
    $executable = if ($kind -eq 'game') { 'Strigoi.Companion.exe' } else { 'Firaw.WorkAssistant.exe' }
    $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities" IgnorableNamespaces="uap uap10 rescap">
  <Identity Name="$($identity.identityName)" Publisher="$publisher" Version="1.0.0.0" ProcessorArchitecture="$arch" />
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
    $filename = if ($kind -eq 'game') { "Strigoi-Assistant-Companion-1.0.0-$arch.msix" } else { "Firaw-Work-Companion-Assistant-1.0.0-$arch.msix" }
    $package = Join-Path $out $filename
    & $sdk pack /o /d $stage /p $package | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Falha no pacote $filename." }
    $hash = Get-FileHash -LiteralPath $package -Algorithm SHA256
    Write-Host "$($hash.Path) SHA256 $($hash.Hash)"
    return $package
}

$kinds = if ($Kind -eq 'both') { @('game','work') } else { @($Kind) }
foreach ($current in $kinds) {
    $packages = @()
    foreach ($arch in @('x64','x86')) { $packages += New-Package $current $arch }
    $bundleDir = Join-Path $out "bundle-$current"
    New-Item -ItemType Directory -Path $bundleDir | Out-Null
    Copy-Item -LiteralPath $packages -Destination $bundleDir
    $bundleName = if ($current -eq 'game') { 'Strigoi-Assistant-Companion-1.0.0.msixbundle' } else { 'Firaw-Work-Companion-Assistant-1.0.0.msixbundle' }
    $bundle = Join-Path $out $bundleName
    & $sdk bundle /o /bv '1.0.0.0' /d $bundleDir /p $bundle
    if ($LASTEXITCODE -ne 0) { throw "Falha no bundle $bundleName." }
    Get-FileHash -LiteralPath $bundle -Algorithm SHA256 | Select-Object Path,Hash
}
Write-Output "Saída: $out"
