using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using FileSignatureChecker.Models;

namespace FileSignatureChecker.Services
{
    public class XmlValidationService
    {
        private readonly string _schemaDirectory;
        private List<XsdSchemaInfo> _availableSchemas;

        public XmlValidationService(string schemaDirectory = null)
        {
            _schemaDirectory = schemaDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            LoadAvailableSchemas();
        }

        private void LoadAvailableSchemas()
        {
            _availableSchemas = new List<XsdSchemaInfo>();

            if (!Directory.Exists(_schemaDirectory))
            {
                Directory.CreateDirectory(_schemaDirectory);
                return;
            }

            var xsdFiles = Directory.GetFiles(_schemaDirectory, "*.xsd");
            
            foreach (var xsdFile in xsdFiles)
            {
                try
                {
                    var schemaInfo = ExtractSchemaInfo(xsdFile);
                    if (schemaInfo != null)
                    {
                        _availableSchemas.Add(schemaInfo);
                    }
                }
                catch
                {
                    // Игнорируем проблемные XSD файлы
                }
            }
        }

        private XsdSchemaInfo ExtractSchemaInfo(string xsdFilePath)
        {
            try
            {
                var doc = XDocument.Load(xsdFilePath);
                var ns = XNamespace.Get("http://www.w3.org/2001/XMLSchema");
                
                var schemaInfo = new XsdSchemaInfo
                {
                    FilePath = xsdFilePath,
                    FileName = Path.GetFileName(xsdFilePath)
                };

                var schemaElement = doc.Root;
                if (schemaElement != null)
                {
                    var versionAttr = schemaElement.Attribute("version");
                    if (versionAttr != null)
                    {
                        schemaInfo.Version = versionAttr.Value;
                    }
                }

                var schemaVersionElements = doc.Descendants(ns + "attribute")
                    .Where(e => e.Attribute("name")?.Value == "SchemaVersion");

                foreach (var element in schemaVersionElements)
                {
                    var fixedAttr = element.Attribute("fixed");
                    if (fixedAttr != null)
                    {
                        schemaInfo.FixedSchemaVersion = fixedAttr.Value;
                        break;
                    }
                }

                return schemaInfo;
            }
            catch
            {
                return null;
            }
        }

        public async Task<ValidationResult> ValidateFileAsync(string filePath)
        {
            return await Task.Run(() => ValidateFile(filePath));
        }

        private ValidationResult ValidateFile(string filePath)
        {
            try
            {
                var fileInfo = ExtractFileSchemaInfo(filePath);

                if (fileInfo == null)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "❌ Не удалось прочитать файл.\n\n" +
                            "Убедитесь, что:\n" +
                            "• Файл является корректным XML или GGE документом\n" +
                            "• Файл не поврежден\n" +
                            "• У вас есть права на чтение файла"
                    };
                }

                var matchingSchema = FindMatchingSchema(fileInfo);

