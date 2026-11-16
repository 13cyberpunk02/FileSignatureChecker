namespace FileSignatureChecker.Models;

/// <summary>
/// Представляет одну ошибку валидации
/// </summary>
public class ValidationError
{
    public int ErrorNumber { get; set; }
    public string? Location { get; set; }
    public string? Path { get; set; }
    public string? Description { get; set; }
    public string? Details { get; set; }
    public string? CurrentValue { get; set; }
    public string FullMessage { get; set; } = string.Empty;
}