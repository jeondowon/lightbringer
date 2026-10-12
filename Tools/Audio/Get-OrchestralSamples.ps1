# Downloads only the selected CC0 recordings; no service accounts or paid APIs.
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$revision = '440300901dfe9275fd84e0b7763af1f8443ae62e'
$folder = Join-Path $PSScriptRoot 'Samples/VSCO2CE'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$manifest = Get-Content (Join-Path $PSScriptRoot 'OrchestralSamples.json') -Raw | ConvertFrom-Json
foreach ($sample in $manifest) {
    $target = Join-Path $folder $sample.file
    if (-not (Test-Path -LiteralPath $target) -or (Get-Item -LiteralPath $target).Length -ne $sample.size) {
        $urlPath = (($sample.source -split '/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
        Invoke-WebRequest -UseBasicParsing -Uri "https://raw.githubusercontent.com/sgossner/VSCO-2-CE/$revision/$urlPath" -OutFile $target
    }
    $bytes = [IO.File]::ReadAllBytes($target)
    $header = [Text.Encoding]::ASCII.GetBytes("blob $($bytes.Length)`0")
    $hasher = [Security.Cryptography.SHA1]::Create()
    try {
        $blobHash = [BitConverter]::ToString($hasher.ComputeHash([byte[]]($header + $bytes))).Replace('-', '').ToLowerInvariant()
        if ($blobHash -ne $sample.blob) { throw "Source hash mismatch: $($sample.file)" }
    } finally { $hasher.Dispose() }
    Write-Output "Verified $($sample.file)"
}
foreach ($name in @('LICENSE', 'Readme.txt')) {
    Invoke-WebRequest -UseBasicParsing -Uri "https://raw.githubusercontent.com/sgossner/VSCO-2-CE/$revision/$name" -OutFile (Join-Path $folder $name)
}
