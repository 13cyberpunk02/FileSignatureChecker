using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ClosedXML.Excel;
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
    [ObservableProperty] private bool _isValidationSuccess;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _errorCount;

    // Для множественных ошибок
    [ObservableProperty] private ObservableCollection<ValidationError> _errors;
    [ObservableProperty] private ObservableCollection<ValidationError> _filteredErrors;
    [ObservableProperty] private string _searchText;
    [ObservableProperty] private bool _hasErrors;

    public SchemaValidationViewModel()
    {
        _validationService = new XmlValidationService();
        ValidationResultText = "Выберите XML или GGE файл для валидации.";
        IsFileNameVisible = false;
        IsValidationSuccess = false;
        IsLoading = false;
        Errors = new ObservableCollection<ValidationError>();
        FilteredErrors = new ObservableCollection<ValidationError>();
        HasErrors = false;
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterErrors();
    }

    private void FilterErrors()
    {
        if (Errors == null || Errors.Count == 0)
        {
            FilteredErrors = new ObservableCollection<ValidationError>();
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredErrors = new ObservableCollection<ValidationError>(Errors);
        }
        else
        {
            var filtered = Errors.Where(e =>
                (e.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true) ||
                (e.Location?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true) ||
                (e.Path?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true) ||
                (e.Details?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true) ||
                (e.CurrentValue?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true)
            );
            FilteredErrors = new ObservableCollection<ValidationError>(filtered);
        }
    }

    [RelayCommand]
    private async Task LoadFileAsync()
    {
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

    private async Task ValidateFileAsync(string filePath)
    {
        try
        {
            IsLoading = true;
            FileName = $"Файл: {filePath}";
            IsFileNameVisible = true;
            ValidationResultText = "⏳ Идет проверка файла...";
            IsValidationSuccess = false;
            HasErrors = false;

            var result = await _validationService.ValidateFileAsync(filePath);
            DisplayValidationResult(result);
        }
        catch (Exception ex)
        {
            ValidationResultText = $"❌ Ошибка при проверке файла:\n\n{ex.Message}";
            IsValidationSuccess = false;
            HasErrors = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void DisplayValidationResult(ValidationResult result)
    {
        IsValidationSuccess = result.IsValid;
        SchemaFileName = result.SchemaFileName;
        SchemaVersion = result.SchemaVersion ?? "не указана";

        if (result.IsValid)
        {
            ValidationResultText = "Файл прошел валидацию успешно!";
            Errors.Clear();
            FilteredErrors.Clear();
            HasErrors = false;
            ErrorCount = 0;  // ← ДОБАВЬ
        }
        else
        {
            ValidationResultText = $"Валидация не пройдена";
        
            Errors.Clear();
            foreach (var error in result.Errors)
            {
                Errors.Add(error);
            }
        
            FilterErrors();
            HasErrors = Errors.Count > 0;
            ErrorCount = Errors.Count; 
        }
    }

    [RelayCommand]
    private void ExportToExcel()
    {
        if (Errors == null || Errors.Count == 0)
        {
            MessageBox.Show("Нет ошибок для экспорта.", "Информация",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Excel файлы (*.xlsx)|*.xlsx",
            FileName = $"Ошибки_валидации_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx",
            Title = "Сохранить отчет об ошибках"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Ошибки валидации");

                // Заголовки
                worksheet.Cell(1, 1).Value = "№";
                worksheet.Cell(1, 2).Value = "Расположение";
                worksheet.Cell(1, 3).Value = "Путь";
                worksheet.Cell(1, 4).Value = "Описание";
                worksheet.Cell(1, 5).Value = "Требования";
                worksheet.Cell(1, 6).Value = "Текущее значение";

                // Стиль заголовков
                var headerRange = worksheet.Range(1, 1, 1, 6);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Данные
                for (int i = 0; i < Errors.Count; i++)
                {
                    var error = Errors[i];
                    var row = i + 2;

                    worksheet.Cell(row, 1).Value = error.ErrorNumber;
                    worksheet.Cell(row, 2).Value = error.Location ?? "";
                    worksheet.Cell(row, 3).Value = error.Path ?? "";
                    worksheet.Cell(row, 4).Value = error.Description ?? "";
                    worksheet.Cell(row, 5).Value = error.Details ?? "";
                    worksheet.Cell(row, 6).Value = error.CurrentValue ?? "";

                    // Перенос текста в ячейках
                    worksheet.Row(row).Style.Alignment.WrapText = true;
                }

                // Автоподбор ширины колонок
                worksheet.Columns().AdjustToContents();

                // Ограничиваем максимальную ширину
                foreach (var column in worksheet.ColumnsUsed())
                {
                    if (column.Width > 50)
                        column.Width = 50;
                }

                workbook.SaveAs(saveFileDialog.FileName);

                MessageBox.Show(
                    $"Отчет успешно сохранен!\n\nФайл: {saveFileDialog.FileName}\nОшибок: {Errors.Count}",
                    "Экспорт завершен",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при экспорте:\n\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler CloseRequested;
}