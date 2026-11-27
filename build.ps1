# Builds Glyph into the bin folder. Run:  powershell -ExecutionPolicy Bypass -File build.ps1
# If this folder's Glyph is running it is closed first and started again afterwards (even if the build fails).
# An installed Glyph (Glyph-Setup) is left alone; while one is installed, this test build doesn't take over
# "start with Windows" (GLYPH_DEV=1).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$bin = "$root\bin\"
$running = Get-Process Glyph -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($bin, [StringComparison]::OrdinalIgnoreCase) }
if ($running) { $running | Stop-Process -Force; Start-Sleep -Milliseconds 500 }
$installed = Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{6F1C8A52-3D4B-4E7A-9C21-6A7970680001}_is1" -ErrorAction SilentlyContinue
try {
    dotnet publish "$root\src\Glyph\Glyph.csproj" -c Release -o "$root\bin" --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
    Write-Output "built $root\bin\Glyph.exe"
}
finally {
    if ($running) {
        if ($installed) { $env:GLYPH_DEV = "1" }
        Start-Process "$root\bin\Glyph.exe"; Write-Output "restarted Glyph"
    }
}
