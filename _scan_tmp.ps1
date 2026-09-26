[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$files = Get-ChildItem -Path . -Recurse -Include *.cs,*.csproj,*.sln,*.config,*.md -File |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|packages|node_modules|\.vs|\.git)\\' } |
    Sort-Object Length -Descending | Select-Object -First 45
foreach ($f in $files) {
    $lines = (Get-Content -LiteralPath $f.FullName).Count
    Write-Output ("{0}`t{1}" -f $lines, $f.FullName)
}