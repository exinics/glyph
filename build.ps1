# Builds Glyph into the bin folder. Run:  powershell -ExecutionPolicy Bypass -File build.ps1
# If Glyph is running it is closed first and started again afterwards (even if the build fails).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$running = Get-Process Glyph -ErrorAction SilentlyContinue
if ($running) { $running | Stop-Process -Force; Start-Sleep -Milliseconds 500 }
try {
    dotnet publish "$root\src\Glyph\Glyph.csproj" -c Release -o "$root\bin" --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
    Write-Output "built $root\bin\Glyph.exe"
}
finally {
    if ($running) { Start-Process "$root\bin\Glyph.exe"; Write-Output "restarted Glyph" }
}
