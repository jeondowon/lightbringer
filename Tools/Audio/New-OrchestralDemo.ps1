param([string]$OutputPath = (Join-Path $PSScriptRoot '../../Assets/Audio/Music/Lightbringer_Orchestral_Demo.wav'))
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'OrchestralDemo.cs')
$renderer = [LightbringerOrchestralDemo]::new()
$samples = Join-Path $PSScriptRoot 'Samples/VSCO2CE'
foreach ($entry in (Get-Content (Join-Path $PSScriptRoot 'OrchestralSamples.json') -Raw | ConvertFrom-Json)) {
    $renderer.Load($entry.bank, (Join-Path $samples $entry.file), $entry.root)
}
$renderer.Render($OutputPath)
