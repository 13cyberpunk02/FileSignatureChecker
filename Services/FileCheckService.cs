using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileSignatureChecker.Models;

namespace FileSignatureChecker.Services
{
    public class FileCheckService
    {
        private readonly string[] _supportedExtensions = [".pdf", ".xls", ".xlsx", ".doc", ".docx", ".gge"];
        // Заполняется параллельно из FindFileByChecksum (AsParallel), поэтому ConcurrentDictionary
        private readonly ConcurrentDictionary<string, string> _checksumCache = new(StringComparer.OrdinalIgnoreCase);

        // Имена файлов, которые встречаются в директории более одного раза (имя -> все пути)
        private Dictionary<string, List<string>> _duplicateFiles = new(StringComparer.OrdinalIgnoreCase);

        public List<FileCheckResult> CheckFiles(List<Document> documents, string directoryPath)
        {
            var results = new List<FileCheckResult>();

            _checksumCache.Clear();

            var allFilesInDirectory = GetAllFiles(directoryPath, out _duplicateFiles);
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            var sigFilesIndex = BuildSignatureFilesIndex(allFilesInDirectory);

            // Все подписи, указанные в XML у любого файла: они не "дополнительные" ни для какого другого файла
            var allXmlSignatureNames = new HashSet<string>(
                documents.SelectMany(d => d.Files).SelectMany(f => f.SignFiles).Select(s => s.FileName),
                StringComparer.OrdinalIgnoreCase);

            foreach (var document in documents)
            {
                foreach (var xmlFile in document.Files)
                {
                    var result = CheckFile(xmlFile, allFilesInDirectory, document, sigFilesIndex, allXmlSignatureNames);
                    results.Add(result);

                    if (!result.FileFound || string.IsNullOrEmpty(result.FilePath)) continue;
                    processedFiles.Add(result.FilePath);

                    if (xmlFile.SignFiles.Count <= 0) continue;
                    foreach (var sigPath in xmlFile.SignFiles.Select(signFile => FindSignatureFile(signFile.FileName, signFile.FileChecksum, allFilesInDirectory)).Where(sigPath => !string.IsNullOrEmpty(sigPath)))
                    {
                        processedFiles.Add(sigPath);
                    }
                }
            }

            var unprocessedFiles = CheckUnprocessedFiles(allFilesInDirectory, processedFiles);
            results.AddRange(unprocessedFiles);

            return results;
        }

        private void CheckSignatures(XmlFileInfo xmlFile, FileCheckResult result, Dictionary<string, string> allFiles)
        {
            if (xmlFile.SignFiles.Count == 0)
                return;

            var allSignaturesValid = true;
            var signatureMessages = new List<string>();

            foreach (var signFile in xmlFile.SignFiles)
            {
                var sigPath = FindFileByName(signFile.FileName, allFiles);

                if (!string.IsNullOrEmpty(sigPath))
                {
                    result.SignatureFound = true;
                    var actualSigChecksum = CalculateChecksumWithCache(sigPath);

                    if (Crc32Service.CompareChecksums(signFile.FileChecksum, actualSigChecksum))
                    {
                        signatureMessages.Add($"✓ Подпись '{signFile.FileName}' найдена и совпадает");
                    }
                    else
                    {
                        allSignaturesValid = false;
                        signatureMessages.Add($"⚠ Подпись '{signFile.FileName}' найдена, но контрольная сумма не совпадает");
                    }
                }
                else
                {
                    sigPath = FindFileByChecksum(signFile.FileChecksum, allFiles);

                    if (!string.IsNullOrEmpty(sigPath))
                    {
                        result.SignatureFound = true;
                        allSignaturesValid = false;
                        signatureMessages.Add($"⚠ Подпись найдена по контрольной сумме с именем '{Path.GetFileName(sigPath)}'. В пояснительной записке файла '{xmlFile.FileName}' нужно заменить подпись.");
                    }
                    else
                    {
                        allSignaturesValid = false;
                        signatureMessages.Add($"✗ Подпись '{signFile.FileName}' не найдена");
                    }
                }
            }

            if (signatureMessages.Count <= 0) return;
            result.Message += "\n" + string.Join("\n", signatureMessages);

            if (!allSignaturesValid && result.Status == CheckStatus.Success)
            {
                result.Status = CheckStatus.Warning;
            }
        }

