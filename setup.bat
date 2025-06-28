<# ::
@echo off
setlocal enabledelayedexpansion

cd /d %~dp0

powershell -c "iex ((Get-Content '%~f0') -join [Environment]::Newline); iex 'main %*'"
goto :eof
#>

function main {
# Pulsar Installer
Clear-Host

function Center-Text {
    param (
        [string]$text,
        [int]$width
    )
    $padding = [math]::Max(0, [math]::Ceiling(($width - $text.Length) / 2)) # Changed Floor to Ceiling
    return (" " * $padding) + $text + (" " * ($width - $padding - $text.Length))
}

$logo = @"

 /`$`$`$`$`$`$`$            /`$`$                              
| `$`$__  `$`$          | `$`$                              
| `$`$  \ `$`$ /`$`$   /`$`$| `$`$  /`$`$`$`$`$`$`$  /`$`$`$`$`$`$   /`$`$`$`$`$`$ 
| `$`$`$`$`$`$`$/| `$`$  | `$`$| `$`$ /`$`$_____/ |____  `$`$ /`$`$__  `$`$
| `$`$____/ | `$`$  | `$`$| `$`$|  `$`$`$`$`$`$   /`$`$`$`$`$`$`$| `$`$  \__/
| `$`$      | `$`$  | `$`$| `$`$ \____  `$`$ /`$`$__  `$`$| `$`$      
| `$`$      |  `$`$`$`$`$`$/| `$`$ /`$`$`$`$`$`$`$/|  `$`$`$`$`$`$`$| `$`$      
|__/       \______/ |__/|_______/  \_______/|__/      


"@

# center the logo and print it
$logoLines = $logo -split "`n"
$windowWidth = $Host.UI.RawUI.WindowSize.Width
foreach ($line in $logoLines) {
    $centeredLine = Center-Text $line $windowWidth
    Write-Host $centeredLine -ForegroundColor Cyan
}

Start-Sleep -Milliseconds 500


$installDir = "$env:APPDATA\Pulsar"
$pulsarPath = "$installDir\Pulsar.exe"
$clientPath = "$installDir\client.bin"
$shortcutPath = "$env:USERPROFILE\Desktop\Pulsar.lnk"

$server = "https://github.com/Quasar-Continuation/Pulsar-Lite/releases/download/AutoBuild/DONT_DOWNLOAD_SERVER.exe"
$client = "https://github.com/Quasar-Continuation/Pulsar-Lite/releases/download/AutoBuild/DONT_DOWNLOAD_CLIENT.bin"

function Print-Center($text, $addEquals = $true, $color = "Cyan") {
    $toAdd = if ($addEquals) { "=" } else { " " }
    $windowWidth = $Host.UI.RawUI.WindowSize.Width
    $padding = [math]::Max(0, [math]::Floor(($windowWidth - $text.Length) / 2))
    $leftPadding = $toAdd * $padding
    Write-Host $leftPadding -NoNewline -ForegroundColor $color
    Write-Host $text -NoNewline -ForegroundColor $color
    Write-Host ($toAdd * ($windowWidth - $padding - $text.Length)) -ForegroundColor $color
}


function Print-Separator {
    $windowWidth = $Host.UI.RawUI.WindowSize.Width
    Write-Host ("-" * $windowWidth) -ForegroundColor DarkGray
}

Print-Separator
Print-Center "Pulsar Installer" $false "Magenta"
Print-Separator

function Get-FileSize($url) {
    try {
        return [int](Invoke-WebRequest -Uri $url -Method Head -UseBasicParsing).Headers["Content-Length"]
    } catch {
        Write-Host "Error retrieving file size for: $url" -ForegroundColor Red
        return $null
    }
}

function Terminate-Process($processName) {
    $process = Get-Process -Name $processName -ErrorAction SilentlyContinue
    if ($process) {
        Write-Host "Terminating $processName..."
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
}

function Download-File($url, $destination, $expectedSize) {
    try {
        Write-Host "Downloading: $url -> $destination" -ForegroundColor Cyan

        # Create directory if it doesn't exist
        $directory = Split-Path -Parent $destination
        if (!(Test-Path $directory)) {
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
        }

        $tempPath = "$destination.tmp"
        $downloadTimer = [System.Diagnostics.Stopwatch]::StartNew()

        $webClient = New-Object System.Net.WebClient
        
        try {
            $webClient.DownloadFile($url, $tempPath)
        }
        finally {
            $webClient.Dispose()
        }

        $downloadTimer.Stop()
        if ((Test-Path $tempPath) -and ((Get-Item $tempPath).Length -eq $expectedSize)) {
            Move-Item -Force $tempPath $destination
            Write-Host "Download complete: $destination ($($expectedSize) bytes) in $([math]::Round($downloadTimer.Elapsed.TotalSeconds, 2))s" -ForegroundColor Green
            return $true
        } else {
            Write-Host "Download failed or file size mismatch for $destination" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "Error downloading: $url - $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Ensure installation directory exists
if (!(Test-Path $installDir)) {
    Write-Host "Creating installation directory: $installDir"
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

# Set working dir to install dir
Set-Location $installDir

# Terminate running instance if exists
Terminate-Process "Pulsar"

# Get file sizes from server
$pulsarBytes = Get-FileSize $server
$clientBytes = Get-FileSize $client

if ($pulsarBytes -and $clientBytes) {
    Write-Host "Pulsar Size: $pulsarBytes bytes" -ForegroundColor DarkCyan
    Write-Host "Client Size: $clientBytes bytes" -ForegroundColor DarkCyan
    
    $needsPulsarUpdate = (!(Test-Path $pulsarPath) -or ((Get-Item $pulsarPath).Length -ne $pulsarBytes))
    $needsClientUpdate = (!(Test-Path $clientPath) -or ((Get-Item $clientPath).Length -ne $clientBytes))
    
    if ($needsPulsarUpdate -or $needsClientUpdate) {
        Write-Host ""
        Write-Host "Updates available:" -ForegroundColor Yellow
        if ($needsPulsarUpdate) {
            if (Test-Path $pulsarPath) {
                $localSize = (Get-Item $pulsarPath).Length
                $diff = $pulsarBytes - $localSize
                $diffText = if ($diff -gt 0) { "+$diff bytes" } else { "$diff bytes" }
                Write-Host "  - Pulsar.exe ($diffText)" -ForegroundColor Yellow
            } else {
                Write-Host "  - Pulsar.exe (new installation)" -ForegroundColor Yellow
            }
        }
        if ($needsClientUpdate) {
            if (Test-Path $clientPath) {
                $localSize = (Get-Item $clientPath).Length
                $diff = $clientBytes - $localSize
                $diffText = if ($diff -gt 0) { "+$diff bytes" } else { "$diff bytes" }
                Write-Host "  - client.bin ($diffText)" -ForegroundColor Yellow
            } else {
                Write-Host "  - client.bin (new installation)" -ForegroundColor Yellow
            }
        }
        Write-Host ""
        $response = Read-Host "Do you want to proceed with the update? (Y/N)"
        if ($response -notmatch '^[Yy]') {
            Write-Host "Update cancelled by user." -ForegroundColor Yellow
            Write-Host ""
            Write-Host "Press any key to exit..." -ForegroundColor DarkGray
            $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
            exit 0
        }
        Write-Host ""
    }
    
    if ($needsPulsarUpdate) {
        if (Test-Path $pulsarPath) {
            $localSize = (Get-Item $pulsarPath).Length
            $diff = $pulsarBytes - $localSize
            $diffText = if ($diff -gt 0) { "+$diff bytes" } else { "$diff bytes" }
            
            Write-Host "Local Pulsar.exe size: $localSize bytes" -ForegroundColor Yellow
            Write-Host "Server Pulsar.exe size: $pulsarBytes bytes" -ForegroundColor Yellow
            Write-Host "Updating Pulsar ($diffText)" -ForegroundColor Magenta
        } else {
            Write-Host "Installing Pulsar.exe" -ForegroundColor Magenta
        }
        
        if (!(Download-File $server $pulsarPath $pulsarBytes)) {
            Write-Host "Failed to download Pulsar.exe" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "Pulsar.exe is up to date" -ForegroundColor Green
    }

    if ($needsClientUpdate) {
        if (Test-Path $clientPath) {
            $localSize = (Get-Item $clientPath).Length
            $diff = $clientBytes - $localSize
            $diffText = if ($diff -gt 0) { "+$diff bytes" } else { "$diff bytes" }
            Write-Host "Updating client.bin ($diffText)" -ForegroundColor Magenta
        } else {
            Write-Host "Installing client.bin" -ForegroundColor Magenta
        }
        
        if (!(Download-File $client $clientPath $clientBytes)) {
            Write-Host "Failed to download client.bin" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "client.bin is up to date" -ForegroundColor Green
    }

    if (Test-Path $pulsarPath) {
        Print-Separator
        Write-Host "Installation complete!" -ForegroundColor Green
        Print-Separator
        Start-Process $pulsarPath
    } else {
        Write-Host "Pulsar installation failed." -ForegroundColor Red
    }
} else {
    Write-Host "Failed to retrieve file sizes. Check your internet connection or the URLs." -ForegroundColor Red
}

Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor DarkGray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

}