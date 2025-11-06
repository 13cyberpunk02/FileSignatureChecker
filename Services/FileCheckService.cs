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
        public List<FileCheckResult> CheckFiles(List<Document> documents, string directoryPath)
        {
            var results = new List<FileCheckResult>();
            var allFilesInDirectory = GetAllFiles(directoryPath);
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var document in documents)
            {
                foreach (var xmlFile in document.Files)
                {
                    var result = CheckFile(xmlFile, allFilesInDirectory, directoryPath);
                    results.Add(result);

                    if (!result.FileFound || string.IsNullOrEmpty(result.FilePath)) continue;
                    processedFiles.Add(result.FilePath);
                        
                    if (!result.SignatureFound || xmlFile.SignFile == null) continue;
                    var sigPath = FindSignatureFile(xmlFile.SignFile.FileName, xmlFile.SignFile.FileChecksum, allFilesInDirectory);
                    if (!string.IsNullOrEmpty(sigPath))
                    {
                        processedFiles.Add(sigPath);
                    }
                }
            }

            var unprocessedFiles = CheckUnprocessedFiles(allFilesInDirectory, processedFiles);
            results.AddRange(unprocessedFiles);

            return results;
        }

        private FileCheckResult CheckFile(Models.FileInfo xmlFile, Dictionary<string, string> allFiles, string directoryPath)
        {
            var result = new FileCheckResult
            {
                FileName = xmlFile.FileName,
                XmlChecksum = xmlFile.FileChecksum
            };

            var filePath = FindFileByName(xmlFile.FileName, allFiles);

            if (!string.IsNullOrEmpty(filePath))
            {
                result.FileFound = true;
                result.FilePath = filePath;
                result.ActualChecksum = Crc32Service.CalculateChecksum(filePath);

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

            if (xmlFile.SignFile != null)
            {
                CheckSignature(xmlFile, result, allFiles);
            }
            
            CheckAdditionalSignaturesInDirectory(xmlFile, result, allFiles);

            return result;
        }

        private void CheckSignature(Models.FileInfo xmlFile, FileCheckResult result, Dictionary<string, string> allFiles)
        {
            if (xmlFile.SignFile == null) return;

            result.SignatureFileName = xmlFile.SignFile.FileName;

            var sigPath = FindFileByName(xmlFile.SignFile.FileName, allFiles);

            if (!string.IsNullOrEmpty(sigPath))
            {
                result.SignatureFound = true;
                var actualSigChecksum = Crc32Service.CalculateChecksum(sigPath);

                if (Crc32Service.CompareChecksums(xmlFile.SignFile.FileChecksum, actualSigChecksum))
                {
                    result.Message += result.FileFound 
                        ? $"\n✓ Подпись '{xmlFile.SignFile.FileName}' найдена и совпадает"
                        : $"\n✓ Подпись найдена и совпадает";
                }
                else
                {
                    result.Status = CheckStatus.Warning;
                    result.Message += $"\n⚠ Подпись '{xmlFile.SignFile.FileName}' найдена, но контрольная сумма не совпадает";
                }
            }
            else
            {
                sigPath = FindFileByChecksum(xmlFile.SignFile.FileChecksum, allFiles);

                if (!string.IsNullOrEmpty(sigPath))
                {
                    result.SignatureFound = true;
                    result.Status = CheckStatus.Warning;
                    result.Message += $"\n⚠ Подпись найдена по контрольной сумме с именем '{Path.GetFileName(sigPath)}'. В пояснительной записке файла '{xmlFile.FileName}' нужно заменить подпись.";
                }
                else
                {
                    result.Status = CheckStatus.Error;
                    result.Message += $"\n✗ Подпись '{xmlFile.SignFile.FileName}' не найдена";
                }
            }
        }

        private static void CheckAdditionalSignaturesInDirectory(Models.FileInfo xmlFile, FileCheckResult result, Dictionary<string, string> allFiles)
        {
            var fileName = xmlFile.FileName;
            var possibleSigFiles = new List<string>();
            var sigFileNamesFromXml = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (xmlFile.SignFile != null)
            {
                sigFileNamesFromXml.Add(xmlFile.SignFile.FileName);
            }

            foreach (var (fileNameInDir, value) in allFiles)
            {
                if (!fileNameInDir.EndsWith(".sig", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (fileNameInDir.Equals(fileName + ".sig", StringComparison.OrdinalIgnoreCase))
                {
                    if (!sigFileNamesFromXml.Contains(fileNameInDir))
                    {
                        possibleSigFiles.Add(value);
                    }
                }
                else if (fileNameInDir.EndsWith($"_{fileName}.sig", StringComparison.OrdinalIgnoreCase) ||
                         fileNameInDir.Contains(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    if (!sigFileNamesFromXml.Contains(fileNameInDir))
                    {
                        possibleSigFiles.Add(value);
                    }
                }
            }

            if (possibleSigFiles.Count <= 0) return;
            if (result.Status == CheckStatus.Success)
            {
                result.Status = CheckStatus.Warning;
            }

            var sigFileNames = possibleSigFiles.Select(Path.GetFileName).ToList();
                
            if (xmlFile.SignFile != null)
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

                var sigFileName = fileName + ".sig";
                var hasSigFile = allFiles.ContainsKey(sigFileName.ToLower());

                if (!hasSigFile) continue;
                var result = new FileCheckResult
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileFound = true,
                    SignatureFound = true,
                    SignatureFileName = sigFileName,
                    Status = CheckStatus.Info,
                    ActualChecksum = Crc32Service.CalculateChecksum(filePath),
                    Message = $"ℹ Файл '{fileName}' с подписью '{sigFileName}' не загружен в пояснительную записку. Необходимо загрузить."
                };
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

        private static string FindFileByName(string fileName, Dictionary<string, string> allFiles)
            => allFiles.TryGetValue(fileName, out var filePath) 
                ? filePath 
                : string.Empty;

        private static string FindFileByChecksum(string checksum, Dictionary<string, string> allFiles)
        {
            foreach (var file in from file in allFiles.Values let actualChecksum = Crc32Service.CalculateChecksum(file) where Crc32Service.CompareChecksums(checksum, actualChecksum) select file)
            {
                return file;
            }

            return string.Empty;
        }

        private static string FindSignatureFile(string signatureFileName, string signatureChecksum, Dictionary<string, string> allFiles)
            => allFiles.TryGetValue(signatureFileName, out var filePath) 
                ? filePath 
                : FindFileByChecksum(signatureChecksum, allFiles);
    }
}
