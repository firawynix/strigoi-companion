param([string]$Version = '0.6.1', [string]$PreviousVersion = '0.6.0')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$testRoot = [IO.Path]::GetFullPath((Join-Path $artifactRoot ('installer-qa-' + [Guid]::NewGuid().ToString('N'))))
$target = Join-Path $testRoot 'installed'
if (-not $testRoot.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Test path escaped artifacts.' }
$setup = Join-Path $artifactRoot "installers\Strigoi-Companion-Setup-$Version.exe"
$payload = Join-Path $artifactRoot "releases\Strigoi-Companion-$Version"
New-Item -ItemType Directory -Path $testRoot | Out-Null
$registry = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\StrigoiCompanion'
$registryBefore = if (Test-Path -LiteralPath $registry) { Get-ItemProperty -LiteralPath $registry | Select-Object * -ExcludeProperty PSPath,PSParentPath,PSChildName,PSDrive,PSProvider | ConvertTo-Json -Depth 4 -Compress } else { 'absent' }
# Read-only snapshots of the real profile: test installers must not touch it.
$profile = Join-Path $env:LOCALAPPDATA 'StrigoiCompanion'
function ProfileHashes {
    if (Test-Path -LiteralPath $profile) {
        Get-ChildItem -LiteralPath $profile -File -Recurse | Where-Object { $_.Extension -eq '.json' } | Sort-Object FullName | ForEach-Object { $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName).Hash }
    }
}
$profileBefore = @(ProfileHashes)
if ($PreviousVersion) {
    $previousSetup = Join-Path $artifactRoot "installers\Strigoi-Companion-Setup-$PreviousVersion.exe"
    $previousProcess = Start-Process -FilePath $previousSetup -ArgumentList "/S /TESTINSTALL /D=$target" -WindowStyle Hidden -PassThru
    if (-not $previousProcess.WaitForExit(60000) -or $previousProcess.ExitCode -ne 0) { throw 'Previous version installation failed.' }
    $previousInstalled = (Get-Item -LiteralPath (Join-Path $target 'Strigoi.Companion.exe')).VersionInfo.FileVersion
    if ($previousInstalled -ne "$PreviousVersion.0") { throw "Unexpected baseline: $previousInstalled" }
}
New-Item -ItemType Directory -Force -Path $target | Out-Null
$sentinel = Join-Path $target 'user-owned-file.txt'
Set-Content -LiteralPath $sentinel -Value 'preserve this file'
$sentinelHash = (Get-FileHash -LiteralPath $sentinel).Hash
$installProcess = Start-Process -FilePath $setup -ArgumentList "/S /TESTINSTALL /D=$target" -WindowStyle Hidden -PassThru
if (-not $installProcess.WaitForExit(60000)) { throw 'Installer timed out.' }
if ($installProcess.ExitCode -ne 0) { throw "Installer exited $($installProcess.ExitCode)" }
$manifest = Join-Path $target 'companion-install.ini'
if (-not (Test-Path -LiteralPath $manifest)) { throw 'Install marker missing.' }
if (-not (Select-String -LiteralPath $manifest -SimpleMatch 'TestMode=1' -Quiet)) { throw 'Test mode was not applied.' }
if (-not (Select-String -LiteralPath $manifest -SimpleMatch "Version=$Version" -Quiet)) { throw 'Installed version marker differs.' }
if ((Get-Item -LiteralPath (Join-Path $target 'Strigoi.Companion.exe')).VersionInfo.FileVersion -ne "$Version.0") { throw 'Updated binary has wrong version.' }
if ((Get-FileHash -LiteralPath $sentinel).Hash -ne $sentinelHash) { throw 'Update modified user-owned file.' }
if ($PreviousVersion -eq '0.1.1' -and (Test-Path -LiteralPath (Join-Path $target 'LEIA-ME.md'))) { throw 'Obsolete packaged readme remained.' }
$verified = 0
foreach ($file in Get-ChildItem -LiteralPath $payload -File -Recurse) {
    $relative = [IO.Path]::GetRelativePath($payload, $file.FullName)
    $installedFile = Join-Path $target $relative
    if (-not (Test-Path -LiteralPath $installedFile)) { throw "Missing payload: $relative" }
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $installedFile).Hash) { throw "Hash mismatch: $relative" }
    $verified++
}
$probeDirectory = Join-Path $testRoot 'app-smoke'
$app = Start-Process -FilePath (Join-Path $target 'Strigoi.Companion.exe') -ArgumentList "--smoke-test $probeDirectory" -WindowStyle Hidden -PassThru
if (-not $app.WaitForExit(30000)) { throw 'Installed app timed out.' }
if ($app.ExitCode -ne 0) { throw 'Installed app smoke test failed.' }
$result = Get-Content -LiteralPath (Join-Path $probeDirectory 'smoke-result.json') -Raw | ConvertFrom-Json
if (-not $result.passed) { throw 'Installed application checks failed.' }
$captureDirectory = Join-Path $testRoot 'app-capture'
$captureProcess = Start-Process -FilePath (Join-Path $target 'Strigoi.Companion.exe') -ArgumentList "--capture-test $captureDirectory" -WindowStyle Hidden -PassThru
if (-not $captureProcess.WaitForExit(60000) -or $captureProcess.ExitCode -ne 0) { throw 'Installed capture test failed.' }
$captureResult = Get-Content -LiteralPath (Join-Path $captureDirectory 'capture-result.json') -Raw | ConvertFrom-Json
if (-not $captureResult.passed) { throw 'Installed capture checks failed.' }
# Validate absolute uninstall target before running any removal.
$resolvedTarget = [IO.Path]::GetFullPath($target)
if (-not $resolvedTarget.StartsWith($testRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Uninstall path escaped the test workspace.' }
$uninstaller = Join-Path $resolvedTarget 'Uninstall.exe'
$uninstallProcess = Start-Process -FilePath $uninstaller -ArgumentList "/S _?=$resolvedTarget" -WindowStyle Hidden -PassThru
if (-not $uninstallProcess.WaitForExit(30000)) { throw 'Uninstaller timed out.' }
if ($uninstallProcess.ExitCode -ne 0) { throw "Uninstaller exited $($uninstallProcess.ExitCode)" }
if (Test-Path -LiteralPath (Join-Path $target 'Strigoi.Companion.exe')) { throw 'Application payload remained after uninstall.' }
foreach ($file in Get-ChildItem -LiteralPath $payload -File -Recurse) {
    $relative = [IO.Path]::GetRelativePath($payload, $file.FullName)
    if (Test-Path -LiteralPath (Join-Path $target $relative)) { throw "Payload not removed: $relative" }
}
if (-not (Test-Path -LiteralPath $sentinel)) { throw 'Uninstaller deleted a user-owned file.' }
$registryAfter = if (Test-Path -LiteralPath $registry) { Get-ItemProperty -LiteralPath $registry | Select-Object * -ExcludeProperty PSPath,PSParentPath,PSChildName,PSDrive,PSProvider | ConvertTo-Json -Depth 4 -Compress } else { 'absent' }
if ($registryBefore -ne $registryAfter) { throw 'Test installation modified production registration.' }
$profileAfter = @(ProfileHashes)
if (Compare-Object $profileBefore $profileAfter) { throw 'Production profile changed during test; inspect before attributing to installer.' }
$report = [ordered]@{
    passed = $true; version = $Version; verifiedPayloadFiles = $verified
    installedApplicationSmokePassed = $result.passed
    installedCapturePassed = $captureResult.passed
    previousVersion = $PreviousVersion; inPlaceUpgradePassed = [bool]$PreviousVersion
    productionProfileUnchanged = $true
    exactFileUninstallPassed = $true; userOwnedFilePreserved = $true
    productionRegistrationUnchanged = $true
    testRoot = $testRoot
    limitation = 'Silent isolated installation tested. Production Start menu/desktop shortcuts are defined in the installer but not created during this test.'
}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $testRoot 'installer-result.json') -Encoding utf8
$report | ConvertTo-Json
