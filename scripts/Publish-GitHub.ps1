[CmdletBinding()]
param([string]$Repository = 'Eris92/SIRK-Access')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
foreach ($cmd in @('git','gh')) { if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) { throw "Missing prerequisite: $cmd" } }
function Invoke-Checked { param([string]$Exe,[string[]]$Arguments) & $Exe @Arguments; if ($LASTEXITCODE -ne 0) { throw "$Exe failed ($LASTEXITCODE)" } }
Invoke-Checked gh @('auth','status')
if (-not (Test-Path .git)) {
    Invoke-Checked git @('init','-b','main')
    Invoke-Checked git @('add','.')
    Invoke-Checked git @('commit','-m','Initial SIRK Access Portal MVP')
}
& gh repo view $Repository --json nameWithOwner,visibility 2>$null
if ($LASTEXITCODE -eq 0) { throw "Repository already exists. Review it before pushing: $Repository" }
if (& git remote) { throw 'Existing remote detected; review it before publishing.' }
Invoke-Checked gh @('repo','create',$Repository,'--private','--source=.','--remote=origin','--push')
Invoke-Checked gh @('repo','view',$Repository,'--json','url,visibility,defaultBranchRef')
