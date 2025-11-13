using System.Collections.Generic;

namespace FileSignatureChecker.Models;


/// <summary>
/// Результат валидации XML/GGE файла
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Прошла ли валидация успешно
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Сообщение об ошибке (понятное человеку)
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Имя файла схемы XSD, который использовался
    /// </summary>
    public string SchemaFileName { get; set; } = string.Empty;

    /// <summary>
    /// Версия схемы
    /// </summary>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>
    /// Путь к файлу XSD
    /// </summary>
    public string SchemaPath { get; set; } = string.Empty;
    
    public List<ColoredTextPart> ColoredMessage { get; set; }
}

public class ColoredTextPart
{
    public string Text { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
}