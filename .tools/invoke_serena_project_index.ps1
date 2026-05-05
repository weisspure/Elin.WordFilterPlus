param(
	[string]$Project = ""
)

$ErrorActionPreference = "Stop"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[Console]::OutputEncoding = $utf8NoBom
$OutputEncoding = $utf8NoBom
$env:PYTHONIOENCODING = "utf-8"

. (Join-Path $PSScriptRoot "serena_output_filter.ps1")

$serenaCommand = Get-Command serena -ErrorAction SilentlyContinue
if ($null -eq $serenaCommand) {
	throw "The 'serena' command was not found on PATH."
}

$projectRoot = if (![string]::IsNullOrWhiteSpace($Project)) { $Project } else { (Get-Location).Path }
$projectConfig = Join-Path $projectRoot ".serena\project.yml"
$projectLocalConfig = Join-Path $projectRoot ".serena\project.local.yml"
$defaultLanguage = "csharp"

# If ElinGamePath was saved at user scope but terminal was not restarted, load it into this process.
if ([string]::IsNullOrWhiteSpace($env:ElinGamePath)) {
	$userElinPath = [System.Environment]::GetEnvironmentVariable("ElinGamePath", "User")
	if (![string]::IsNullOrWhiteSpace($userElinPath)) {
		$env:ElinGamePath = $userElinPath
		Write-Host "[invoke_serena_project_index] Loaded ElinGamePath from user environment for this session."
	}
}

# Avoid interactive first-run prompts by creating a C#-only project configuration.
if (!(Test-Path $projectConfig)) {
	Write-Host "[invoke_serena_project_index] No project config found. Creating Serena project (language: $defaultLanguage)..."
	& $serenaCommand.Source project create $projectRoot --language $defaultLanguage 2>&1 | ForEach-Object {
		$line = $_.ToString()
		if (!(Test-SerenaNoiseLine -Line $line)) {
			Write-Host $line
		}
	}
	if ($LASTEXITCODE -ne 0) {
		exit $LASTEXITCODE
	}
}

# Clean up legacy auto-generated OmniSharp fallback override from older script versions.
if (Test-Path $projectLocalConfig) {
	$projectLocalContent = Get-Content -Raw $projectLocalConfig
	if ($projectLocalContent -match "Auto-generated fallback" -and $projectLocalContent -match '(?m)^-\s*csharp_omnisharp\s*$') {
		Remove-Item $projectLocalConfig -Force
		Write-Host "[invoke_serena_project_index] Removed legacy local OmniSharp fallback override."
	}
}

$indexArgs = @("project", "index")
if (![string]::IsNullOrWhiteSpace($Project)) {
	$indexArgs += $Project
}

$previousErrorActionPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
try {
	& $serenaCommand.Source @indexArgs 2>&1 | ForEach-Object {
		$line = $_.ToString()
		if (!(Test-SerenaNoiseLine -Line $line)) {
			Write-Host $line
		}
	}
}
finally {
	$ErrorActionPreference = $previousErrorActionPreference
}

$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) {
	exit $exitCode
}