        private static void CheckAdditionalSignaturesInDirectory(
            XmlFileInfo xmlFile, 
            FileCheckResult result, 
            Dictionary<string, string> allFiles,
            Dictionary<string, List<string>> sigFilesIndex,
            HashSet<string> allXmlSignatureNames)
        {
            var fileName = xmlFile.FileName;
            if (string.IsNullOrWhiteSpace(fileName)) return;

            var possibleSigFiles = new List<string>();

            if (sigFilesIndex.TryGetValue(fileName, out var indexedSigFiles))
            {
                possibleSigFiles.AddRange(indexedSigFiles.Where(sigPath => !allXmlSignatureNames.Contains(Path.GetFileName(sigPath))));
            }

            foreach (var (fileNameInDir, sigPath) in allFiles)
            {
                if (!fileNameInDir.EndsWith(".sig", StringComparison.OrdinalIgnoreCase)) continue;
                if (allXmlSignatureNames.Contains(fileNameInDir)) continue;
                if (possibleSigFiles.Contains(sigPath)) continue;
                if (IsSignatureNameFor(fileNameInDir, fileName))
                    possibleSigFiles.Add(sigPath);
            }

            if (possibleSigFiles.Count <= 0) return;
            if (result.Status == CheckStatus.Success)
            {
                result.Status = CheckStatus.Warning;
            }
            
            var sigFileNames = possibleSigFiles.Select(Path.GetFileName).ToList();
                
            if (xmlFile.SignFiles.Count > 0)
            {
                if (possibleSigFiles.Count == 1)
                {
                    result.Message += $"\n⚠ Найдена дополнительная подпись '{sigFileNames[0]}' в директории, которая не указана в XML.\n  → Необходимо загрузить эту подпись в пояснительную записку.";
                }
                else
                {
                    result.Message += $"\n⚠ Найдено {possibleSigFiles.Count} дополнительных подписей в директории, которые не указаны в XML:";
                    foreach (var sigName in sigFileNames)
                    {
                        result.Message += $"\n  • {sigName}";
                    }
                    result.Message += "\n  → Необходимо загрузить эти подписи в пояснительную записку.";
                }
            }
            else
            {
                if (possibleSigFiles.Count == 1)
                {
                    result.Message += $"\n⚠ Найдена подпись '{sigFileNames[0]}' в директории, но она не указана в XML.\n  → Необходимо загрузить подпись в пояснительную записку.";
                }
                else
                {
                    result.Message += $"\n⚠ Найдено {possibleSigFiles.Count} подписей в директории, но они не указаны в XML:";
                    foreach (var sigName in sigFileNames)
                    {
                        result.Message += $"\n  • {sigName}";
                    }
                    result.Message += "\n  → Необходимо загрузить подписи в пояснительную записку.";
                }
            }

        }

