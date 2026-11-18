Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# git status
Write-Host "=== STATUS ==="
& git status --short

# git add
Write-Host "=== ADD ==="
& git add -A

# git diff --cached
Write-Host "=== DIFF ==="
& git diff --cached --stat

# git commit
Write-Host "=== COMMIT ==="
& git commit -m "fix(velum): push DXF pending after save (case 57)

During Save, SolidWorks performs rebuild inside the save contour where
live pending write is suppressed (_partSaveDepth). Thickness/radius changes
confirmed by Save were lost until manual Update button.

Fix:
- BeginPartSave captures GetSaveFlag() before save starts
- NotifyPartSaved pushes pending via pure predicate
  ShouldWriteDxfPendingAfterSave, excluding fold/unfold and config changes
- New pure class VelumDxfGeometryStampRules (E96)

Regression tests: 503/503 (6 new).
Cases 56+57 + heuristics E95+E96."

# git push
Write-Host "=== PUSH ==="
& git push origin main

Write-Host "=== DONE ==="
