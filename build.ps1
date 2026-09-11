$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Autobahn\Autobahn.csproj'
$output = Join-Path $PSScriptRoot 'dist'
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Write-Output "Ready: $output\AntigravityAutobahn.exe"
