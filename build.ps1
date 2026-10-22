<#
  Builds everything for a release of Opsis Appearance:
    dist\standalone\     the stand-alone app (framework-dependent, needs the .NET 10 Desktop Runtime)
    dist\plugin\         the Opsis plugin (OpsisAppearancePlugin.dll)
    Output\OpsisAppearance-Standalone-<v>.zip, OpsisAppearance-Plugin-<v>.zip, OpsisAppearance-Setup.exe (+ .sha256 files)
  Requires the .NET 10 SDK and Inno Setup 7 (6.x works too). Usage: powershell -File build.ps1 [-SkipTests]
#>
param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root
$version = ([xml](Get-Content "$root\src\OpsisAppearance.Plugin\OpsisAppearancePlugin.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

if (-not $SkipTests) {
    dotnet test OpsisAppearance.sln -c Release -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}
dotnet build OpsisAppearance.sln -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

Remove-Item "$root\dist" -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish src\OpsisAppearance.App\OpsisAppearance.App.csproj -c Release --no-build -o "$root\dist\standalone" -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
New-Item -ItemType Directory -Force "$root\dist\plugin" | Out-Null
Copy-Item "$root\src\OpsisAppearance.Plugin\bin\Release\net10.0-windows\OpsisAppearancePlugin.dll" "$root\dist\plugin\"
@"
Opsis Appearance - plugin for Opsis
Copy this folder's DLL into  %AppData%\Opsis\plugins\Opsis Appearance\  and restart Opsis.
(or run OpsisAppearance-Setup.exe and choose "As a plugin of Opsis")
"@ | Set-Content "$root\dist\plugin\README.txt"

New-Item -ItemType Directory -Force "$root\Output" | Out-Null
$zipStandalone = "$root\Output\OpsisAppearance-Standalone-$version.zip"
$zipPlugin = "$root\Output\OpsisAppearance-Plugin-$version.zip"
Remove-Item $zipStandalone, $zipPlugin -Force -ErrorAction SilentlyContinue
Compress-Archive -Path "$root\dist\standalone\*" -DestinationPath $zipStandalone
# the plugin zip contains the ready-to-copy folder "Opsis Appearance"
$stage = "$root\dist\stage\Opsis Appearance"
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item "$root\dist\plugin\*" $stage
Compress-Archive -Path "$root\dist\stage\Opsis Appearance" -DestinationPath $zipPlugin

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe", "$env:ProgramFiles\Inno Setup 7\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup (ISCC.exe) not found' }
& $iscc "$root\installer\OpsisAppearance.iss"
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed' }

foreach ($f in $zipStandalone, $zipPlugin, "$root\Output\OpsisAppearance-Setup.exe") {
    $h = (Get-FileHash $f -Algorithm SHA256).Hash.ToLower()
    [System.IO.File]::WriteAllText("$f.sha256", "$h  $(Split-Path $f -Leaf)")
    Write-Host "$(Split-Path $f -Leaf)  $h"
}
