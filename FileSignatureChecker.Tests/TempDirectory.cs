using System.Text;

namespace FileSignatureChecker.Tests;

/// <summary>
/// Временная папка для теста, удаляется после него.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "fsc_tests_" + Guid.NewGuid().ToString("N")[..8]);

    public TempDirectory() => Directory.CreateDirectory(Path);

    /// <summary>Создаёт файл (и при необходимости подпапки) и возвращает полный путь.</summary>
    public string Write(string relativePath, string content)
    {
        var full = Combine(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content, new UTF8Encoding(false));
        return full;
    }

    public string Combine(string relativePath) => System.IO.Path.Combine(Path,
        relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { /* антивирус или индексатор держит файл — не валим тест */ }
    }
}
