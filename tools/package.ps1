<#
.SYNOPSIS
    Builds the release package: a self-contained, single-file win-x64 MW5SaveEditor.exe plus its
    notices, staged in obj\package\MW5SaveEditor\ and zipped to dist\MW5SaveEditor-<version>.zip.

.DESCRIPTION
    The staged folder is the payload /publish hashes for its no-op guard (it holds no file whose
    content changes on every publish), and the zip is the GitHub release asset CI uploads to
    NexusMods. Players need nothing installed: the .NET runtime is bundled.

.OUTPUTS
    The zip path, on the pipeline.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$props = [xml](Get-Content (Join-Path $root 'Directory.Build.props'))
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw 'No <Version> in Directory.Build.props.' }

$stage = Join-Path $root 'obj\package\MW5SaveEditor'
$dist = Join-Path $root 'dist'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage, $dist | Out-Null

$project = Join-Path $root 'src\Mech5.SaveEditor.App\Mech5.SaveEditor.App.csproj'
dotnet publish $project -c $Configuration -r win-x64 --self-contained true -nologo `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -p:GenerateDocumentationFile=false -o $stage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }

# Only the program and what players should read ship; nothing else from the publish folder.
Get-ChildItem $stage -File | Where-Object { $_.Name -notin @('MW5SaveEditor.exe') -and $_.Extension -ne '.txt' } | Remove-Item -Force
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $stage
Copy-Item (Join-Path $root 'docs\USER_GUIDE.md') (Join-Path $stage 'README.md')

$zip = Join-Path $dist "MW5SaveEditor-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
$zip
