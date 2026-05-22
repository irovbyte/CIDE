# CIDE

**CIDE** — это современная, быстрая и стильная интегрированная среда разработки (IDE) на базе Avalonia, предназначенная для C/C++ и C#. Она предлагает кроссплатформенный пользовательский интерфейс с невероятным быстродействием для максимальной продуктивности разработчика.

## 🔥 Особенности

- **Кроссплатформенность:** Построен с использованием Avalonia UI для плавного и красивого интерфейса.
- **Ориентация на C/C++ и C#:** Встроенные профили терминала, шаблоны `.slnx` и подготовка для глубокой работы с кодом.
- **Быстрый и Легкий:** Создан для максимальной производительности с минимальным потреблением ресурсов в отличие от "тяжелых" IDE.
- **Единый исполняемый файл (Single Executable):** CIDE распространяется как один самодостаточный `.exe` файл, который обновляет сам себя.

---

## 🚀 Установка (В одну строку)

Вы можете установить CIDE мгновенно на Windows, просто запустив скрипт в PowerShell. 
Этот скрипт сам скачает последнюю версию программы, поместит её в `C:\cide` и добавит в системный путь (`PATH`).

Откройте **PowerShell** и вставьте эту строку:

```powershell
New-Item -ItemType Directory -Force -Path C:\cide; Invoke-WebRequest -Uri "https://github.com/irovbyte/CIDE/releases/latest/download/CIDE.exe" -OutFile C:\cide\CIDE.exe; $p = [Environment]::GetEnvironmentVariable("PATH","User"); if($p -notlike "*C:\cide*"){ [Environment]::SetEnvironmentVariable("PATH", "$p;C:\cide", "User") }
```

После выполнения скрипта вы сможете просто написать команду `cide .` в любой папке, чтобы открыть IDE прямо в этой директории! *(Примечание: может потребоваться перезапуск терминала для применения изменений `PATH`).*

### 🐧 Для Linux (Bash)

Откройте терминал и выполните:

```bash
sudo mkdir -p /opt/cide && sudo wget -O /opt/cide/CIDE "https://github.com/irovbyte/CIDE/releases/latest/download/CIDE" && sudo chmod +x /opt/cide/CIDE && sudo ln -sf /opt/cide/CIDE /usr/local/bin/cide
```

Теперь `cide` доступен глобально из любого места!

---

## 🛠 Сборка из исходного кода

Для локальной сборки CIDE вам понадобится [.NET 10 SDK](https://dotnet.microsoft.com/).

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

Проект распространяется под лицензией MIT. Подробнее см. в файле [LICENSE.txt](LICENSE.txt).
