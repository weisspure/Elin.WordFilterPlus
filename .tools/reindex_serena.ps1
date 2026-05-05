$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$indexScript = Join-Path $PSScriptRoot "invoke_serena_project_index.ps1"

if (!(Test-Path $indexScript)) {
	throw "Missing Serena index wrapper: $indexScript"
}

Write-Host "[reindex_serena] Re-indexing Serena project..."
& $indexScript -Project $repoRoot
if (!$?) {
	throw "serena project index failed."
}

Write-Host "[reindex_serena] Done."
