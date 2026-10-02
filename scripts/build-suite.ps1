param(
    [string]$WorkAssistantRoot = (Join-Path $PSScriptRoot '..\..\firaw-work-assistant')
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workRoot = [IO.Path]::GetFullPath($WorkAssistantRoot)
$gameProject = Join-Path $root 'src\Strigoi.Companion\Strigoi.Companion.csproj'
$workProject = Join-Path $workRoot 'Firaw.WorkAssistant.csproj'
if (-not (Test-Path -LiteralPath $workProject)) { throw "Assistente de trabalho não encontrado: $workProject" }

[xml]$gameXml = Get-Content -LiteralPath $gameProject
[xml]$workXml = Get-Content -LiteralPath $workProject
$gameVersion = [string]$gameXml.Project.PropertyGroup.Version
$workVersion = [string]$workXml.Project.PropertyGroup.Version
$suiteRoot = [IO.Path]::GetFullPath((Join-Path $root "artifacts\suite\$gameVersion"))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
if (-not $suiteRoot.StartsWith($artifactsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Diretório de saída inválido.'
}
if (Test-Path -LiteralPath $suiteRoot) { Remove-Item -LiteralPath $suiteRoot -Recurse -Force }
New-Item -ItemType Directory -Path $suiteRoot | Out-Null

$gamePublish = Join-Path $suiteRoot 'build-game'
$workPublish = Join-Path $suiteRoot 'build-work'
& dotnet publish $gameProject -c Release -r win-x64 --self-contained true -p:DebugType=none -o $gamePublish
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar Strigoi.' }
& dotnet publish $workProject -c Release -r win-x64 --self-contained true -p:DebugType=none -o $workPublish
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar Firaw.' }

$gamePackage = Join-Path $suiteRoot 'Strigoi Companion Assistant'
$workPackage = Join-Path $suiteRoot 'Firaw Assistente de Trabalho'
New-Item -ItemType Directory -Path $gamePackage,$workPackage | Out-Null
Copy-Item -Path (Join-Path $gamePublish '*') -Destination $gamePackage -Recurse -Force
Copy-Item -Path (Join-Path $workPublish '*') -Destination $workPackage -Recurse -Force
$gameWorkModule = Join-Path $gamePackage 'WorkAssistant'
$workGameModule = Join-Path $workPackage 'GameCompanion'
New-Item -ItemType Directory -Path $gameWorkModule,$workGameModule | Out-Null
Copy-Item -Path (Join-Path $workPublish '*') -Destination $gameWorkModule -Recurse -Force
Copy-Item -Path (Join-Path $gamePublish '*') -Destination $workGameModule -Recurse -Force

@"
Strigoi Companion Assistant $gameVersion

Abra Strigoi.Companion.exe. O painel mantém o visual do Strigoi para jogos.
Use "Assistente de trabalho" para abrir tarefas, notas, checklists e os demais recursos do Firaw.
Os dois módulos estão incluídos. Os dados permanecem locais no perfil do Windows.
"@ | Set-Content -LiteralPath (Join-Path $gamePackage 'LEIA-ME.txt') -Encoding UTF8
@"
Firaw Assistente de Trabalho $workVersion

Abra Firaw.WorkAssistant.exe. A janela mantém o visual do Firaw para trabalho.
Use "Jogos" para abrir o Familiar e os demais recursos do Strigoi.
Os dois módulos estão incluídos. Os dados permanecem locais no perfil do Windows.
"@ | Set-Content -LiteralPath (Join-Path $workPackage 'LEIA-ME.txt') -Encoding UTF8

foreach ($package in @($gamePackage,$workPackage)) {
    $zip = "$package-x64.zip"
    Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal
    Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Select-Object Path,Hash
}
