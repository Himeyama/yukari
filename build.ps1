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
Move-Item ..\yukari-ui\build build\yukari-ui

# yukari のビルド
$csproj = ".\Yukari\Yukari.csproj"
$version = (Get-Date).ToString("yy.M.d")
dotnet publish $csproj -c Release -p:Version=$version -o build\yukari