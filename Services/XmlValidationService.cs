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

        // Словарь переводов стандартных типов XSD на русский
        private static readonly Dictionary<string, string> XsdTypeTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "positiveInteger", "положительное целое число (больше 0)" },
            { "nonNegativeInteger", "неотрицательное целое число (0 или больше)" },
            { "negativeInteger", "отрицательное целое число (меньше 0)" },
            { "nonPositiveInteger", "неположительное целое число (0 или меньше)" },
            { "integer", "целое число" },
            { "int", "целое число" },
            { "unsignedInt", "целое число без знака (0 или больше)" },
            { "long", "длинное целое число" },
            { "unsignedLong", "длинное целое число без знака" },
            { "short", "короткое целое число" },
            { "unsignedShort", "короткое целое число без знака" },
            { "byte", "байт (-128 до 127)" },
            { "unsignedByte", "байт без знака (0 до 255)" },
            { "decimal", "десятичное число" },
            { "float", "число с плавающей точкой" },
            { "double", "число с плавающей точкой двойной точности" },
            { "string", "текстовая строка" },
            { "boolean", "логическое значение (true/false)" },
            { "date", "дата (ГГГГ-ММ-ДД)" },
            { "time", "время (ЧЧ:ММ:СС)" },
            { "dateTime", "дата и время (ГГГГ-ММ-ДД ЧЧ:ММ:СС)" },
            { "duration", "длительность" },
            { "anyURI", "URL адрес" },
            { "base64Binary", "данные в формате Base64" },
            { "hexBinary", "данные в шестнадцатеричном формате" },
            { "normalizedString", "нормализованная строка" },
            { "token", "токен (строка без лишних пробелов)" }
        };

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
                        Errors = new List<ValidationError>
                        {
                            new ValidationError
                            {
                                ErrorNumber = 1,
                                Description = "Не удалось прочитать файл.\n\n" +
                                    "Убедитесь, что:\n" +
                                    "• Файл является корректным XML или GGE документом\n" +
                                    "• Файл не поврежден\n" +
                                    "• У вас есть права на чтение файла",
                                FullMessage = "❌ Не удалось прочитать файл.\n\n" +
                                    "Убедитесь, что:\n" +
                                    "• Файл является корректным XML или GGE документом\n" +
                                    "• Файл не поврежден\n" +
                                    "• У вас есть права на чтение файла"
                            }
                        }
                    };
                }

                var matchingSchema = FindMatchingSchema(fileInfo);

                if (matchingSchema == null)
                {
                    var schemaFileName = Path.GetFileName(fileInfo.SchemaLocation);
                    var availableSchemasList = string.Join("\n   • ", _availableSchemas.Select(s => $"{s.FileName} (версия: {s.Version ?? s.FixedSchemaVersion ?? "не указана"})"));
                    
                    var errorMessage = $"Не найдена подходящая XSD схема для валидации.\n\n" +
                        $"📋 Файл требует:\n" +
                        $"   • Схема: {schemaFileName}\n" +
                        $"   • Версия: {fileInfo.Version ?? "не указана"}\n\n";
                    
                    if (_availableSchemas.Count > 0)
                    {
                        errorMessage += $"📂 Доступные схемы в папке ({_availableSchemas.Count}):\n" +
                            $"   • {availableSchemasList}\n\n";
                    }
                    
                    errorMessage += $"💡 Что нужно сделать:\n" +
                        $"   1. Поместите файл '{schemaFileName}' в папку:\n" +
                        $"      {_schemaDirectory}\n" +
                        $"   2. Убедитесь, что имя файла точно совпадает\n" +
                        $"   3. Проверьте версию схемы в XSD файле";
                    
                    return new ValidationResult
                    {
                        IsValid = false,
                        Errors = new List<ValidationError>
                        {
                            new ValidationError
                            {
                                ErrorNumber = 1,
                                Description = errorMessage,
                                FullMessage = "❌ " + errorMessage,
                                Location = $"Требуется: {schemaFileName}",
                                Details = $"Папка схем: {_schemaDirectory}"
                            }
                        }
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
                        SchemaVersion = string.IsNullOrEmpty(matchingSchema.Version) 
                            ? matchingSchema.FixedSchemaVersion 
                            : matchingSchema.Version,
                        SchemaPath = matchingSchema.FilePath
                    };
                }
                else
                {
                    var errors = new List<ValidationError>();
                    
                    for (int i = 0; i < validationErrors.Count; i++)
                    {
                        var detailedErrorText = TranslateValidationErrorDetailed(
                            validationErrors[i], 
                            xsdDoc, 
                            xmlDoc, 
                            filePath);
                        
                        // Парсим текст ошибки в структурированный объект
                        var error = ParseErrorToObject(detailedErrorText, i + 1);
                        errors.Add(error);
                    }

                    return new ValidationResult
                    {
                        IsValid = false,
                        Errors = errors,
                        SchemaFileName = matchingSchema.FileName,
                        SchemaVersion = string.IsNullOrEmpty(matchingSchema.Version) 
                            ? matchingSchema.FixedSchemaVersion 
                            : matchingSchema.Version,
                        SchemaPath = matchingSchema.FilePath
                    };
                }
            }
            catch (Exception ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<ValidationError>
                    {
                        new ValidationError
                        {
                            ErrorNumber = 1,
                            Description = $"Произошла непредвиденная ошибка:\n\n{ex.Message}",
                            Details = $"Stack trace:\n{ex.StackTrace}",
                            FullMessage = $"❌ Произошла непредвиденная ошибка:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}"
                        }
                    }
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
            
            // Сразу переводим исходное сообщение на русский
            var message = TranslateStandardXsdError(error.Args.Message);
            var ns = XNamespace.Get("http://www.w3.org/2001/XMLSchema");

            try
            {
                // Извлекаем имя элемента с ошибкой из сообщения
                var elementNameMatch = Regex.Match(message, @"'(\w+)'");
                
                if (!elementNameMatch.Success)
                {
                    return $"⚠️ {message}";
                }

                var errorElementName = elementNameMatch.Groups[1].Value;

                // Находим элемент в XML по номеру строки
                XElement errorElement = FindElementAtLine(xmlDoc, error.LineNumber, errorElementName);
                
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
                        // Показываем только описание без технического названия
                        pathDescriptions.Add($"   → {description}");
                    }
                    else
                    {
                        // Если нет описания, показываем техническое название
                        pathDescriptions.Add($"   → {pathElement}");
                    }
                }
                
                result.AppendLine(string.Join("\n", pathDescriptions));
                result.AppendLine();

                // Анализируем тип ошибки и даем детальное объяснение
                result.AppendLine($"❌ Описание проблемы:");
                
                if (message.IndexOf("pattern constraint", StringComparison.OrdinalIgnoreCase) >= 0 || 
                    message.Contains("шаблон"))
                {
                    var patternExplanation = ExplainPatternError(errorElementName, errorElement, xsdDoc, ns);
                    result.Append(patternExplanation);
                }
                else if (message.Contains("could not be converted") || 
                         message.Contains("не может быть преобразован") ||
                         message.Contains("invalid value") ||
                         message.Contains("is not a valid value"))
                {
                    // Ошибка преобразования типа
                    var typeExplanation = ExplainTypeConversionError(errorElementName, errorElement, xsdDoc, ns, message);
                    result.Append(typeExplanation);
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
                    // Пытаемся перевести стандартное сообщение
                    var translatedMessage = TranslateStandardXsdError(message);
                    result.AppendLine($"   {translatedMessage}");
                    
                    // Показываем значение с ошибкой только для не-типовых ошибок
                    if (!string.IsNullOrWhiteSpace(errorElement.Value))
                    {
                        result.AppendLine();
                        result.AppendLine($"💡 Текущее значение: '{errorElement.Value}'");
                    }
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
        /// <summary>
        /// Парсит текстовое сообщение об ошибке в структурированный объект
        /// </summary>
        private ValidationError ParseErrorToObject(string errorText, int errorNumber)
        {
            var error = new ValidationError
            {
                ErrorNumber = errorNumber,
                FullMessage = errorText
            };

            var lines = errorText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var locationBuilder = new StringBuilder();
            var pathBuilder = new StringBuilder();
            var descriptionBuilder = new StringBuilder();
            var detailsBuilder = new StringBuilder();
            var currentSection = "";

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("═══")) continue;

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
                    var match = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"['\""](.+?)['\""']");
                    if (match.Success)
                    {
                        error.CurrentValue = match.Groups[1].Value;
                    }
                    continue;
                }

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

            error.Location = locationBuilder.Length > 0 ? locationBuilder.ToString() : null;
            error.Path = pathBuilder.Length > 0 ? pathBuilder.ToString() : null;
            error.Description = descriptionBuilder.Length > 0 ? descriptionBuilder.ToString() : null;
            error.Details = detailsBuilder.Length > 0 ? detailsBuilder.ToString() : null;

            return error;
        }

        /// <summary>
        /// Объясняет ошибку преобразования типа с переводом на русский
        /// </summary>
        private string ExplainTypeConversionError(string elementName, XElement errorElement, XDocument xsdDoc, XNamespace ns, string originalMessage)
        {
            var result = new StringBuilder();
            
            // Получаем описание элемента из XSD
            var elementDescription = GetElementDescription(xsdDoc, ns, elementName);
            
            if (!string.IsNullOrEmpty(elementDescription))
            {
                result.AppendLine($"   Поле: {CleanDescription(elementDescription)}");
                result.AppendLine();
            }

            // Ищем определение элемента в XSD
            var elementDef = xsdDoc.Descendants(ns + "element")
                .FirstOrDefault(e => e.Attribute("name")?.Value == elementName);

            if (elementDef != null)
            {
                // Получаем тип элемента
                var typeName = elementDef.Attribute("type")?.Value;
                
                if (!string.IsNullOrEmpty(typeName))
                {
                    // Убираем префикс xs: или xsd:
                    if (typeName.Contains(":"))
                    {
                        typeName = typeName.Split(':')[1];
                    }

                    // Переводим тип на русский
                    if (XsdTypeTranslations.TryGetValue(typeName, out var russianType))
                    {
                        result.AppendLine($"   Требуемый тип данных: {russianType}");
                    }
                    else
                    {
                        result.AppendLine($"   Требуемый тип данных: {typeName}");
                    }
                    result.AppendLine();

                    // Добавляем примеры для популярных типов
                    result.AppendLine($"   ⚙️ Требования:");
                    
                    switch (typeName.ToLower())
                    {
                        case "positiveinteger":
                            result.AppendLine($"   • Должно быть целое число больше нуля");
                            result.AppendLine($"   • Примеры правильных значений: 1, 2, 100, 999");
                            result.AppendLine($"   • Недопустимо: 0, -1, 1.5, текст, пустое значение");
                            break;
                            
                        case "nonnegativeinteger":
                            result.AppendLine($"   • Должно быть целое число, равное нулю или больше");
                            result.AppendLine($"   • Примеры правильных значений: 0, 1, 2, 100");
                            result.AppendLine($"   • Недопустимо: -1, 1.5, текст, пустое значение");
                            break;
                            
                        case "integer":
                        case "int":
                            result.AppendLine($"   • Должно быть целое число");
                            result.AppendLine($"   • Примеры правильных значений: -100, 0, 1, 999");
                            result.AppendLine($"   • Недопустимо: 1.5, текст, пустое значение");
                            break;
                            
                        case "decimal":
                            result.AppendLine($"   • Должно быть число (можно с дробной частью)");
                            result.AppendLine($"   • Примеры правильных значений: 0, 1, 1.5, 99.99, -10.5");
                            result.AppendLine($"   • Недопустимо: текст, пустое значение");
                            break;
                            
                        case "string":
                            result.AppendLine($"   • Должна быть текстовая строка");
                            result.AppendLine($"   • Может содержать любые символы");
                            break;
                            
                        case "date":
                            result.AppendLine($"   • Должна быть дата в формате: ГГГГ-ММ-ДД");
                            result.AppendLine($"   • Примеры правильных значений: 2024-01-15, 2023-12-31");
                            result.AppendLine($"   • Недопустимо: 15.01.2024, 01/15/2024, текст");
                            break;
                            
                        case "datetime":
                            result.AppendLine($"   • Должны быть дата и время в формате: ГГГГ-ММ-ДДTЧЧ:ММ:СС");
                            result.AppendLine($"   • Примеры правильных значений: 2024-01-15T14:30:00");
                            result.AppendLine($"   • Недопустимо: 15.01.2024 14:30, текст");
                            break;
                            
                        case "boolean":
                            result.AppendLine($"   • Должно быть логическое значение");
                            result.AppendLine($"   • Допустимые значения: true, false, 1, 0");
                            result.AppendLine($"   • Недопустимо: да, нет, текст");
                            break;
                    }
                    
                    // Добавляем текущее значение из элемента
                    if (errorElement != null && !string.IsNullOrEmpty(errorElement.Value))
                    {
                        result.AppendLine();
                        result.AppendLine($"   💡 Текущее значение: '{errorElement.Value}'");
                        result.AppendLine($"   ⚠️ Это значение не соответствует требуемому типу данных!");
                    }
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Переводит стандартные ошибки XSD на русский
        /// </summary>
        private string TranslateStandardXsdError(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            // Словарь переводов стандартных сообщений
            var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Основные фразы
                { "The element", "Элемент" },
                { "The value", "Значение" },
                { "is not a valid value", "не является допустимым значением" },
                { "is not a valid value for", "не является допустимым значением для" },
                { "could not be converted", "невозможно преобразовать" },
                { "invalid value", "недопустимое значение" },
                { "is invalid", "недопустим" },
                { "недопустим-", "недопустим." }, // исправляем дефис
                
                // Типы данных
                { "according to its datatype", "согласно типу данных" },
                { "positiveInteger", "положительное целое число" },
                { "nonNegativeInteger", "неотрицательное целое число" },
                { "negativeInteger", "отрицательное целое число" },
                { "integer", "целое число" },
                { "decimal", "десятичное число" },
                { "string", "строка" },
                { "boolean", "логическое значение" },
                { "date", "дата" },
                { "dateTime", "дата и время" },
                { "unsignedShort", "короткое целое число без знака" },
                { "unsignedInt", "целое число без знака" },
                { "unsignedByte", "байт без знака" },
                
                // Ошибки диапазона
                { "was either too large or too small for", "выходит за допустимые пределы для типа" },
                { "Value", "Значение" },
                { "PositiveInteger", "положительное целое число" },
                
                // Структурные ошибки
                { "has invalid child element", "содержит недопустимый дочерний элемент" },
                { "List of possible elements expected", "Ожидается список возможных элементов" },
                { "required attribute", "обязательный атрибут" },
                { "required element", "обязательный элемент" },
                { "missing", "отсутствует" },
                { "not expected", "не ожидается" },
                { "incomplete content", "неполное содержимое" },
                
                // URL и технические термины
                { "http://www.w3.org/2001/XMLSchema", "схема XML" }
            };

            var translated = message;
            
            // Применяем переводы по порядку
            foreach (var pair in translations)
            {
                translated = translated.Replace(pair.Key, pair.Value);
            }
            
            // Удаляем лишние пробелы
            translated = Regex.Replace(translated, @"\s+", " ");
            translated = translated.Trim();

            return translated;
        }

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
                XElement typeDef = null;
                string typeName = elementDef.Attribute("type")?.Value;
                
                if (!string.IsNullOrEmpty(typeName))
                {
                    // Тип указан через атрибут type (explanatorynote.xsd)
                    if (typeName.Contains(":"))
                    {
                        typeName = typeName.Split(':')[1];
                    }

                    typeDef = xsdDoc.Descendants(ns + "simpleType")
                        .FirstOrDefault(t => t.Attribute("name")?.Value == typeName);
                }
                else
                {
                    // Тип определён inline внутри элемента (MarketAnalysis.xsd)
                    typeDef = elementDef.Element(ns + "simpleType");
                    typeName = "inline";
                }

                if (typeDef != null)
                {
                    var typeDescription = typeDef.Descendants(ns + "documentation")
                        .FirstOrDefault(d => d.Attribute(XNamespace.Xml + "lang")?.Value == "ru");

                    // Если нет русской, берем первую любую
                    if (typeDescription == null)
                    {
                        typeDescription = typeDef.Descendants(ns + "documentation").FirstOrDefault();
                    }

                    if (typeDescription != null)
                    {
                        var textElement = typeDescription.Element("text");
                        var typeDesc = textElement != null ? textElement.Value.Trim() : typeDescription.Value.Trim();
                        result.AppendLine($"   Тип данных: {CleanDescription(typeDesc)}");
                    }

                    // Ищем pattern и length в restriction
                    var restriction = typeDef.Descendants(ns + "restriction").FirstOrDefault();
                    if (restriction != null)
                    {
                        var pattern = restriction.Element(ns + "pattern")?.Attribute("value")?.Value;
                        var length = restriction.Element(ns + "length")?.Attribute("value")?.Value;
                        var minLength = restriction.Element(ns + "minLength")?.Attribute("value")?.Value;
                        var maxLength = restriction.Element(ns + "maxLength")?.Attribute("value")?.Value;

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
                }
                else if (!string.IsNullOrEmpty(typeName))
                {
                    result.AppendLine($"   Тип данных: {typeName}");
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Переводит регулярное выражение на человеческий язык
        /// </summary>
        private string ExplainPattern(string pattern)
        {
            // Проверяем паттерны с OR (|)
            if (pattern.Contains("|"))
            {
                var parts = pattern.Split('|');
                var explanations = new List<string>();
                
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    
                    // Пустой паттерн
                    if (string.IsNullOrEmpty(trimmed))
                    {
                        explanations.Add("пустое значение");
                        continue;
                    }
                    
                    // [0-9]{10} или \d{10}
                    var digitMatch = Regex.Match(trimmed, @"\{(\d+)\}");
                    if (digitMatch.Success)
                    {
                        var count = digitMatch.Groups[1].Value;
                        explanations.Add($"{count} цифр");
                        continue;
                    }
                    
                    explanations.Add(trimmed);
                }
                
                return string.Join(" или ", explanations);
            }
            
            // Одиночные паттерны
            if (pattern == @"\d{13}")
                return "13 цифр";
            
            if (pattern == @"\d{10}")
                return "10 цифр";
            
            if (pattern == @"\d{12}")
                return "12 цифр";
            
            if (pattern == @"\d{9}")
                return "9 цифр";
            
            // Любой паттерн с {N}
            var match = Regex.Match(pattern, @"\{(\d+)\}");
            if (match.Success)
            {
                return $"{match.Groups[1].Value} цифр";
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
                var documentations = element.Descendants(ns + "documentation").ToList();
                
                // Сначала ищем русскую документацию
                var documentation = documentations
                    .FirstOrDefault(d => d.Attribute(XNamespace.Xml + "lang")?.Value == "ru");

                // Если не нашли русскую, берем первую любую документацию
                if (documentation == null && documentations.Count > 0)
                {
                    documentation = documentations[0];
                }

                if (documentation != null)
                {
                    // Если внутри есть <text>, берем только его содержимое (без <links>)
                    var textElement = documentation.Element("text");
                    if (textElement != null)
                    {
                        return CleanDescription(textElement.Value);
                    }
                    
                    // Иначе берем весь текст документации, но очищаем от links
                    var allText = documentation.Value;
                    
                    // Убираем текст из <links> если он есть
                    var linksElement = documentation.Element("links");
                    if (linksElement != null)
                    {
                        allText = allText.Replace(linksElement.Value, "");
                    }
                    
                    return CleanDescription(allText);
                }
            }

            return null;
        }

        /// <summary>
        /// Очищает описание от лишних пробелов и переносов строк
        /// </summary>
        private string CleanDescription(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            // Убираем лишние пробелы и переносы
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            
            return text.Trim();
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
            if (fileInfo == null || string.IsNullOrEmpty(fileInfo.SchemaLocation))
                return null;

            // Извлекаем имя файла схемы из SchemaLocation (например: "MarketAnalysis-3_01.xsd")
            var schemaFileName = Path.GetFileName(fileInfo.SchemaLocation);
            
            System.Diagnostics.Debug.WriteLine($"[FindMatchingSchema] Ищем схему: {schemaFileName}, версия: {fileInfo.Version}");

            // ПРИОРИТЕТ 1: Точное совпадение по имени файла И версии
            var exactMatch = _availableSchemas.FirstOrDefault(s =>
                s.FileName.Equals(schemaFileName, StringComparison.OrdinalIgnoreCase) &&
                (s.Version == fileInfo.Version || s.FixedSchemaVersion == fileInfo.Version));

            if (exactMatch != null)
            {
                System.Diagnostics.Debug.WriteLine($"[FindMatchingSchema] ✓ Найдено точное совпадение: {exactMatch.FileName}");
                return exactMatch;
            }

            // ПРИОРИТЕТ 2: Совпадение по имени файла (игнорируем версию)
            var nameMatch = _availableSchemas.FirstOrDefault(s =>
                s.FileName.Equals(schemaFileName, StringComparison.OrdinalIgnoreCase));

            if (nameMatch != null)
            {
                System.Diagnostics.Debug.WriteLine($"[FindMatchingSchema] ⚠ Найдено совпадение по имени: {nameMatch.FileName} (версия может не совпадать)");
                return nameMatch;
            }

            // ПРИОРИТЕТ 3: Попытка найти по базовому имени без версии
            // Например: "MarketAnalysis-3_01.xsd" -> "MarketAnalysis"
            var baseSchemaName = ExtractBaseSchemaName(schemaFileName);
            
            if (!string.IsNullOrEmpty(baseSchemaName))
            {
                var baseNameMatch = _availableSchemas.FirstOrDefault(s =>
                {
                    var candidateBaseName = ExtractBaseSchemaName(s.FileName);
                    return candidateBaseName.Equals(baseSchemaName, StringComparison.OrdinalIgnoreCase);
                });

                if (baseNameMatch != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[FindMatchingSchema] ⚠ Найдено совпадение по базовому имени: {baseNameMatch.FileName}");
                    return baseNameMatch;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[FindMatchingSchema] ✗ Схема не найдена для: {schemaFileName}");
            return null;
        }

        /// <summary>
        /// Извлекает базовое имя схемы без версии
        /// Например: "MarketAnalysis-3_01.xsd" -> "MarketAnalysis"
        /// "LocalEstimateResourceIndexMethod-3_01.xsd" -> "LocalEstimateResourceIndexMethod"
        /// </summary>
        private string ExtractBaseSchemaName(string schemaFileName)
        {
            if (string.IsNullOrEmpty(schemaFileName))
                return string.Empty;

            // Убираем расширение .xsd
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(schemaFileName);
            
            // Убираем версию (всё после последнего дефиса с цифрами)
            // "MarketAnalysis-3_01" -> "MarketAnalysis"
            // "LocalEstimateResourceIndexMethod-3_01" -> "LocalEstimateResourceIndexMethod"
            var match = System.Text.RegularExpressions.Regex.Match(nameWithoutExtension, @"^(.+?)[-_]\d+");
            
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return nameWithoutExtension;
        }
    }
}
