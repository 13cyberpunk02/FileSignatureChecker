using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileSignatureChecker.Models;
using FileSignatureChecker.Services;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;

namespace FileSignatureChecker.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly FileCheckService _fileCheckService = new();

        public SnackbarMessageQueue MessageQueue { get; } = new(TimeSpan.FromSeconds(3));

        [ObservableProperty] private string _xmlFilePath = string.Empty;

        [ObservableProperty] private string _directoryPath = string.Empty;

        [ObservableProperty] private ObservableCollection<FileCheckResult> _checkResults = [];

        [ObservableProperty] private bool _isChecking;

        [ObservableProperty] private int _totalFiles;

        [ObservableProperty] private int _progressValue;

        [ObservableProperty] private int _progressMax = 100;

        [ObservableProperty] private string _progressText = "";

        [ObservableProperty] private int _successCount;

        [ObservableProperty] private int _warningCount;

        [ObservableProperty] private int _errorCount;

        [ObservableProperty] private int _infoCount;

        [ObservableProperty] private string _statusMessage = "Готов к проверке";

        [ObservableProperty] private FileCheckResult? _selectedResult;

        [ObservableProperty] private string selectedStatusFilter = "Все";

        [ObservableProperty] private string selectedSectionFilter = "Все разделы";

        [ObservableProperty] private string searchText = string.Empty;

        public List<string> StatusFilterOptions { get; } =
        [
            "Все",
            "Успешно",
            "Предупреждения",
            "Ошибки",
            "Информация"
        ];

        [ObservableProperty] private ObservableCollection<string> sectionFilterOptions = ["Все разделы"];


        private readonly ObservableCollection<FileCheckResult> _allCheckResults = [];

        partial void OnSelectedStatusFilterChanged(string value)
        {
            ApplyFilters();
        }

        partial void OnSelectedSectionFilterChanged(string value)
        {
            ApplyFilters();
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilters();
        }

        [RelayCommand]
        private void SelectXmlFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "XML файлы (*.xml)|*.xml|Все файлы (*.*)|*.*",
                Title = "Выберите XML файл"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                XmlFilePath = openFileDialog.FileName;
            }
        }

        [RelayCommand]
        private void SelectDirectory()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Выберите директорию с файлами"
            };

            if (dialog.ShowDialog() == true)
            {
                DirectoryPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private async Task CheckFilesAsync()
        {
            if (string.IsNullOrWhiteSpace(XmlFilePath))
            {
                MessageBox.Show("Пожалуйста, выберите XML файл", "Внимание", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(DirectoryPath))
            {
                MessageBox.Show("Пожалуйста, выберите директорию", "Внимание", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(XmlFilePath))
            {
                MessageBox.Show("XML файл не найден", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!Directory.Exists(DirectoryPath))
            {
                MessageBox.Show("Директория не найдена", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            IsChecking = true;
            StatusMessage = "Выполняется проверка...";
            CheckResults.Clear();
            ResetCounters();
            ProgressValue = 0;
            ProgressText = "Подготовка";

            Application.Current.Dispatcher.Invoke(() =>
            {
                var window = Application.Current.MainWindow as MainWindow;
                window?.StartDotsAnimation();
            });

            try
            {
                await Task.Run(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProgressText = "Парсинг XML файла";
                        ProgressValue = 10;
                    });

                    var documents = XmlParserService.ParseXmlFile(XmlFilePath);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProgressText = "Проверка файлов";
                        ProgressValue = 30;
                    });

                    var results = _fileCheckService.CheckFiles(documents, DirectoryPath);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProgressText = "Формирование отчета";
                        ProgressValue = 80;
                    });

                    System.Threading.Thread.Sleep(200);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _allCheckResults.Clear();
                        foreach (var result in results)
                        {
                            _allCheckResults.Add(result);
                        }

                        UpdateSectionFilterOptions();

                        SelectedStatusFilter = "Все";
                        SelectedSectionFilter = "Все разделы";
                        SearchText = string.Empty;

                        ApplyFilters();

                        ProgressValue = 100;
                        ProgressText = "Готово!";
                    });
                });

                CalculateStatistics();
                StatusMessage = $"Проверка завершена. Всего файлов: {TotalFiles}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                StatusMessage = "Ошибка при проверке";
            }
            finally
            {
                IsChecking = false;
            }
        }

        /// <summary>
        /// Команда для открытия окна проверки схемы
        /// [RelayCommand] автоматически создает свойство OpenSchemaValidationCommand
        /// Это свойство можно привязать к кнопке в XAML
        /// </summary>
        [RelayCommand]
        private void OpenSchemaValidation()
        {
            var mainWindow = Application.Current.MainWindow;

            mainWindow?.Hide();

            var validationWindow = new SchemaValidationView();

            validationWindow.Closed += (s, args) => { mainWindow?.Show(); };

            validationWindow.Show();
        }

        [RelayCommand]
        private void CopyFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;

            try
            {
                Clipboard.SetText(fileName);
                MessageQueue.Enqueue("📋 Имя файла скопировано!");
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue($"❌ Ошибка: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ClearResults()
        {
            CheckResults.Clear();
            _allCheckResults.Clear();
            ResetCounters();
            SelectedStatusFilter = "Все";
            SelectedSectionFilter = "Все разделы";
            SearchText = string.Empty;
            SectionFilterOptions.Clear();
            SectionFilterOptions.Add("Все разделы");
            StatusMessage = "Результаты очищены";
        }

        [RelayCommand]
        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        [RelayCommand]
        private void ExportResults()
        {
            if (CheckResults.Count == 0)
            {
                MessageBox.Show("Нет результатов для экспорта", "Внимание", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                FileName = $"Результаты_проверки_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using var writer = new StreamWriter(saveFileDialog.FileName);
                    writer.WriteLine($"Отчет о проверке файлов");
                    writer.WriteLine($"Дата: {DateTime.Now}");
                    writer.WriteLine($"XML файл: {XmlFilePath}");
                    writer.WriteLine($"Директория: {DirectoryPath}");
                    writer.WriteLine(new string('=', 80));
                    writer.WriteLine();
                    writer.WriteLine($"Статистика:");
                    writer.WriteLine($"  Всего файлов: {TotalFiles}");
                    writer.WriteLine($"  Успешно: {SuccessCount}");
                    writer.WriteLine($"  Предупреждения: {WarningCount}");
                    writer.WriteLine($"  Ошибки: {ErrorCount}");
                    writer.WriteLine($"  Информационные: {InfoCount}");
                    writer.WriteLine();
                    writer.WriteLine(new string('=', 80));
                    writer.WriteLine();

                    foreach (var result in CheckResults)
                    {
                        writer.WriteLine($"Файл: {result.FileName}");
                        writer.WriteLine($"Статус: {GetStatusText(result.Status)}");
                        writer.WriteLine($"Сообщение: {result.Message}");
                        if (!string.IsNullOrEmpty(result.FilePath))
                            writer.WriteLine($"Путь: {result.FilePath}");
                        writer.WriteLine();
                    }

                    MessageBox.Show("Результаты успешно экспортированы", "Успех", MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void OpenFileLocation(FileCheckResult? result)
        {
            if (result == null || string.IsNullOrEmpty(result.FilePath))
                return;

            try
            {
                var directory = Path.GetDirectoryName(result.FilePath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{result.FilePath}\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть расположение файла: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            if (CheckResults.Count == 0)
            {
                MessageBox.Show("Нет результатов для экспорта", "Внимание", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx",
                FileName = $"Результаты_проверки_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.xlsx"
            };

            if (saveFileDialog.ShowDialog() != true) return;
            try
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Результаты проверки");

                worksheet.Cell(1, 1).Value = "Статус";
                worksheet.Cell(1, 2).Value = "Раздел";
                worksheet.Cell(1, 3).Value = "Тип документа";
                worksheet.Cell(1, 4).Value = "Номер документа";
                worksheet.Cell(1, 5).Value = "Дата документа";
                worksheet.Cell(1, 6).Value = "Файл";
                worksheet.Cell(1, 7).Value = "Результат";
                worksheet.Cell(1, 8).Value = "Путь к файлу";

                var headerRange = worksheet.Range(1, 1, 1, 8);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(103, 58, 183);
                headerRange.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                headerRange.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                headerRange.Style.Alignment.Vertical = ClosedXML.Excel.XLAlignmentVerticalValues.Center;

                var row = 2;
                foreach (var result in CheckResults)
                {
                    worksheet.Cell(row, 1).Value = GetStatusText(result.Status);
                    worksheet.Cell(row, 2).Value = result.DocName;
                    worksheet.Cell(row, 3).Value = result.DocType;
                    worksheet.Cell(row, 4).Value = result.DocNumber;
                    worksheet.Cell(row, 5).Value = result.DocDate;
                    worksheet.Cell(row, 6).Value = result.FileName;
                    worksheet.Cell(row, 7).Value = result.Message;
                    worksheet.Cell(row, 8).Value = result.FilePath;

                    var rowRange = worksheet.Range(row, 1, row, 8);
                    switch (result.Status)
                    {
                        case CheckStatus.Success:
                            rowRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(200, 230, 201);
                            break;
                        case CheckStatus.Warning:
                            rowRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(255, 224, 178);
                            break;
                        case CheckStatus.Error:
                            rowRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(255, 205, 210);
                            break;
                        case CheckStatus.Info:
                            rowRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(187, 222, 251);
                            break;
                    }

                    rowRange.Style.Alignment.WrapText = true;
                    rowRange.Style.Alignment.Vertical = ClosedXML.Excel.XLAlignmentVerticalValues.Top;

                    row++;
                }

                worksheet.Column(1).Width = 15;
                worksheet.Column(2).Width = 40;
                worksheet.Column(3).Width = 15;
                worksheet.Column(4).Width = 20;
                worksheet.Column(5).Width = 15;
                worksheet.Column(6).Width = 40;
                worksheet.Column(7).Width = 70;
                worksheet.Column(8).Width = 60;

                for (var r = 2; r < row; r++)
                {
                    worksheet.Row(r).AdjustToContents();
                    if (worksheet.Row(r).Height > 150)
                    {
                        worksheet.Row(r).Height = 150;
                    }
                }

                var dataRange = worksheet.Range(1, 1, row - 1, 8);
                dataRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;

                worksheet.SheetView.FreezeRows(1);

                workbook.SaveAs(saveFileDialog.FileName);
                MessageBox.Show("Excel файл успешно создан!", "Успех", MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании Excel: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ApplyFilters()
        {
            CheckResults.Clear();

            var filtered = _allCheckResults.AsEnumerable();

            if (SelectedStatusFilter != "Все")
            {
                filtered = SelectedStatusFilter switch
                {
                    "Успешно" => filtered.Where(r => r.Status == CheckStatus.Success),
                    "Предупреждения" => filtered.Where(r => r.Status == CheckStatus.Warning),
                    "Ошибки" => filtered.Where(r => r.Status == CheckStatus.Error),
                    "Информация" => filtered.Where(r => r.Status == CheckStatus.Info),
                    _ => filtered
                };
            }

            if (SelectedSectionFilter != "Все разделы")
            {
                filtered = filtered.Where(r => r.DocName == SelectedSectionFilter);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLower();
                filtered = filtered.Where(r =>
                    r.DocName.Contains(searchLower, StringComparison.CurrentCultureIgnoreCase) ||
                    r.FileName.Contains(searchLower, StringComparison.CurrentCultureIgnoreCase) ||
                    r.SignatureFileName.Contains(searchLower, StringComparison.CurrentCultureIgnoreCase) ||
                    r.Message.Contains(searchLower, StringComparison.CurrentCultureIgnoreCase) ||
                    r.DocType.Contains(searchLower, StringComparison.CurrentCultureIgnoreCase) ||
                    r.DocNumber.Contains(searchLower, StringComparison.CurrentCultureIgnoreCase)
                );
            }

            foreach (var result in filtered)
            {
                CheckResults.Add(result);
            }

            CalculateStatistics();
        }

        private void UpdateSectionFilterOptions()
        {
            var sections = _allCheckResults
                .Select(r => r.DocName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            SectionFilterOptions.Clear();
            SectionFilterOptions.Add("Все разделы");

            foreach (var section in sections)
            {
                SectionFilterOptions.Add(section);
            }
        }

        private void FilterResults()
        {
            CheckResults.Clear();

            var filtered = SelectedStatusFilter switch
            {
                "Успешно" => _allCheckResults.Where(r => r.Status == CheckStatus.Success),
                "Предупреждения" => _allCheckResults.Where(r => r.Status == CheckStatus.Warning),
                "Ошибки" => _allCheckResults.Where(r => r.Status == CheckStatus.Error),
                "Информация" => _allCheckResults.Where(r => r.Status == CheckStatus.Info),
                _ => _allCheckResults
            };

            foreach (var result in filtered)
            {
                CheckResults.Add(result);
            }

            CalculateStatistics();
        }

        private void CalculateStatistics()
        {
            TotalFiles = CheckResults.Count;
            SuccessCount = CheckResults.Count(r => r.Status == CheckStatus.Success);
            WarningCount = CheckResults.Count(r => r.Status == CheckStatus.Warning);
            ErrorCount = CheckResults.Count(r => r.Status == CheckStatus.Error);
            InfoCount = CheckResults.Count(r => r.Status == CheckStatus.Info);
        }

        private void ResetCounters()
        {
            TotalFiles = 0;
            SuccessCount = 0;
            WarningCount = 0;
            ErrorCount = 0;
            InfoCount = 0;
        }

        private string GetStatusText(CheckStatus status)
        {
            return status switch
            {
                CheckStatus.Success => "Успешно",
                CheckStatus.Warning => "Предупреждение",
                CheckStatus.Error => "Ошибка",
                CheckStatus.Info => "Информация",
                _ => "Неизвестно"
            };
        }
    }
}