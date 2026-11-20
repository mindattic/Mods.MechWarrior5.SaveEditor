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

# The program and its runtime ship; build leftovers (symbols, XML docs, a WebView2 stray) don't. Nor do (M5SE-T149, the
# NexusMods quarantine review): createdump.exe, the runtime's crash-dump tool, which the editor never runs; a second exe
# that dumps process memory is a known LSASS-dumping trick, so scanners flag it. And WebView2Loader.dll (an embedded
# browser's native loader, one per CPU under runtimes\), which a dependency drags in for a control this app never uses.
Get-ChildItem $stage -File -Recurse |
    Where-Object { $_.Extension -in @('.pdb', '.xml') -or $_.Name -like '*WebView2*' -or $_.Name -eq 'createdump.exe' } |
    Remove-Item -Force
# Folders the removals left empty (runtimes\win-*\native) go too, deepest first.
Get-ChildItem $stage -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending |
    Where-Object { -not (Get-ChildItem $_.FullName -Force) } | Remove-Item -Force
# The zip must hold exactly one program: anything else executable is a packaging mistake, so stop before it ships.
$exes = @(Get-ChildItem $stage -File -Recurse -Filter *.exe)
if ($exes.Count -ne 1 -or $exes[0].Name -ne 'MW5SaveEditor.exe') { throw "Package must hold only MW5SaveEditor.exe, found: $($exes.Name -join ', ')" }
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $stage
# Add-on packs: plain JSON data for other mods, read from the Addons folder beside the exe.
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'Addons') | Out-Null
Copy-Item (Join-Path $root 'addons\*.json') (Join-Path $stage 'Addons')
Copy-Item (Join-Path $root 'docs\USER_GUIDE.md') (Join-Path $stage 'README.md')

# The one NexusMods description always carries the full changelog (M5SE-T132).
& (Join-Path $PSScriptRoot 'nexus-description.ps1') | Out-Null

$zip = Join-Path $dist "MechWarrior5.SaveEditor-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
# A plain Deflate zip with '/' in every entry name, written entry by entry so it comes out the same under Windows
# PowerShell 5.1 and PowerShell 7 (older Compress-Archive versions write '\', which extractors and mod managers unpack as
# flat file names, and NexusMods' uploader blocks an archive it can't preview).
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem $stage -File -Recurse | Sort-Object FullName) {
        $name = $file.FullName.Substring($stage.Length).TrimStart('\', '/').Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
}
$zip
