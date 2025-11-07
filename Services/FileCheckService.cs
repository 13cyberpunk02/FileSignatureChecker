using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileSignatureChecker.Models;

namespace FileSignatureChecker.Services
{
    public class FileCheckService
    {
        private readonly string[] _supportedExtensions = [".pdf", ".xls", ".xlsx", ".doc", ".docx", ".gge"];
        private readonly Dictionary<string, string> _checksumCache = new Dictionary<string, string>();


        public List<FileCheckResult> CheckFiles(List<Document> documents, string directoryPath)
        {
            var results = new List<FileCheckResult>();
            
            _checksumCache.Clear();
            
            var allFilesInDirectory = GetAllFiles(directoryPath);
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            var sigFilesIndex = BuildSignatureFilesIndex(allFilesInDirectory);

            foreach (var document in documents)
            {
                foreach (var xmlFile in document.Files)
                {
                    var result = CheckFile(xmlFile, allFilesInDirectory, document, sigFilesIndex);
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
            Dictionary<string, List<string>> sigFilesIndex)
        {
            
            var fileName = xmlFile.FileName;
            var possibleSigFiles = new List<string>();
            var sigFileNamesFromXml = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var signFile in xmlFile.SignFiles)
            {
                sigFileNamesFromXml.Add(signFile.FileName);
            }

            if (sigFilesIndex.TryGetValue(fileName, out var indexedSigFiles))
            {
                possibleSigFiles.AddRange(from sigPath in indexedSigFiles let sigFileName = Path.GetFileName(sigPath) where !sigFileNamesFromXml.Contains(sigFileName) select sigPath);
            }
            
            foreach (var kvp in from kvp in allFiles let fileNameInDir = kvp.Key where fileNameInDir.EndsWith(".sig", StringComparison.OrdinalIgnoreCase) where fileNameInDir.Contains(fileName, StringComparison.OrdinalIgnoreCase) &&
                         !sigFileNamesFromXml.Contains(fileNameInDir) where !possibleSigFiles.Contains(kvp.Value) select kvp)
            {
                possibleSigFiles.Add(kvp.Value);
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

                var relatedSigFiles = (from file in allFiles where file.Key.EndsWith(".sig", StringComparison.OrdinalIgnoreCase) && (file.Key.Equals(fileName + ".sig", StringComparison.OrdinalIgnoreCase) || file.Key.Contains(fileName, StringComparison.OrdinalIgnoreCase)) where !processedFiles.Contains(file.Value) select file.Key).ToList();

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

        private static Dictionary<string, string> GetAllFiles(string directoryPath)
        {
            var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var allFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);
                foreach (var file in allFiles)
                {
                    var fileName = Path.GetFileName(file);
                    files[fileName] = file;
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки доступа к директориям
            }

            return files;
        }

        private FileCheckResult CheckFile(
            XmlFileInfo xmlFile, 
            Dictionary<string, string> allFiles, 
            Document document, 
            Dictionary<string, List<string>> sigFilesIndex)
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
            
            if (xmlFile.SignFiles.Count > 0)
            {
                CheckSignatures(xmlFile, result, allFiles);
            }
            
            CheckAdditionalSignaturesInDirectory(xmlFile, result, allFiles, sigFilesIndex);

            return result;
        }


        private static string FindFileByName(string fileName, Dictionary<string, string> allFiles)
            => allFiles.TryGetValue(fileName, out var filePath) 
                ? filePath 
                : string.Empty;

        private string FindFileByChecksum(string checksum, Dictionary<string, string> allFiles)
        {
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
            if (_checksumCache.TryGetValue(filePath, out var cachedChecksum))
            {
                return cachedChecksum;
            }

            var checksum = Crc32Service.CalculateChecksum(filePath);
            _checksumCache[filePath] = checksum;
            return checksum;
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
