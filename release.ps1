# Makes a new release on GitHub: every Glyph with updates on picks it up.
#   powershell -ExecutionPolicy Bypass -File release.ps1                 (next patch version, e.g. 1.0.3 -> 1.0.4)
#   powershell -ExecutionPolicy Bypass -File release.ps1 -Version 1.2.0 -Notes "What changed"
#   powershell -ExecutionPolicy Bypass -File release.ps1 -LocalOnly      (just build out\ + the installer: no version bump, nothing published)
# Needs: the .NET 9 SDK, Inno Setup 6.5+ (winget install JRSoftware.InnoSetup), git and the GitHub CLI (gh) signed in.
param([string]$Version = "", [string]$Notes = "", [switch]$LocalOnly)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = "$root\src\Glyph\Glyph.csproj"

# 1. the version number (kept in Glyph.csproj)
$xml = Get-Content $proj -Raw
if ($xml -notmatch '<Version>(\d+)\.(\d+)\.(\d+)</Version>') { throw "no <Version> in Glyph.csproj" }
if ($LocalOnly) { $Version = "$($Matches[1]).$($Matches[2]).$($Matches[3])" }
else {
    if ($Version -eq "") { $Version = "$($Matches[1]).$($Matches[2]).$([int]$Matches[3] + 1)" }
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "version must look like 1.2.3" }
    $xml = $xml -replace '<Version>[^<]*</Version>', "<Version>$Version</Version>"
    [IO.File]::WriteAllText($proj, $xml, (New-Object Text.UTF8Encoding $false))
}
Write-Output "version $Version"

# 2. build into out\Glyph\bin, laid out like the Glyph folder (bin\ + an empty config\ for your settings)
$out = "$root\out"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish $proj -c Release -o "$out\Glyph\bin" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "build failed" }
New-Item -ItemType Directory -Force "$out\Glyph\config" | Out-Null
Set-Content "$out\Glyph\config\README.txt" "Your settings, themes and shortcuts live in this folder. Updates never change it." -Encoding UTF8
$env:GLYPH_HOME = $root   # so the new build writes the guide into this folder, not into out\
Start-Process "$out\Glyph\bin\Glyph.exe" -ArgumentList "--write-guide" -Wait -NoNewWindow
Remove-Item Env:GLYPH_HOME
Copy-Item "$root\config\GUIDE.md" "$out\Glyph\GUIDE.md" -ErrorAction SilentlyContinue
Copy-Item "$root\README.md" "$out\Glyph\README.md"
$zip = "$out\Glyph-win-x64.zip"
Push-Location $out; & "$env:SystemRoot\System32\tar.exe" -a -c -f $zip Glyph; Pop-Location   # Windows' tar (Git's can't write zips)
if ($LASTEXITCODE -ne 0) { throw "zip failed" }
Write-Output "zip $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)"

# 2b. the installer (Glyph-Setup-<version>.exe): pick a folder, Start menu shortcuts, start with Windows, uninstaller
$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup not found (winget install JRSoftware.InnoSetup)" }
& $iscc /Q "/DSrc=$out\Glyph" "/DVersion=$Version" "/O$out" "$root\installer\Glyph.iss"
if ($LASTEXITCODE -ne 0) { throw "installer build failed" }
$setup = "$out\Glyph-Setup-$Version.exe"
Write-Output "installer $setup ($([math]::Round((Get-Item $setup).Length / 1MB, 1)) MB)"
if ($LocalOnly) { return }

# 3. commit, tag, push, publish
git -C $root add -A
git -C $root commit -m "Glyph v$Version" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" | Out-Null
git -C $root tag "v$Version"
git -C $root push origin HEAD --tags
if ($LASTEXITCODE -ne 0) { throw "push failed" }
if ($Notes -eq "") { $Notes = "Glyph $Version" }
gh release create "v$Version" $zip $setup --title "Glyph v$Version" --notes $Notes
if ($LASTEXITCODE -ne 0) { throw "release failed" }
Write-Output "released v$Version"
