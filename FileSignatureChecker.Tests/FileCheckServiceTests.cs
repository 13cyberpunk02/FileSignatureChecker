using FileSignatureChecker.Models;
using FileSignatureChecker.Services;

namespace FileSignatureChecker.Tests;

public sealed class FileCheckServiceTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly FileCheckService _service = new();

    public void Dispose() => _dir.Dispose();

    // ---------- помощники ----------

    private string Crc(string relativePath) => Crc32Service.CalculateChecksum(_dir.Combine(relativePath));

    /// <summary>Описание файла в XML с реальной контрольной суммой файла из папки.</summary>
    private XmlFileInfo XmlFile(string name, params string[] signatures) => new()
    {
        FileName = name,
        FileChecksum = Crc(name),
        SignFiles = signatures.Select(s => new SignFileInfo { FileName = s, FileChecksum = Crc(s) }).ToList()
    };

    private List<FileCheckResult> Check(params XmlFileInfo[] files)
    {
        var document = new Document { DocName = "Раздел 1", DocType = "01.01", DocNumber = "№1", DocDate = "2024-01-01" };
        document.Files.AddRange(files);
        return _service.CheckFiles([document], _dir.Path);
    }

    private static FileCheckResult For(List<FileCheckResult> results, string fileName)
        => results.Single(r => r.FileName == fileName);

    // ---------- основной файл ----------

    [Fact]
    public void FileAndSignatureMatch_Success()
    {
        _dir.Write("doc.pdf", "content");
        _dir.Write("doc.pdf.sig", "signature");

        var result = For(Check(XmlFile("doc.pdf", "doc.pdf.sig")), "doc.pdf");

        Assert.Equal(CheckStatus.Success, result.Status);
        Assert.True(result.FileFound);
        Assert.True(result.SignatureFound);
        Assert.Equal("Раздел 1", result.DocName);
        Assert.Equal(_dir.Combine("doc.pdf"), result.FilePath);
    }

    [Fact]
    public void FileChangedAfterUpload_Warning()
    {
        _dir.Write("doc.pdf", "original");
        var xml = XmlFile("doc.pdf");
        _dir.Write("doc.pdf", "edited");

        var result = For(Check(xml), "doc.pdf");

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Contains("контрольная сумма не совпадает", result.Message);
    }

    [Fact]
    public void RenamedFile_FoundByChecksum()
    {
        _dir.Write("old name.pdf", "content");
        var xml = XmlFile("old name.pdf");
        File.Move(_dir.Combine("old name.pdf"), _dir.Combine("new name.pdf"));

        var result = For(Check(xml), "old name.pdf");

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Equal(_dir.Combine("new name.pdf"), result.FilePath);
        Assert.Contains("new name.pdf", result.Message);
    }

    [Fact]
    public void RenamedFile_FoundByChecksum_InLargeDirectory()
    {
        // При > 100 файлов поиск по CRC идёт параллельно: проверяем, что кэш сумм потокобезопасен
        for (var i = 0; i < 150; i++)
            _dir.Write($"other/file{i}.pdf", $"content {i}");
        _dir.Write("target.pdf", "the one");
        var xml = XmlFile("target.pdf");
        File.Move(_dir.Combine("target.pdf"), _dir.Combine("other/renamed.pdf"));

        var result = For(Check(xml), "target.pdf");

        Assert.Equal(_dir.Combine("other/renamed.pdf"), result.FilePath);
    }

    [Fact]
    public void MissingFile_Error()
    {
        var result = For(Check(new XmlFileInfo { FileName = "нет.pdf", FileChecksum = "12345678" }), "нет.pdf");

        Assert.Equal(CheckStatus.Error, result.Status);
        Assert.False(result.FileFound);
    }

    [Fact]
    public void EmptyChecksumInXml_DoesNotMatchArbitraryFile()
    {
        // Регрессия: пустая сумма совпадала с пустой суммой нечитаемого файла
        _dir.Write("какой-то.pdf", "x");

        var result = For(Check(new XmlFileInfo { FileName = "нет.pdf", FileChecksum = "" }), "нет.pdf");

        Assert.Equal(CheckStatus.Error, result.Status);
        Assert.False(result.FileFound);
    }

    // ---------- подписи из XML ----------

    [Fact]
    public void SignatureMissing_Warning()
    {
        _dir.Write("doc.pdf", "content");
        var xml = new XmlFileInfo
        {
            FileName = "doc.pdf",
            FileChecksum = Crc("doc.pdf"),
            SignFiles = [new SignFileInfo { FileName = "doc.pdf.sig", FileChecksum = "DEADBEEF" }]
        };

        var result = For(Check(xml), "doc.pdf");

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Contains("Подпись 'doc.pdf.sig' не найдена", result.Message);
    }

    [Fact]
    public void SignatureChanged_Warning()
    {
        _dir.Write("doc.pdf", "content");
        _dir.Write("doc.pdf.sig", "old signature");
        var xml = XmlFile("doc.pdf", "doc.pdf.sig");
        _dir.Write("doc.pdf.sig", "new signature");

        var result = For(Check(xml), "doc.pdf");

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Contains("контрольная сумма не совпадает", result.Message);
    }

    // ---------- лишние подписи в папке ----------

    [Fact]
    public void SignatureOfAnotherFileWithSimilarName_IsNotExtra()
    {
        // Регрессия: "11.pdf.sig" содержит "1.pdf" и раньше считался лишней подписью файла 1.pdf
        _dir.Write("1.pdf", "one");
        _dir.Write("1.pdf.sig", "sig one");
        _dir.Write("11.pdf", "eleven");
        _dir.Write("11.pdf.sig", "sig eleven");
        _dir.Write("Раздел1.pdf.sig", "sig of another file");

        var results = Check(XmlFile("1.pdf", "1.pdf.sig"), XmlFile("11.pdf", "11.pdf.sig"));

        Assert.Equal(CheckStatus.Success, For(results, "1.pdf").Status);
        Assert.Equal(CheckStatus.Success, For(results, "11.pdf").Status);
    }

    [Fact]
    public void SignatureListedForAnotherFile_IsNotExtra()
    {
        // Регрессия: подпись, указанная в XML у другого файла, считалась лишней
        _dir.Write("a.pdf", "a");
        _dir.Write("b.pdf", "b");
        _dir.Write("Иванов_a.pdf.sig", "sig");

        var results = Check(XmlFile("a.pdf"), XmlFile("b.pdf", "Иванов_a.pdf.sig"));

        Assert.Equal(CheckStatus.Success, For(results, "a.pdf").Status);
    }

    [Fact]
    public void UnlistedSignatureInDirectory_Warning()
    {
        _dir.Write("1.pdf", "one");
        _dir.Write("1.pdf.sig", "sig");
        _dir.Write("Иванов_1.pdf.sig", "second signer");

        var result = For(Check(XmlFile("1.pdf", "1.pdf.sig")), "1.pdf");

        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Contains("Иванов_1.pdf.sig", result.Message);
        Assert.Contains("дополнительная подпись", result.Message);
    }

    // ---------- файлы, которых нет в XML ----------

    [Fact]
    public void FileWithSignatureNotInXml_Info()
    {
        _dir.Write("listed.pdf", "listed");
        _dir.Write("forgotten.pdf", "forgotten");
        _dir.Write("forgotten.pdf.sig", "sig");
        _dir.Write("no-signature.pdf", "plain");

        var results = Check(XmlFile("listed.pdf"));

        var info = Assert.Single(results, r => r.Status == CheckStatus.Info);
        Assert.Equal("forgotten.pdf", info.FileName);
        Assert.Equal("forgotten.pdf.sig", info.SignatureFileName);
        Assert.DoesNotContain(results, r => r.FileName == "no-signature.pdf");
    }

    [Fact]
    public void FileFoundByChecksum_IsNotReportedAsUnlisted()
    {
        _dir.Write("old.pdf", "content");
        _dir.Write("old.pdf.sig", "sig");
        var xml = XmlFile("old.pdf", "old.pdf.sig");
        File.Move(_dir.Combine("old.pdf"), _dir.Combine("new.pdf"));
        File.Move(_dir.Combine("old.pdf.sig"), _dir.Combine("new.pdf.sig"));

        var results = Check(xml);

        Assert.DoesNotContain(results, r => r.Status == CheckStatus.Info);
    }

    // ---------- структура папки ----------

    [Fact]
    public void DuplicateNamesInSubfolders_ShallowestCheckedAndWarned()
    {
        _dir.Write("doc.pdf", "root copy");
        _dir.Write("archive/old/doc.pdf", "old copy");
        var xml = XmlFile("doc.pdf");

        var result = For(Check(xml), "doc.pdf");

        Assert.Equal(_dir.Combine("doc.pdf"), result.FilePath);
        Assert.Equal(CheckStatus.Warning, result.Status);
        Assert.Contains(_dir.Combine(Path.Combine("archive", "old", "doc.pdf")), result.Message);
    }

    [Fact]
    public void FilesInSubfoldersAreFound()
    {
        var folder = Path.Combine("Том 1", "Раздел 2");
        _dir.Write(Path.Combine(folder, "doc.pdf"), "content");
        _dir.Write(Path.Combine(folder, "doc.pdf.sig"), "sig");
        var xml = new XmlFileInfo
        {
            FileName = "doc.pdf",
            FileChecksum = Crc(Path.Combine(folder, "doc.pdf")),
            SignFiles = [new SignFileInfo { FileName = "doc.pdf.sig", FileChecksum = Crc(Path.Combine(folder, "doc.pdf.sig")) }]
        };

        var result = For(Check(xml), "doc.pdf");

        Assert.Equal(CheckStatus.Success, result.Status);
        Assert.Equal(_dir.Combine(Path.Combine(folder, "doc.pdf")), result.FilePath);
    }

    [Fact]
    public void RepeatedChecks_DoNotReuseStaleChecksums()
    {
        _dir.Write("doc.pdf", "v1");
        var xml = XmlFile("doc.pdf");
        Assert.Equal(CheckStatus.Success, For(Check(xml), "doc.pdf").Status);

        _dir.Write("doc.pdf", "v2");

        Assert.Equal(CheckStatus.Warning, For(Check(xml), "doc.pdf").Status);
    }
}
