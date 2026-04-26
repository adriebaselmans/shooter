param(
    [string]$Python = "python"
)

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Error "Git was not found on PATH."
    exit 1
}

$branch = & git -C $repoRoot branch --show-current

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to determine the current git branch."
    exit $LASTEXITCODE
}

if (-not $branch.StartsWith("release/")) {
    Write-Error "export-release-docs is release-only. Current branch '$branch' is not a release branch."
    exit 1
}

if (-not (Get-Command $Python -ErrorAction SilentlyContinue)) {
    Write-Error "Python executable '$Python' was not found."
    exit 1
}

& $Python -m team_orchestrator.cli export-docs

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Generated user-facing docs for $branch."
