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
    /// Список всех ошибок валидации
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new List<ValidationError>();

    /// <summary>
    /// Сообщение об ошибке (для обратной совместимости - возвращает первую ошибку)
    /// </summary>
    public string ErrorMessage => Errors.Count > 0 ? Errors[0].FullMessage : "";

    /// <summary>
    /// Имя файла схемы XSD, который использовался
    /// </summary>
    public string SchemaFileName { get; set; }

    /// <summary>
    /// Версия схемы
    /// </summary>
    public string SchemaVersion { get; set; }

    /// <summary>
    /// Путь к файлу XSD
    /// </summary>
    public string SchemaPath { get; set; }
}