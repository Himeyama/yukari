if (Test-Path -Path "build") {
    Remove-Item -Path "build" -Recurse -Force
}
New-Item -Path build -ItemType Directory

# yukari-engine のビルド
Set-Location ..\yukari-engine
cargo build --release
Set-Location ..\yukari
Move-Item ..\yukari-engine\target\release build\yukari-engine

# yukari-ui のビルド
Set-Location ..\yukari-ui
yarn build
Set-Location ..\yukari
if (Test-Path -Path "build\yukari-ui") {
    Remove-Item -Path "build\yukari-ui" -Recurse -Force
}
Move-Item ..\yukari-ui\build build\yukari-ui

# yukari のビルド
$csproj = ".\Yukari\Yukari.csproj"
$version = (Get-Date).ToString("yy.M.d")
dotnet publish $csproj -c Release -p:Version=$version -o build\yukari

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