Stop-Process -Name yukari-engine -ErrorAction SilentlyContinue
Stop-Process -Name yukari -ErrorAction SilentlyContinue
if (Test-Path -Path "build") {
    Remove-Item -Path "build" -Recurse -Force
}
New-Item -Path build -ItemType Directory

# yukari-engine のビルド
Set-Location ..\yukari-engine
cargo build --release
cargo license --color never > hikari-engine-3rd-party-licenses.txt
Set-Location ..\yukari
if (Test-Path -Path "build\yukari-engine") {
    Remove-Item -Path "build\yukari-engine" -Recurse -Force
}
Move-Item ..\yukari-engine\target\release build\yukari-engine
Move-Item ..\yukari-engine\hikari-engine-3rd-party-licenses.txt .\build\yukari-engine

# yukari-ui のビルド
Set-Location ..\yukari-ui
yarn build
yarn licenses list > hikari-ui-3rd-party-licenses.txt
Set-Location ..\yukari
if (Test-Path -Path "build\yukari-ui") {
    Remove-Item -Path "build\yukari-ui" -Recurse -Force
}
Move-Item ..\yukari-ui\build build\yukari-ui

# yukari のビルド
$csproj = ".\Yukari\Yukari.csproj"
$version = (Get-Date).ToString("yy.M.d")
dotnet publish $csproj -c Release -p:Version=$version -o build\yukari
Copy-Item .\Yukari\Assets\third-party-oss.md .\build\yukari

# NSIS
$date = (Get-Date).ToString("yyyyMMdd")
$version = (Get-Date).ToString("yy.M.d")
$publishDir = "build"
$appName = "Yukari"
$execFile = "Yukari.exe"
$publisher = "ひかり"
$muiIcon = "Yukari\Assets\App.ico"
$size = [Math]::Round((Get-ChildItem $publishDir -Force -Recurse -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum / 1KB, 0, [MidpointRounding]::AwayFromZero)
.'C:\Program Files (x86)\NSIS\makensis.exe' /DDESKTOP_APP_NAME="$appName" /DVERSION="$version" /DDATE="$date" /DSIZE="$size" /DMUI_ICON="$muiIcon" /DMUI_UNICON="$muiIcon" /DPUBLISH_DIR="$publishDir" /DPRODUCT_NAME="$appName" /DEXEC_FILE="$execFile" /DPUBLISHER="$publisher" installer.nsh

# scp install.exe 10.16.125.231:.