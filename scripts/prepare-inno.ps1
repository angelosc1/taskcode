param([string]$Destination)
$ErrorActionPreference = 'Stop'
$taskcodeRoot = Split-Path -Parent $PSScriptRoot
if (-not $Destination) { $Destination = Join-Path $taskcodeRoot 'out/tools/InnoSetup' }
if (Test-Path -LiteralPath (Join-Path $Destination 'ISCC.exe')) { Write-Output "Compiler ready: $Destination"; exit 0 }
$taskcodeToolDirectory = Join-Path $taskcodeRoot 'out/tools'
New-Item -ItemType Directory -Path $taskcodeToolDirectory -Force | Out-Null
$taskcodeInstaller = Join-Path $taskcodeToolDirectory 'innosetup-7.1.1-x64.exe'
$taskcodeExpectedHash = '8A5A6711C3766A6E5FB74F280BA0E3551E719CE94538D901FDBD1F8F9920B3F0'
if (-not (Test-Path -LiteralPath $taskcodeInstaller)) {
    Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_1/innosetup-7.1.1-x64.exe' -OutFile $taskcodeInstaller
}
if ((Get-FileHash -LiteralPath $taskcodeInstaller -Algorithm SHA256).Hash -ne $taskcodeExpectedHash) { throw 'Compiler installer checksum mismatch.' }
$taskcodeSignature = Get-AuthenticodeSignature -LiteralPath $taskcodeInstaller
if ($taskcodeSignature.Status -ne 'Valid' -or $taskcodeSignature.SignerCertificate.Subject -notmatch 'Pyrsys') { throw 'Compiler installer signature is not valid.' }
$taskcodeProcess = Start-Process -FilePath $taskcodeInstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER',('/DIR="'+[System.IO.Path]::GetFullPath($Destination)+'"'),'/NOICONS','/SP-') -WindowStyle Hidden -Wait -PassThru
if ($taskcodeProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $Destination 'ISCC.exe'))) { throw 'Compiler setup failed.' }
Write-Output "Compiler ready: $Destination"
