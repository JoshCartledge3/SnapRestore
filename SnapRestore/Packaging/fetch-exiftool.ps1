$ErrorActionPreference = "Stop"

$version = "13.59"
$expectedSha256 = "44b512b25af500724ba579d0a53c8fc5851628b692dd5e5d94ae4a15c2cba9ec"
$url = "https://sourceforge.net/projects/exiftool/files/exiftool-$($version)_64.zip/download"
$projectDirectory = Split-Path -Parent $PSScriptRoot
$targetDirectory = Join-Path $projectDirectory "Tools/win-x64"
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) "snaprestore-exiftool-$([Guid]::NewGuid().ToString('N'))"
$archive = Join-Path $temporaryDirectory "exiftool-$($version)_64.zip"

try {
    New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
    Invoke-WebRequest -Uri $url -OutFile $archive

    $actualSha256 = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $expectedSha256) {
        throw "ExifTool checksum mismatch. Expected $expectedSha256 but received $actualSha256."
    }

    Expand-Archive -Path $archive -DestinationPath $temporaryDirectory
    $sourceDirectory = Join-Path $temporaryDirectory "exiftool-$($version)_64"

    New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
    $existingPaths = @(
        (Join-Path $targetDirectory "exiftool.exe")
        (Join-Path $targetDirectory "exiftool_files")
        (Join-Path $targetDirectory "ExifTool-README.txt")
    )
    Remove-Item -Path $existingPaths -Recurse -Force -ErrorAction SilentlyContinue

    Copy-Item (Join-Path $sourceDirectory "exiftool(-k).exe") (Join-Path $targetDirectory "exiftool.exe")
    Copy-Item -Recurse (Join-Path $sourceDirectory "exiftool_files") $targetDirectory
    Copy-Item (Join-Path $sourceDirectory "README.txt") (Join-Path $targetDirectory "ExifTool-README.txt")

    & (Join-Path $targetDirectory "exiftool.exe") -ver
    if ($LASTEXITCODE -ne 0) {
        throw "The downloaded ExifTool executable failed its version check."
    }
}
finally {
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $temporaryDirectory
}
