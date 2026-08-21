$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "ExitPrivacyGuard.csproj"
$output = Join-Path $PSScriptRoot "publish\win-arm64"

dotnet publish $project --configuration Release --runtime win-arm64 --self-contained true --output $output `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false

Write-Host "`n构建完成：$output\离职隐私卫士.exe"
