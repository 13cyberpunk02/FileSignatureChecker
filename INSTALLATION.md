# Инструкция по установке и запуску

## Требования к системе

- Windows 10 версии 1809 или выше / Windows 11
- .NET 8.0 SDK или Runtime
- Минимум 4 ГБ RAM
- 100 МБ свободного места на диске

## Установка .NET 8.0

### Вариант 1: Установка .NET SDK (для разработчиков)

1. Перейдите на официальный сайт: https://dotnet.microsoft.com/download/dotnet/8.0
2. Скачайте ".NET SDK 8.0" для Windows
3. Запустите установщик и следуйте инструкциям
4. После установки проверьте версию в командной строке:
   ```
   dotnet --version
   ```

### Вариант 2: Установка .NET Runtime (для пользователей)

1. Перейдите на официальный сайт: https://dotnet.microsoft.com/download/dotnet/8.0
2. Скачайте ".NET Desktop Runtime 8.0" для Windows
3. Запустите установщик и следуйте инструкциям

## Установка приложения

### Способ 1: Сборка из исходного кода

1. Распакуйте архив с проектом или клонируйте репозиторий
2. Откройте командную строку в папке проекта
3. Выполните команду для восстановления зависимостей:
   ```
   dotnet restore
   ```
4. Соберите проект:
   ```
   dotnet build --configuration Release
   ```
5. Запустите приложение:
   ```
   dotnet run --configuration Release
   ```

### Способ 2: Запуск через Visual Studio

1. Установите Visual Studio 2022 Community (бесплатно): https://visualstudio.microsoft.com/
2. При установке выберите рабочую нагрузку ".NET desktop development"
3. Откройте файл `FileSignatureChecker.csproj` в Visual Studio
4. Дождитесь восстановления пакетов NuGet (происходит автоматически)
5. Нажмите F5 или кнопку "Start" для запуска

### Способ 3: Запуск через Rider

1. Установите JetBrains Rider: https://www.jetbrains.com/rider/
2. Откройте файл `FileSignatureChecker.csproj` в Rider
3. Дождитесь индексации и восстановления пакетов
4. Нажмите Shift+F10 или кнопку "Run" для запуска

## Создание исполняемого файла (exe)

Для создания standalone приложения, которое не требует установки .NET:

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

Исполняемый файл будет создан в папке:
```
bin\Release\net8.0-windows\win-x64\publish\
```

## Возможные проблемы и решения

### Ошибка: "SDK not found"

**Решение:** Установите .NET 8.0 SDK с официального сайта Microsoft.

### Ошибка: "NuGet packages not restored"

**Решение:** 
1. Удалите папку `bin` и `obj`
2. Выполните команду:
   ```
   dotnet restore --force
   ```

### Ошибка: "MaterialDesignThemes не найден"

**Решение:**
1. Проверьте подключение к интернету
2. Выполните команду:
   ```
   dotnet add package MaterialDesignThemes --version 4.9.0
   dotnet add package MaterialDesignColors --version 2.1.4
   ```

### Приложение не запускается

**Решение:**
1. Убедитесь, что установлен .NET 8.0 Runtime или SDK
2. Проверьте, что у вас установлена последняя версия Windows 10/11
3. Попробуйте запустить от имени администратора

### OpenFolderDialog не работает

**Решение:** 
Если вы используете .NET 7 или ниже, замените `OpenFolderDialog` на `FolderBrowserDialog` в файле `MainViewModel.cs`:

```csharp
using System.Windows.Forms;

[RelayCommand]
private void SelectDirectory()
{
    using var dialog = new FolderBrowserDialog
    {
        Description = "Выберите директорию с файлами",
        ShowNewFolderButton = false
    };

    if (dialog.ShowDialog() == DialogResult.OK)
    {
        DirectoryPath = dialog.SelectedPath;
    }
}
```

И добавьте ссылку в .csproj:
```xml
<ItemGroup>
  <PackageReference Include="System.Windows.Forms" Version="8.0.0" />
</ItemGroup>
```

## Первый запуск

1. Запустите приложение
2. Нажмите "Обзор" для выбора XML файла (можете использовать `example.xml`)
3. Нажмите "Обзор" для выбора директории с файлами
4. Нажмите "Выполнить проверку"
5. Просмотрите результаты в таблице

## Обновление приложения

1. Получите новую версию исходного кода
2. Выполните команду:
   ```
   dotnet clean
   dotnet restore
   dotnet build --configuration Release
   ```

## Удаление приложения

1. Удалите папку с проектом
2. При необходимости удалите .NET Runtime через "Программы и компоненты"

## Техническая поддержка

При возникновении проблем:
1. Проверьте логи в папке приложения
2. Убедитесь, что у вас последняя версия .NET 8.0
3. Проверьте права доступа к файлам и папкам
4. Убедитесь, что XML файл имеет правильную структуру

## Системные требования для разработки

- Visual Studio 2022 17.8 или выше / Rider 2023.3 или выше
- .NET 8.0 SDK
- Git (опционально)
- Windows 10 SDK (устанавливается с Visual Studio)
