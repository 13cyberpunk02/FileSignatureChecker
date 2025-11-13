namespace FileSignatureChecker.Models;


/// <summary>
/// Информация о схеме из XML/GGE файла
/// </summary>
public class FileSchemaInfo
{
    public string SchemaLocation { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}