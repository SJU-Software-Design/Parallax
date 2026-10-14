param([string]$GameExecutable)
$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Common.ps1')
$projectDir = Get-ParallaxProjectPath
$deliveryDir = Get-ParallaxDeliveryPath -ProjectPath $projectDir
if (-not $GameExecutable) { $GameExecutable = (Join-Path $deliveryDir 'Parallax_Unity_v0.7_Indoor\Parallax.exe') }
if (-not (Test-Path -LiteralPath $GameExecutable)) { throw 'Build the indoor Windows player first.' }
$indoorOutput = Join-Path $projectDir 'Validation\Indoor'
New-Item -ItemType Directory -Path $indoorOutput -Force | Out-Null
$indoorArgs = @('-indoor-smoke', '-shade-output', ('"' + $indoorOutput + '"'), '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900', '-logFile', ('"' + (Join-Path $indoorOutput 'player.log') + '"'))
$indoorProcess = Start-Process -FilePath $GameExecutable -ArgumentList $indoorArgs -WindowStyle Hidden -PassThru
if (-not $indoorProcess.WaitForExit(180000)) { Stop-Process -Id $indoorProcess.Id; throw 'Indoor validation exceeded 180 seconds.' }
$indoorReportPath = Join-Path $indoorOutput 'indoor-smoke-results.json'
if (-not (Test-Path -LiteralPath $indoorReportPath)) { throw 'The player did not produce a validation report.' }
$indoorReport = Get-Content -LiteralPath $indoorReportPath -Raw | ConvertFrom-Json
if ($indoorProcess.ExitCode -ne 0 -or -not $indoorReport.passed) { Get-Content -LiteralPath $indoorReportPath -Raw; throw 'Indoor validation failed.' }
Write-Output "Indoor checks passed: $($indoorReport.checks.Count)"
Write-Output "Unity: $($indoorReport.unity) / GPU: $($indoorReport.gpu)"
