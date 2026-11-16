# Установка / обновление LangSwitch на Windows 10/11 одной командой (PowerShell):
#   irm https://raw.githubusercontent.com/PavelB-22/langswitch/main/install.ps1 | iex
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$url = 'https://github.com/PavelB-22/langswitch/releases/latest/download/LangSwitch-windows.zip'
$dir = Join-Path $env:LOCALAPPDATA 'Programs\LangSwitch'
$zip = Join-Path $env:TEMP 'LangSwitch-windows.zip'

Write-Host '> Скачиваю LangSwitch...'
Invoke-WebRequest $url -OutFile $zip -UseBasicParsing

Get-Process LangSwitch -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Write-Host '> Устанавливаю...'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
Expand-Archive -Path $zip -DestinationPath $dir -Force
Remove-Item $zip -ErrorAction SilentlyContinue
$exe = Join-Path $dir 'LangSwitch.exe'
Unblock-File $exe -ErrorAction SilentlyContinue

# Ярлык в меню «Пуск»
$lnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'LangSwitch.lnk'
$sh = New-Object -ComObject WScript.Shell
$s = $sh.CreateShortcut($lnk)
$s.TargetPath = $exe
$s.WorkingDirectory = $dir
$s.Description = 'Бесплатный переключатель раскладки'
$s.Save()

# Автозапуск вместе с Windows
Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'LangSwitch' -Value "`"$exe`""

Start-Process $exe
Write-Host ''
Write-Host 'Готово! LangSwitch запущен — значок RU/EN у часов (если не видно, нажми стрелочку ^ рядом с часами).'
Write-Host 'Двойной Shift — сменить язык. «ghbdtn» + пробел само станет «привет».'
Write-Host 'Если стоит Punto Switcher или Caramba — выключи их, чтобы не конфликтовали.'
