# CIDE 🚀

CIDE — это современная, быстрая и стильная интегрированная среда разработки (IDE) на базе Avalonia, предназначенная для C/C++ и C#. Она предлагает кроссплатформенный пользовательский интерфейс с невероятным быстродействием для максимальной продуктивности разработчика.

## 🔥 Особенности
- **Кроссплатформенность**: Построен с использованием Avalonia UI для плавного и красивого интерфейса (Windows, macOS, Linux).
- **Ориентация на C/C++ и C#**: Встроенные профили терминала, шаблоны .slnx и подготовка для глубокой работы с кодом.
- **Быстрый и Легкий**: Создан для максимальной производительности с минимальным потреблением ресурсов в отличие от "тяжелых" IDE.
- **Кинематографичный Дизайн**: Плавные `CrossFade` переходы, Zen Mode (F11), красивое размытие и прозрачность.
- **Удобный инсталлятор**: CIDE поставляется с полноценным Inno Setup инсталлятором, который автоматически настраивает контекстное меню и переменные среды.
- **Продвинутый Терминал**: Встроенный терминал (PowerShell, CMD, WSL, SSH) с защитой от закрытия вкладок.
- **Мощный Файловый Менеджер**: Контекстное меню, тулбар для создания/удаления файлов и горячие клавиши.
- **Безопасная Компиляция**: Кнопка **STOP** для принудительной остановки зависших программ (например, при бесконечном цикле). Умный скролл и защита от утечки памяти при огромных логах.

---

## 🚀 Установка (В одну строку)

Вы можете установить CIDE мгновенно на Windows, просто запустив скрипт в PowerShell. 
Этот скрипт сам скачает последнюю версию программы, добавит её в системный путь (`PATH`) и создаст пункт **«Открыть в CIDE»** в контекстном меню Windows (по правому клику).

Откройте **PowerShell** и вставьте эту строку:

```powershell
New-Item -ItemType Directory -Force -Path C:\cide; Invoke-WebRequest -Uri "https://github.com/irovbyte/CIDE/releases/latest/download/CIDE.exe" -OutFile C:\cide\CIDE.exe; $p = [Environment]::GetEnvironmentVariable("PATH","User"); if($p -notlike "*C:\cide*"){ [Environment]::SetEnvironmentVariable("PATH", "$p;C:\cide", "User") }; $k1="HKCU:\Software\Classes\Directory\shell\CIDE"; $k2="HKCU:\Software\Classes\Directory\Background\shell\CIDE"; foreach($k in $k1,$k2){ New-Item -Path $k -Force | Out-Null; New-ItemProperty -Path $k -Name "(Default)" -Value "Открыть в CIDE" -Force | Out-Null; New-ItemProperty -Path $k -Name "Icon" -Value "C:\cide\CIDE.exe" -Force | Out-Null; New-Item -Path "$k\command" -Force | Out-Null; New-ItemProperty -Path "$k\command" -Name "(Default)" -Value "`"C:\cide\CIDE.exe`" `"%V`"" -Force | Out-Null }; Start-Process -FilePath "C:\cide\CIDE.exe"
```
*(После выполнения вы сможете писать команду `cide .` в терминале, а также кликать правой кнопкой мыши по любой папке и выбирать "Open with CIDE").*

---

## 🐧 Для Linux и Windows Subsystem for Linux (WSL)

CIDE обладает **умной интеграцией с WSL**. Если вы запустите `cide .` внутри WSL, он автоматически откроет Windows-версию IDE, подключенную к файловой системе Linux!

Откройте терминал (Linux или WSL) и выполните установку:

```bash
sudo mkdir -p /opt/cide && \
sudo wget -O /opt/cide/CIDE "https://github.com/irovbyte/CIDE/releases/latest/download/CIDE" && \
sudo chmod +x /opt/cide/CIDE && \
echo '#!/bin/bash
if grep -qE "(Microsoft|WSL)" /proc/version &> /dev/null; then
    WIN_PATH=$(wslpath -w "$1" 2>/dev/null || wslpath -w ".")
    /mnt/c/cide/CIDE.exe "$WIN_PATH" --wsl &
else
    /opt/cide/CIDE "$@" &
fi' | sudo tee /usr/local/bin/cide > /dev/null && \
sudo chmod +x /usr/local/bin/cide && \
cide .
```

---

## 🛠 Сборка из исходного кода
Для локальной сборки CIDE вам понадобится **.NET 10 SDK** (или новее).

```bash
git clone https://github.com/irovbyte/CIDE.git
cd CIDE
dotnet build -c Release
```
Если вы хотите собрать единый standalone `.exe` вручную:
```bash
dotnet publish CIDE.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o output
```

## 📜 Лицензия
Проект распространяется под лицензией MIT. Подробнее см. в файле LICENSE.txt.

