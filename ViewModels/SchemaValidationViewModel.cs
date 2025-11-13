using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileSignatureChecker.Models;
using FileSignatureChecker.Services;
using Microsoft.Win32;

namespace FileSignatureChecker.ViewModels;

/// <summary>
/// ViewModel для окна валидации XML/GGE файлов
/// Наследуется от ObservableObject - это базовый класс из CommunityToolkit.Mvvm
/// который реализует INotifyPropertyChanged
/// </summary>
public partial class SchemaValidationViewModel : ObservableObject
{
    private readonly XmlValidationService _validationService;

    [ObservableProperty] private string _fileName;

    [ObservableProperty] private bool _isFileNameVisible;

    [ObservableProperty] private string _validationResultText;

    [ObservableProperty] private string _schemaFileName;

    [ObservableProperty] private string _schemaVersion;

    [ObservableProperty] private string _errorLocation;

    [ObservableProperty] private string _errorPath;

    [ObservableProperty] private string _errorDescription;

    [ObservableProperty] private string _errorDetails;

    [ObservableProperty] private string _currentValue;

    [ObservableProperty] private bool _isValidationSuccess;

    [ObservableProperty] private bool _isLoading;

    public SchemaValidationViewModel()
    {
        _validationService = new XmlValidationService();
        ValidationResultText = "Выберите XML или GGE файл для валидации.";
        IsFileNameVisible = false;
        IsValidationSuccess = false;
        IsLoading = false;
    }

    /// <summary>
    /// Команда загрузки файла
    /// [RelayCommand] автоматически создает свойство LoadFileCommand типа IAsyncRelayCommand
    /// Это свойство можно биндить к кнопке: Command="{Binding LoadFileCommand}"
    /// </summary>
    [RelayCommand]
    private async Task LoadFileAsync()
    {
        // Открываем диалог выбора файла
        var openFileDialog = new OpenFileDialog()
        {
            Filter = "XML и GGE файлы (*.xml;*.gge)|*.xml;*.gge|XML файлы (*.xml)|*.xml|GGE файлы (*.gge)|*.gge",
            Title = "Выберите файл для валидации"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            await ValidateFileAsync(openFileDialog.FileName);
        }
    }

    /// <summary>
    /// Валидирует выбранный файл
    /// </summary>
    private async Task ValidateFileAsync(string filePath)
    {
        try
        {
            IsLoading = true;

            FileName = $"Файл: {filePath}";
            IsFileNameVisible = true;

            ValidationResultText = "⏳ Идет проверка файла...";
            IsValidationSuccess = false;

            var result = await _validationService.ValidateFileAsync(filePath);

            DisplayValidationResult(result);
        }
        catch (Exception ex)
        {
            ValidationResultText = $"❌ Ошибка при проверке файла:\n\n{ex.Message}";
            IsValidationSuccess = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Отображает результат валидации
    /// </summary>
    private void DisplayValidationResult(ValidationResult result)
    {
        IsValidationSuccess = result.IsValid;

        if (result.IsValid)
        {
            ValidationResultText = "Файл прошел валидацию успешно!";
            SchemaFileName = result.SchemaFileName;
            SchemaVersion = result.SchemaVersion ?? "не указана";

            ErrorLocation = null;
            ErrorPath = null;
            ErrorDescription = null;
            ErrorDetails = null;
            CurrentValue = null;
        }
        else
        {
            SchemaFileName = result.SchemaFileName;
            SchemaVersion = result.SchemaVersion ?? "не указана";
            ValidationResultText = $"Проверка по схеме: {SchemaFileName} (версия {SchemaVersion})";

            ParseErrorMessage(result.ErrorMessage);
        }
    }

    private void ParseErrorMessage(string errorMessage)
    {
        var lines = errorMessage.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var locationBuilder = new StringBuilder();
        var pathBuilder = new StringBuilder();
        var descriptionBuilder = new StringBuilder();
        var detailsBuilder = new StringBuilder();
        var valueBuilder = new StringBuilder();

        var currentSection = "";

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Пропускаем заголовки
            if (trimmedLine.StartsWith("═══")) continue;
            if (trimmedLine.StartsWith("Найдены")) continue;

            // Определяем секцию
            if (trimmedLine.Contains("📍") && trimmedLine.Contains("Расположение"))
            {
                currentSection = "location";
                continue;
            }
            else if (trimmedLine.Contains("📂") && trimmedLine.Contains("Путь"))
            {
                currentSection = "path";
                continue;
            }
            else if (trimmedLine.Contains("❌") && trimmedLine.Contains("Описание"))
            {
                currentSection = "description";
                continue;
            }
            else if (trimmedLine.Contains("⚙️") && trimmedLine.Contains("Требования"))
            {
                currentSection = "details";
                continue;
            }
            else if (trimmedLine.Contains("💡"))
            {
                currentSection = "value";
                var match = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"['\""](.+?)['\""']");
                if (match.Success)
                {
                    valueBuilder.Append(match.Groups[1].Value);
                }

                continue;
            }

            // Добавляем в секцию с ПЕРЕНОСАМИ СТРОК
            switch (currentSection)
            {
                case "location":
                    if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        if (locationBuilder.Length > 0) locationBuilder.AppendLine();
                        locationBuilder.Append(trimmedLine);
                    }

                    break;
                case "path":
                    if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        var cleanLine = trimmedLine.Replace("→", "").Trim();
                        if (!string.IsNullOrWhiteSpace(cleanLine))
                        {
                            if (pathBuilder.Length > 0) pathBuilder.AppendLine();
                            pathBuilder.Append("→ " + cleanLine);
                        }
                    }

                    break;
                case "description":
                    if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        if (descriptionBuilder.Length > 0) descriptionBuilder.AppendLine();
                        descriptionBuilder.Append(trimmedLine);
                    }

                    break;
                case "details":
                    if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        if (detailsBuilder.Length > 0) detailsBuilder.AppendLine();
                        detailsBuilder.Append(trimmedLine);
                    }

                    break;
            }
        }

        ErrorLocation = locationBuilder.Length > 0 ? locationBuilder.ToString() : null;
        ErrorPath = pathBuilder.Length > 0 ? pathBuilder.ToString() : null;
        ErrorDescription = descriptionBuilder.Length > 0 ? descriptionBuilder.ToString() : null;
        ErrorDetails = detailsBuilder.Length > 0 ? detailsBuilder.ToString() : null;
        CurrentValue = valueBuilder.Length > 0 ? valueBuilder.ToString() : null;
    }

    /// <summary>
    /// Команда закрытия окна
    /// [RelayCommand] создает свойство CloseCommand
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Событие для закрытия окна
    /// View подписывается на это событие
    /// </summary>
    public event EventHandler CloseRequested;
}