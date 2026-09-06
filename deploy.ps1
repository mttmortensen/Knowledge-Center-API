<#
Builds and deploys Knowledge-Center-API to MRTN-APPS (10.0.0.190).

Requires a one-time credential file for the MRTN-APPS admin account:
    $cred = Get-Credential -UserName "matt"
    $cred | Export-Clixml -Path "$env:USERPROFILE\.mrtn-apps-cred.xml"
And 10.0.0.190 added to this machine's WinRM TrustedHosts:
    Set-Item WSMan:\localhost\Client\TrustedHosts -Value "10.0.0.190" -Force -Concatenate
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot     = $PSScriptRoot
$publishDir   = Join-Path $repoRoot "bin\_deploy-publish"
$remoteHost   = "10.0.0.190"
$remoteShare  = "\\$remoteHost\C$\apis\KC"
$serviceName  = "KnowledgeCenterAPI"
$healthPort   = 5065
$credPath     = "$env:USERPROFILE\.mrtn-apps-cred.xml"

if (-not (Test-Path $credPath)) {
    throw "Missing credential file at $credPath. See script header for setup."
}

Write-Host "==> Publishing $Configuration build..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $repoRoot "Knowledge-Center-API.csproj") -c $Configuration -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$stored = Import-Clixml -Path $credPath
$cred = New-Object System.Management.Automation.PSCredential("$remoteHost\matt", $stored.Password)

Write-Host "==> Stopping service $serviceName on $remoteHost..." -ForegroundColor Cyan
Invoke-Command -ComputerName $remoteHost -Credential $cred -ScriptBlock {
    param($svc)
    Stop-Service -Name $svc -Force -ErrorAction Stop
} -ArgumentList $serviceName

Write-Host "==> Copying published files to $remoteShare..." -ForegroundColor Cyan
# Note: no /MIR - this only overwrites files present in the publish output,
# so server-only files (appsettings.Production.json) are left untouched.
$roboArgs = @($publishDir, $remoteShare, "/E", "/Z", "/FFT", "/NFL", "/NDL", "/NP")
$psDrive = New-PSDrive -Name MRTNDeploy -PSProvider FileSystem -Root "\\$remoteHost\C$" -Credential $cred
try {
    robocopy @roboArgs
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
} finally {
    Remove-PSDrive MRTNDeploy -ErrorAction SilentlyContinue
}

Write-Host "==> Starting service $serviceName on $remoteHost..." -ForegroundColor Cyan
Invoke-Command -ComputerName $remoteHost -Credential $cred -ScriptBlock {
    param($svc)
    Start-Service -Name $svc -ErrorAction Stop
    Start-Sleep -Seconds 2
    (Get-Service -Name $svc).Status
} -ArgumentList $serviceName

Write-Host "==> Verifying API responds on port $healthPort..." -ForegroundColor Cyan
Start-Sleep -Seconds 2
$test = Test-NetConnection -ComputerName $remoteHost -Port $healthPort -WarningAction SilentlyContinue
if ($test.TcpTestSucceeded) {
    Write-Host "Deploy succeeded - API is listening on ${remoteHost}:${healthPort}" -ForegroundColor Green
} else {
    Write-Warning "Service started but port $healthPort is not responding yet - check the service manually."
}
