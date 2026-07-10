<div align="center">

# Steam Epic Sync

[![Build & Release](https://github.com/linskay/steam-plagin-import/actions/workflows/release.yml/badge.svg)](https://github.com/linskay/steam-plagin-import/actions/workflows/release.yml)
[![GitHub release (latest by date)](https://img.shields.io/github/v/release/linskay/steam-plagin-import?color=00C853&logo=github&style=flat-square)](https://github.com/linskay/steam-plagin-import/releases)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D7.svg?logo=windows&style=flat-square)](https://www.microsoft.com/windows)
[![Language](https://img.shields.io/badge/language-C%23%205.0-green.svg?logo=c-sharp&style=flat-square)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/github/license/linskay/steam-plagin-import?color=orange&style=flat-square)](LICENSE)

**Легковесная C# утилита для автоматической синхронизации библиотеки Epic Games Store со Steam и оптимизации фоновых ресурсов.**

[Скачать последнюю версию](https://github.com/linskay/steam-plagin-import/releases/latest) • [Инструкция](#установка-и-использование) • [Сборка](#сборка-из-исходников)

</div>

---

## 🚀 Основные возможности

* **🔄 Автоматический импорт**: Находит установленные игры Epic Games и добавляет их в Steam в качестве сторонних игр (с оригинальными иконками и вычислением корректного Steam AppID для поддержки оверлея).
* **🧠 Умное управление ресурсами**: Запускает Epic Games Launcher в фоновом/тихом режиме только на время сессии. После закрытия игры полностью завершает все фоновые процессы Epic (`EpicGamesLauncher.exe`, `EpicWebHelper.exe`), освобождая оперативную память и процессорное время.
* **🎨 Современный интерфейс**: Панель управления на WPF с темной темой и полупрозрачным дизайном (эффект акрила/стекла) для удобной настройки и мониторинга.
* **⚡ Низкое потребление ресурсов**: Утилита написана на нативном C# (.NET Framework 4.X), работает без тяжелых внешних зависимостей и расходует всего 5–15 МБ ОЗУ в фоновом режиме.
* **📥 Работа из трея**: Удобное меню в системном трее для быстрой синхронизации, настройки автозапуска с Windows и открытия дашборда.

---

## 📦 Установка и использование

> [!IMPORTANT]
> Для корректной работы программы файлы `SteamEpicSync.exe` и `MainWindow.xaml` должны находиться в одной папке. Всегда скачивайте архив целиком.

1. Перейдите в раздел **[Releases](https://github.com/linskay/steam-plagin-import/releases/latest)** и скачайте архив `SteamEpicSync.zip`.
2. Распакуйте его в любую постоянную папку на компьютере.
3. Запустите `SteamEpicSync.exe`.
4. Нажмите кнопку **Синхронизировать сейчас** в интерфейсе панели (или выберите этот пункт в меню трея).
5. Полностью перезапустите Steam. Игры из Epic Games Store появятся в вашей библиотеке Steam.

---

## 💻 Параметры командной строки (CLI)

Утилита поддерживает автоматизацию через аргументы запуска:

| Аргумент | Описание |
| :--- | :--- |
| `--sync-silent` | Выполняет синхронизацию библиотеки в фоновом режиме без открытия окон и завершает работу. |
| `--launch "<AppName>"` | Запускает указанную игру Epic Games, отслеживает её состояние и закрывает EGS после выхода из игры. |

---

## 🛠️ Сборка из исходников

Для компиляции проекта на Windows не требуется установка сторонних SDK или компиляторов. Сборка выполняется стандартным компилятором .NET:

1. Откройте терминал в папке проекта.
2. Запустите скрипт компиляции:
   ```cmd
   build.bat
   ```

Скрипт скомпилирует исходный код в исполняемый файл `SteamEpicSync.exe` в корневой папке проекта.
