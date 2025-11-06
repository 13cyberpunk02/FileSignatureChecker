using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileSignatureChecker.Models;
using FileSignatureChecker.Services;
using Microsoft.Win32;

namespace FileSignatureChecker.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly FileCheckService _fileCheckService = new();

        [ObservableProperty]
        private string _xmlFilePath = string.Empty;

        [ObservableProperty]
        private string _directoryPath = string.Empty;

        [ObservableProperty]
        private ObservableCollection<FileCheckResult> _checkResults = [];

        [ObservableProperty]
        private bool _isChecking;

        [ObservableProperty]
        private int _totalFiles;

        [ObservableProperty]
        private int _progressValue;

        [ObservableProperty]
        private int _progressMax = 100;

        [ObservableProperty]
        private string _progressText = "";
        
        [ObservableProperty]
        private int _successCount;

        [ObservableProperty]
        private int _warningCount;

        [ObservableProperty]
        private int _errorCount;

        [ObservableProperty]
        private int _infoCount;

        [ObservableProperty]
        private string _statusMessage = "Готов к проверке";

        [ObservableProperty]
        private FileCheckResult? _selectedResult;

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
                MessageBox.Show("Пожалуйста, выберите XML файл", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(DirectoryPath))
            {
                MessageBox.Show("Пожалуйста, выберите директорию", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            ProgressText = "Подготовка...";
            
            try
            {
                await Task.Run(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProgressText = "Парсинг XML файла...";
                        ProgressValue = 10;
                    });
                    
                    var documents = XmlParserService.ParseXmlFile(XmlFilePath);
                    
                    var totalFilesToCheck = documents.Sum(d => d.Files.Count);
                    var processedFiles = 0;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProgressText = "Сканирование директории...";
                        ProgressValue = 20;
                    });
                    
                    var results = new List<FileCheckResult>();

                    foreach (var document in documents)
                    {
                        foreach (var xmlFile in document.Files)
                        {
                            var allFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            var allFilesArray = Directory.GetFiles(DirectoryPath, "*.*", SearchOption.AllDirectories);
                            foreach (var file in allFilesArray)
                            {
                                var fileName = Path.GetFileName(file);
                                allFiles[fileName] = file;
                            }

                            var result = _fileCheckService.CheckFiles([document], DirectoryPath)
                                .FirstOrDefault(r => r.FileName == xmlFile.FileName);
                    
                            if (result != null)
                            {
                                results.Add(result);
                            }

                            processedFiles++;
                            var progress = 20 + (int)((processedFiles / (double)totalFilesToCheck) * 70);
                    
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ProgressValue = progress;
                                ProgressText = $"Обработано {processedFiles} из {totalFilesToCheck} файлов...";
                            });
                        }
                    }
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProgressText = "Формирование отчета...";
                        ProgressValue = 95;
                    });
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var result in results)
                        {
                            CheckResults.Add(result);
                        }
                    });
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProgressValue = 100;
                        ProgressText = "Готово!";
                    });
                });

                CalculateStatistics();
                StatusMessage = $"Проверка завершена. Всего файлов: {TotalFiles}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Ошибка при проверке";
            }
            finally
            {
                IsChecking = false;
            }
        }

        [RelayCommand]
        private void ClearResults()
        {
            CheckResults.Clear();
            ResetCounters();
            StatusMessage = "Результаты очищены";
        }

        [RelayCommand]
        private void ExportResults()
        {
            if (CheckResults.Count == 0)
            {
                MessageBox.Show("Нет результатов для экспорта", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                    MessageBox.Show("Результаты успешно экспортированы", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Не удалось открыть расположение файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
