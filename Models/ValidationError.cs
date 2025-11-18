namespace FileSignatureChecker.Models;

/// <summary>
/// Представляет одну ошибку валидации
/// </summary>
public class ValidationError
{
    public int ErrorNumber { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public string FullMessage { get; set; } = string.Empty;
}