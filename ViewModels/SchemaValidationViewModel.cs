using System;
using System.IO;
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
    
    [ObservableProperty]
    private string _fileName;
        
    [ObservableProperty]
    private bool _isFileNameVisible;
        
    [ObservableProperty]
    private string _validationResultText;
        
    [ObservableProperty]
    private bool _isValidationSuccess;
        
    [ObservableProperty]
    private bool _isLoading;

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
            ValidationResultText = 
                $"✅ Файл прошел валидацию успешно!\n\n" +
                $"📄 Используемая схема: {result.SchemaFileName}\n" +
                $"📋 Версия схемы: {result.SchemaVersion ?? "не указана"}";
        }
        else
        {
            var errorText = $"❌ Валидация не пройдена\n\n";

            if (!string.IsNullOrEmpty(result.SchemaFileName))
            {
                errorText += $"📄 Проверка по схеме: {result.SchemaFileName}\n";
                errorText += $"📋 Версия: {result.SchemaVersion ?? "не указана"}\n\n";
            }

            errorText += $"🔍 Описание проблемы:\n{result.ErrorMessage}";

            ValidationResultText = errorText;
        }
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