        private List<FileCheckResult> CheckUnprocessedFiles(Dictionary<string, string> allFiles, HashSet<string> processedFiles)
        {
            var results = new List<FileCheckResult>();

            foreach (var (fileName, filePath) in allFiles)
            {
                if (processedFiles.Contains(filePath))
                    continue;

                var extension = Path.GetExtension(fileName).ToLower();
                if (!_supportedExtensions.Contains(extension))
                    continue;

                var relatedSigFiles = allFiles
                    .Where(file => file.Key.EndsWith(".sig", StringComparison.OrdinalIgnoreCase)
                                   && IsSignatureNameFor(file.Key, fileName)
                                   && !processedFiles.Contains(file.Value))
                    .Select(file => file.Key)
                    .ToList();

                if (relatedSigFiles.Count <= 0) continue;
                var result = new FileCheckResult
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileFound = true,
                    SignatureFound = true,
                    Status = CheckStatus.Info,
                    ActualChecksum = CalculateChecksumWithCache(filePath)
                };

                if (relatedSigFiles.Count == 1)
                {
                    result.SignatureFileName = relatedSigFiles[0];
                    result.Message = $"ℹ Файл '{fileName}' с подписью '{relatedSigFiles[0]}' не загружен в пояснительную записку. Необходимо загрузить.";
                }
                else
                {
                    result.SignatureFileName = string.Join(", ", relatedSigFiles);
                    result.Message = $"ℹ Файл '{fileName}' с {relatedSigFiles.Count} подписями не загружен в пояснительную записку:";
                    foreach (var sig in relatedSigFiles)
                    {
                        result.Message += $"\n  • {sig}";
                    }
                    result.Message += "\nНеобходимо загрузить файл и все его подписи.";
                }

                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Собирает файлы директории (рекурсивно) в словарь "имя файла -> путь".
        /// Если одно имя встречается в нескольких подпапках, в словарь попадает файл
        /// с наименьшей глубиной вложенности, а все пути возвращаются в <paramref name="duplicates"/>.
        /// </summary>
        private static Dictionary<string, string> GetAllFiles(string directoryPath, out Dictionary<string, List<string>> duplicates)
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true, // недоступная подпапка не должна обнулять весь список
                AttributesToSkip = FileAttributes.System
            };

            var grouped = Directory.EnumerateFiles(directoryPath, "*", options)
                .OrderBy(path => path.Count(c => c == Path.DirectorySeparatorChar))
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            duplicates = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in grouped)
            {
                var paths = group.ToList();
                files[group.Key!] = paths[0];
                if (paths.Count > 1)
                    duplicates[group.Key!] = paths;
            }

