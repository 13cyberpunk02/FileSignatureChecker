namespace FileSignatureChecker.Models;

/// <summary>
/// Информация о XSD схеме
/// </summary>
public class XsdSchemaInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string FixedSchemaVersion { get; set; } = string.Empty;
}