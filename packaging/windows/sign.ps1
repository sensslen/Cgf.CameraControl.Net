param([Parameter(Mandatory)][string[]] $Files)

$ErrorActionPreference = 'Stop'
$pfx = Join-Path $env:RUNNER_TEMP 'cert.pfx'
[IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($env:CERTIFICATE))
try {
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe |
        Where-Object { $_.FullName -match 'x64' } | Select-Object -First 1
    & $signtool.FullName sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
        /f $pfx /p $env:CERTIFICATE_PASSWORD @Files
    if ($LASTEXITCODE -ne 0) { throw "signtool failed with exit code $LASTEXITCODE" }
}
finally {
    Remove-Item $pfx
}
