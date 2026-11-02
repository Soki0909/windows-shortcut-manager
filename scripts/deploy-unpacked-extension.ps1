#requires -Version 7.4
param(
  [Parameter(Mandatory=$true)][string]$Destination,
  [string]$ArchivePath, [string]$ArchiveSha256,
  [string]$PreviousArchivePath, [string]$PreviousArchiveSha256,
  [switch]$RecoverOnly
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'lib/ExtensionDeployment.psm1') -Force
Invoke-ExtensionDeployment @PSBoundParameters
