$ErrorActionPreference = "Stop"
$root = "D:\VELUM\Velum\docs\help"
$utf8 = New-Object System.Text.UTF8Encoding $false
$files = Get-ChildItem -Path $root -Filter "*.html" -Recurse
$ok = 0
foreach ($f in $files) {
  $rel = $f.FullName.Substring($root.Length).TrimStart('\', '/')
  $parts = $rel -split '[\\/]'
  $depth = $parts.Length - 1
  $base = ""
  if ($depth -gt 0) { $base = "../" * $depth }
  $scriptSrc = $base + "shared/nav.js"
  $text = [System.IO.File]::ReadAllText($f.FullName)

  $start = $text.IndexOf('<div class="nav">')
  if ($start -lt 0) {
    Write-Host "SKIP (no nav): $rel"
    continue
  }
  $contentMark = '<div class="content">'
  $end = $text.IndexOf($contentMark, $start)
  if ($end -lt 0) {
    Write-Host "SKIP (no content): $rel"
    continue
  }
  $before = $text.Substring(0, $end)
  $close = $before.LastIndexOf('</div>')
  if ($close -lt $start) {
    Write-Host "SKIP (bad close): $rel"
    continue
  }
  $navEnd = $close + 6
  $replacement = '<div id="velum-nav" class="nav" data-base="' + $base + '"></div>' + "`r`n    "
  $newText = $text.Substring(0, $start) + $replacement + $text.Substring($navEnd).TrimStart("`r", "`n", " ")

  if ($newText -notmatch 'shared/nav\.js') {
    $newText = $newText.Replace("</body>", "  <script src=`"$scriptSrc`"></script>`r`n</body>")
  }

  [System.IO.File]::WriteAllText($f.FullName, $newText, $utf8)
  $ok++
  Write-Host "OK $rel base=$base"
}
Write-Host "DONE updated=$ok"
