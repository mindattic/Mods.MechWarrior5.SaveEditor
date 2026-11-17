<#
.SYNOPSIS
    Builds the release package: a self-contained win-x64 folder (MW5SaveEditor.exe beside the .NET runtime
    and its own DLLs) plus its notices, staged in obj\package\MW5SaveEditor\ and zipped to
    dist\MechWarrior5.SaveEditor-<version>.zip.

.DESCRIPTION
    The staged folder is the payload /publish hashes for its no-op guard (it holds no file whose
    content changes on every publish; the git commit is kept out of the version for the same reason), and the zip is the GitHub release asset CI uploads to
    NexusMods. Players need nothing installed: the .NET runtime is bundled. It is published as a plain
    folder, not one single-file exe: a small launcher with 140 MB appended reads as a packer or dropper to
    antivirus heuristics (VirusTotal flagged 3.0.0), while ordinary DLLs beside the exe don't (M5SE-T126).

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

$project = Join-Path $root 'src\MechWarrior5.SaveEditor.App\MechWarrior5.SaveEditor.App.csproj'
dotnet publish $project -c $Configuration -r win-x64 --self-contained true -nologo `
    -p:PublishSingleFile=false -p:SatelliteResourceLanguages=en -p:IncludeSourceRevisionInInformationalVersion=false `
    -p:DebugType=none -p:GenerateDocumentationFile=false -o $stage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }

# The program and its runtime ship; build leftovers (symbols, XML docs, a WebView2 stray) don't.
Get-ChildItem $stage -File -Recurse | Where-Object { $_.Extension -in @('.pdb', '.xml') -or $_.Name -like 'Microsoft.Web.WebView2*' } | Remove-Item -Force
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $stage
# Add-on packs: plain JSON data for other mods, read from the Addons folder beside the exe.
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'Addons') | Out-Null
Copy-Item (Join-Path $root 'addons\*.json') (Join-Path $stage 'Addons')
Copy-Item (Join-Path $root 'docs\USER_GUIDE.md') (Join-Path $stage 'README.md')

# The one NexusMods description always carries the full changelog (M5SE-T132).
& (Join-Path $PSScriptRoot 'nexus-description.ps1') | Out-Null

$zip = Join-Path $dist "MechWarrior5.SaveEditor-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
$zip