                if (matchingSchema == null)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"❌ Не найдена подходящая XSD схема для валидации.\n\n" +
                            $"📋 Файл требует схему:\n" +
                            $"   • Имя схемы: {fileInfo.SchemaLocation}\n" +
                            $"   • Версия: {fileInfo.Version}\n\n" +
                            $"💡 Что нужно сделать:\n" +
                            $"   • Поместите нужный XSD файл в папку: {_schemaDirectory}\n" +
                            $"   • Убедитесь, что версия в XSD совпадает с версией в файле"
                    };
                }

                // Загружаем документы для детального анализа
                var xsdDoc = XDocument.Load(matchingSchema.FilePath);
                var xmlDoc = XDocument.Load(filePath, LoadOptions.SetLineInfo); // ВАЖНО: SetLineInfo для номеров строк

                var validationErrors = new List<DetailedValidationError>();
                
                var settings = new XmlReaderSettings();
                settings.Schemas.Add(null, matchingSchema.FilePath);
                settings.ValidationType = ValidationType.Schema;
                settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
                
                settings.ValidationEventHandler += (sender, args) =>
                {
                    validationErrors.Add(new DetailedValidationError
                    {
                        Args = args,
                        LineNumber = args.Exception?.LineNumber ?? 0,
                        LinePosition = args.Exception?.LinePosition ?? 0
                    });
                };

                using (var reader = XmlReader.Create(filePath, settings))
                {
                    while (reader.Read()) { }
                }

                if (validationErrors.Count == 0)
                {
                    return new ValidationResult
                    {
                        IsValid = true,
                        SchemaFileName = matchingSchema.FileName,
                        SchemaVersion = matchingSchema.Version ?? matchingSchema.FixedSchemaVersion,
                        SchemaPath = matchingSchema.FilePath
                    };
                }
                else
                {
                    var errorMessage = new StringBuilder();
                    errorMessage.AppendLine("Найдены следующие проблемы:\n");
                    
                    for (int i = 0; i < validationErrors.Count; i++)
                    {
                        var detailedError = TranslateValidationErrorDetailed(
                            validationErrors[i], 
                            xsdDoc, 
                            xmlDoc, 
                            filePath);
                        
                        errorMessage.AppendLine($"═══ Ошибка {i + 1} ═══");
                        errorMessage.AppendLine(detailedError);
                        
                        if (i < validationErrors.Count - 1)
                        {
                            errorMessage.AppendLine();
                        }
                    }

                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = errorMessage.ToString(),
                        SchemaFileName = matchingSchema.FileName,
                        SchemaVersion = matchingSchema.Version ?? matchingSchema.FixedSchemaVersion,
                        SchemaPath = matchingSchema.FilePath
                    };
                }
            }
            catch (Exception ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"❌ Произошла непредвиденная ошибка:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}"
                };
            }
        }

        /// <summary>
        /// Детальный перевод ошибки валидации с контекстом из XSD
        /// </summary>
        private string TranslateValidationErrorDetailed(
            DetailedValidationError error, 
            XDocument xsdDoc, 
            XDocument xmlDoc,
            string xmlFilePath)
        {
            var result = new StringBuilder();
            var message = error.Args.Message;
            var ns = XNamespace.Get("http://www.w3.org/2001/XMLSchema");

            // ОТЛАДКА
            result.AppendLine($"🔍 DEBUG: Начало анализа ошибки");
            result.AppendLine($"🔍 DEBUG: Сообщение = {message}");
            result.AppendLine();

            try
            {
                // Извлекаем имя элемента с ошибкой из сообщения
                var elementNameMatch = Regex.Match(message, @"'(\w+)'");
                
                result.AppendLine($"🔍 DEBUG: Regex match success = {elementNameMatch.Success}");
                
                if (!elementNameMatch.Success)
                {
                    return $"⚠️ {message}";
                }

                var errorElementName = elementNameMatch.Groups[1].Value;
                result.AppendLine($"🔍 DEBUG: Имя элемента с ошибкой = {errorElementName}");

                // Находим элемент в XML по номеру строки
                XElement errorElement = FindElementAtLine(xmlDoc, error.LineNumber, errorElementName);
                
                result.AppendLine($"🔍 DEBUG: Элемент найден = {errorElement != null}");
                
                if (errorElement == null)
                {
                    return $"⚠️ Ошибка в элементе '{errorElementName}' (строка {error.LineNumber})\n{message}";
                }

                // Строим путь от корня до элемента с ошибкой
                var path = BuildElementPath(errorElement);
                
                result.AppendLine($"📍 Расположение ошибки:");
                result.AppendLine($"   Строка {error.LineNumber} в файле");
                result.AppendLine();

                // Получаем описания для каждого уровня пути
                result.AppendLine($"📂 Путь к проблемному элементу:");
                
                var pathDescriptions = new List<string>();
                foreach (var pathElement in path)
                {
                    var description = GetElementDescription(xsdDoc, ns, pathElement);
                    if (!string.IsNullOrEmpty(description))
                    {
                        pathDescriptions.Add($"   → {pathElement}: {description}");
                    }
                    else
                    {
                        pathDescriptions.Add($"   → {pathElement}");
                    }
                }
                
                result.AppendLine(string.Join("\n", pathDescriptions));
                result.AppendLine();

                // Анализируем тип ошибки и даем детальное объяснение
                result.AppendLine($"❌ Описание проблемы:");
                
                result.AppendLine($"🔍 DEBUG: Проверка типа ошибки...");
                result.AppendLine($"🔍 DEBUG: message.Contains('pattern constraint', ignoreCase) = {message.IndexOf("pattern constraint", StringComparison.OrdinalIgnoreCase) >= 0}");
                result.AppendLine();
                
                if (message.IndexOf("pattern constraint", StringComparison.OrdinalIgnoreCase) >= 0 || 
                    message.Contains("шаблон"))
                {
                    // НОВЫЙ КОД РАБОТАЕТ!
                    result.AppendLine("   ✅ НОВАЯ ВЕРСИЯ КОДА АКТИВНА!");
                    result.AppendLine();
                    
                    var patternExplanation = ExplainPatternError(errorElementName, errorElement, xsdDoc, ns);
                    result.Append(patternExplanation);
                }
                else if (message.Contains("required attribute") || message.Contains("обязательный атрибут"))
                {
                    result.AppendLine($"   В элементе отсутствует обязательный атрибут.");
                    result.AppendLine($"   Проверьте, что все необходимые атрибуты заполнены.");
                }
                else if (message.Contains("required element") || message.Contains("обязательный элемент"))
                {
                    result.AppendLine($"   Отсутствует обязательный дочерний элемент.");
                    result.AppendLine($"   Проверьте структуру элемента согласно схеме.");
                }
                else if (message.Contains("invalid child element"))
                {
                    result.AppendLine($"   Элемент находится не в том месте или не должен там быть.");
                }
                else
                {
                    result.AppendLine($"   {message}");
                }

                // Показываем значение с ошибкой
                if (!string.IsNullOrWhiteSpace(errorElement.Value))
                {
                    result.AppendLine();
                    result.AppendLine($"💡 Текущее значение: '{errorElement.Value}'");
                }

            }
            catch (Exception ex)
            {
                return $"⚠️ {message} (строка {error.LineNumber})\n\nОшибка анализа: {ex.Message}";
            }

            return result.ToString();
        }

        /// <summary>
        /// Объясняет ошибку с pattern (регулярное выражение)
        /// </summary>
        private string ExplainPatternError(string elementName, XElement errorElement, XDocument xsdDoc, XNamespace ns)
        {
            var result = new StringBuilder();
            var elementDescription = GetElementDescription(xsdDoc, ns, elementName);
            
            if (!string.IsNullOrEmpty(elementDescription))
            {
                result.AppendLine($"   Поле: {elementDescription}");
            }

            // Ищем тип элемента
            var elementDef = xsdDoc.Descendants(ns + "element")
                .FirstOrDefault(e => e.Attribute("name")?.Value == elementName);

            if (elementDef != null)
            {
                var typeName = elementDef.Attribute("type")?.Value;
                if (!string.IsNullOrEmpty(typeName))
                {
                    // Убираем префикс, если есть (например, xs:string -> string)
                    if (typeName.Contains(":"))
                    {
                        typeName = typeName.Split(':')[1];
                    }

                    // Ищем определение типа
                    var typeDef = xsdDoc.Descendants(ns + "simpleType")
                        .FirstOrDefault(t => t.Attribute("name")?.Value == typeName);

                    if (typeDef != null)
                    {
                        var typeDescription = typeDef.Descendants(ns + "documentation")
                            .FirstOrDefault(d => d.Attribute(XNamespace.Xml + "lang")?.Value == "ru");

                        if (typeDescription != null)
                        {
                            result.AppendLine($"   Тип данных: {typeDescription.Value}");
                        }

                        // Ищем pattern и length
                        var pattern = typeDef.Descendants(ns + "pattern")
                            .FirstOrDefault()?.Attribute("value")?.Value;
                        
                        var length = typeDef.Descendants(ns + "length")
                            .FirstOrDefault()?.Attribute("value")?.Value;

                        var minLength = typeDef.Descendants(ns + "minLength")
                            .FirstOrDefault()?.Attribute("value")?.Value;

                        var maxLength = typeDef.Descendants(ns + "maxLength")
                            .FirstOrDefault()?.Attribute("value")?.Value;

                        if (!string.IsNullOrEmpty(pattern) || !string.IsNullOrEmpty(length) || 
                            !string.IsNullOrEmpty(minLength) || !string.IsNullOrEmpty(maxLength))
                        {
                            result.AppendLine();
                            result.AppendLine($"   ⚙️ Требования к заполнению:");

                            if (!string.IsNullOrEmpty(pattern))
                            {
                                var patternExplanation = ExplainPattern(pattern);
                                result.AppendLine($"   • Формат: {patternExplanation}");
                            }

                            if (!string.IsNullOrEmpty(length))
                            {
                                result.AppendLine($"   • Длина должна быть ровно: {length} символов");
                            }

                            if (!string.IsNullOrEmpty(minLength))
                            {
                                result.AppendLine($"   • Минимальная длина: {minLength} символов");
                            }

                            if (!string.IsNullOrEmpty(maxLength))
                            {
                                result.AppendLine($"   • Максимальная длина: {maxLength} символов");
                            }
                        }
                    }
                    else
                    {
                        result.AppendLine($"   Тип данных: {typeName}");
                    }
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Переводит регулярное выражение на человеческий язык
        /// </summary>
        private string ExplainPattern(string pattern)
        {
            if (pattern == @"\d{13}")
                return "13 цифр";
            
            if (pattern == @"\d{10}")
                return "10 цифр";
            
            if (pattern == @"\d{12}")
                return "12 цифр";
            
            if (pattern == @"\d{9}")
                return "9 цифр";

            if (pattern.Contains(@"\d{") && pattern.Contains("}"))
            {
                var match = Regex.Match(pattern, @"\\d\{(\d+)\}");
                if (match.Success)
                {
                    return $"{match.Groups[1].Value} цифр";
                }
            }

            if (pattern.Contains(@"\d") && pattern.Contains("+"))
                return "только цифры";

            if (pattern.Contains(@"[A-Za-z]"))
                return "буквы латинского алфавита";

            if (pattern.Contains(@"[А-Яа-я]"))
                return "буквы русского алфавита";

            return $"специальный формат: {pattern}";
        }

        /// <summary>
        /// Получает описание элемента из XSD
        /// </summary>
        private string GetElementDescription(XDocument xsdDoc, XNamespace ns, string elementName)
        {
            var element = xsdDoc.Descendants(ns + "element")
                .FirstOrDefault(e => e.Attribute("name")?.Value == elementName);

            if (element != null)
            {
                var documentation = element.Descendants(ns + "documentation")
                    .FirstOrDefault(d => d.Attribute(XNamespace.Xml + "lang")?.Value == "ru");

                if (documentation != null)
                {
                    return documentation.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Строит путь от корня до элемента
        /// </summary>
        private List<string> BuildElementPath(XElement element)
        {
            var path = new List<string>();
            var current = element;

            while (current != null)
            {
                path.Insert(0, current.Name.LocalName);
                current = current.Parent;
            }

            return path;
        }

        /// <summary>
        /// Находит элемент по номеру строки
        /// </summary>
        private XElement FindElementAtLine(XDocument doc, int lineNumber, string elementName)
        {
            try
            {
                var elements = doc.Descendants()
                    .Where(e => e.Name.LocalName == elementName)
                    .ToList();

                // Если нашли только один элемент с таким именем - возвращаем его
                if (elements.Count == 1)
                    return elements[0];

                // Если несколько - пытаемся найти по линии
                foreach (var elem in elements)
                {
                    var lineInfo = (IXmlLineInfo)elem;
                    if (lineInfo.HasLineInfo() && lineInfo.LineNumber == lineNumber)
                    {
                        return elem;
                    }
                }

                // Если не нашли точно - возвращаем первый
                return elements.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private FileSchemaInfo ExtractFileSchemaInfo(string filePath)
        {
            try
            {
                var doc = XDocument.Load(filePath);
                var root = doc.Root;

                if (root == null)
                    return null;

                var info = new FileSchemaInfo();

                var xsiNamespace = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");
                var schemaLocationAttr = root.Attribute(xsiNamespace + "noNamespaceSchemaLocation");
                
                if (schemaLocationAttr != null)
                {
                    info.SchemaLocation = schemaLocationAttr.Value;
                }

                var schemaVersionAttr = root.Attribute("SchemaVersion");
                if (schemaVersionAttr != null)
                {
                    info.Version = schemaVersionAttr.Value;
                }
                else
                {
                    var metaElement = root.Element("Meta");
                    if (metaElement != null)
                    {
                        var fileElement = metaElement.Element("File");
                        if (fileElement != null)
                        {
                            var versionElement = fileElement.Element("Version");
                            if (versionElement != null)
                            {
                                info.Version = versionElement.Value;
                            }
                        }
                    }
                }

                return info;
            }
            catch
            {
                return null;
            }
        }

        private XsdSchemaInfo FindMatchingSchema(FileSchemaInfo fileInfo)
        {
            var schemaByName = _availableSchemas.FirstOrDefault(s => 
                s.FileName.Equals(fileInfo.SchemaLocation, StringComparison.OrdinalIgnoreCase));

            if (schemaByName != null)
            {
                if (!string.IsNullOrEmpty(fileInfo.Version))
                {
                    if (schemaByName.Version == fileInfo.Version || 
                        schemaByName.FixedSchemaVersion == fileInfo.Version)
                    {
                        return schemaByName;
                    }
                }
                else
                {
                    return schemaByName;
                }
            }

            if (!string.IsNullOrEmpty(fileInfo.Version))
            {
                var schemaByVersion = _availableSchemas.FirstOrDefault(s =>
                    s.Version == fileInfo.Version || s.FixedSchemaVersion == fileInfo.Version);

                if (schemaByVersion != null)
                {
                    return schemaByVersion;
                }
            }

            return null;
        }
    }
}