            return files;
        }

        /// <summary>
        /// Похоже ли имя .sig файла на подпись файла <paramref name="fileName"/>:
        /// имя файла входит в имя подписи и не "приклеено" к другим буквам/цифрам слева.
        /// "Иванов_1.pdf.sig" и "1.pdf.sig" — подписи "1.pdf", а "11.pdf.sig" и "Раздел1.pdf.sig" — нет.
        /// </summary>
        private static bool IsSignatureNameFor(string sigFileName, string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;

            var index = sigFileName.IndexOf(fileName, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                if (index == 0 || !char.IsLetterOrDigit(sigFileName[index - 1]))
                    return true;
                index = sigFileName.IndexOf(fileName, index + 1, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private void AppendDuplicateWarning(string fileName, FileCheckResult result)
        {
            if (!_duplicateFiles.TryGetValue(fileName, out var paths)) return;

            if (result.Status == CheckStatus.Success)
                result.Status = CheckStatus.Warning;

            result.Message += $"\n⚠ В директории найдено {paths.Count} файла(ов) с именем '{fileName}'. Проверен: '{paths[0]}'. Остальные:";
            foreach (var path in paths.Skip(1))
            {
                result.Message += $"\n  • {path}";
            }
        }

        private FileCheckResult CheckFile(
            XmlFileInfo xmlFile, 
            Dictionary<string, string> allFiles, 
            Document document,
            Dictionary<string, List<string>> sigFilesIndex,
            HashSet<string> allXmlSignatureNames)
        {
            var result = new FileCheckResult
            {
                FileName = xmlFile.FileName,
                XmlChecksum = xmlFile.FileChecksum,
                DocName = document.DocName,
                DocType = document.DocType,
                DocNumber = document.DocNumber,
                DocDate = document.DocDate
            };

            var filePath = FindFileByName(xmlFile.FileName, allFiles);

            if (!string.IsNullOrEmpty(filePath))
            {
                result.FileFound = true;
                result.FilePath = filePath;
                result.ActualChecksum = CalculateChecksumWithCache(filePath);

                if (Crc32Service.CompareChecksums(xmlFile.FileChecksum, result.ActualChecksum))
                {
                    result.Status = CheckStatus.Success;
                    result.Message = $"✓ Файл '{xmlFile.FileName}' найден, контрольная сумма совпадает";
                }
                else
                {
                    result.Status = CheckStatus.Warning;
                    result.Message = $"⚠ Файл '{xmlFile.FileName}' найден, но контрольная сумма не совпадает. Возможно файл был изменен.";
                }
            }
            else
            {
                filePath = FindFileByChecksum(xmlFile.FileChecksum, allFiles);

                if (!string.IsNullOrEmpty(filePath))
                {
                    result.FileFound = true;
                    result.FilePath = filePath;
                    result.ActualChecksum = xmlFile.FileChecksum;
                    result.Status = CheckStatus.Warning;
                    result.Message = $"⚠ Файл найден по контрольной сумме, но с другим именем: '{Path.GetFileName(filePath)}'. В пояснительной записке нужно перезалить файл '{xmlFile.FileName}'.";
                }
                else
                {
                    result.Status = CheckStatus.Error;
                    result.Message = $"✗ Файл '{xmlFile.FileName}' загружен в пояснительную записку, но не найден в директории.\n⚠ Либо удалите запись из XML, либо найдите и добавьте этот файл в директорию.";
                }
            }
            
            AppendDuplicateWarning(xmlFile.FileName, result);

            if (xmlFile.SignFiles.Count > 0)
            {
                CheckSignatures(xmlFile, result, allFiles);
            }
            
            CheckAdditionalSignaturesInDirectory(xmlFile, result, allFiles, sigFilesIndex, allXmlSignatureNames);

            return result;
        }


        private static string FindFileByName(string fileName, Dictionary<string, string> allFiles)
            => allFiles.TryGetValue(fileName, out var filePath) 
                ? filePath 
                : string.Empty;

        private string FindFileByChecksum(string checksum, Dictionary<string, string> allFiles)
        {
            // Без контрольной суммы искать нечего, а перебор пересчитал бы CRC всех файлов
            if (string.IsNullOrWhiteSpace(checksum))
                return string.Empty;

            if (allFiles.Count > 100)
            {
                var result = allFiles.AsParallel()
                    .FirstOrDefault(kvp => 
                    {
                        var actualChecksum = CalculateChecksumWithCache(kvp.Value);
                        return Crc32Service.CompareChecksums(checksum, actualChecksum);
                    });
        
                return result.Value ?? string.Empty;
            }
            else
            {
                foreach (var file in from file in allFiles.Values let actualChecksum = CalculateChecksumWithCache(file) where Crc32Service.CompareChecksums(checksum, actualChecksum) select file)
                {
                    return file;
                }
            }

            return string.Empty;
        }
        
        private string CalculateChecksumWithCache(string filePath)
        {
            return _checksumCache.GetOrAdd(filePath, Crc32Service.CalculateChecksum);
        }

        private string FindSignatureFile(string signatureFileName, string signatureChecksum,
            Dictionary<string, string> allFiles)
            => allFiles.TryGetValue(signatureFileName, out var filePath) ? filePath : FindFileByChecksum(signatureChecksum, allFiles);
        
        private Dictionary<string, List<string>> BuildSignatureFilesIndex(Dictionary<string, string> allFiles)
        {
            var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var (fileName, s) in allFiles)
            {
                if (!fileName.EndsWith(".sig", StringComparison.OrdinalIgnoreCase))
                    continue;

                var baseFileName = fileName.Substring(0, fileName.Length - 4);

                if (!index.TryGetValue(baseFileName, out var value1))
                {
                    value1 = [];
                    index[baseFileName] = value1;
                }

                value1.Add(s);

                var underscoreIndex = fileName.LastIndexOf('_');
                if (underscoreIndex <= 0) continue;
                var alternativeBase = fileName.Substring(underscoreIndex + 1, fileName.Length - underscoreIndex - 5); 
                if (!index.TryGetValue(alternativeBase, out var value))
                {
                    value = [];
                    index[alternativeBase] = value;
                }

                value.Add(s);
            }

            return index;
        }
    }
}
