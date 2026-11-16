using FileSignatureChecker.Services;

namespace FileSignatureChecker.Tests;

public class Crc32ServiceTests
{
    [Fact]
    public void CalculateChecksum_StandardCheckValue()
    {
        // Эталонное значение CRC-32 (IEEE) для строки "123456789"
        using var dir = new TempDirectory();
        var file = dir.Write("check.txt", "123456789");

        Assert.Equal("CBF43926", Crc32Service.CalculateChecksum(file));
    }

    [Fact]
    public void CalculateChecksum_EmptyFile_IsZero()
    {
        using var dir = new TempDirectory();
        var file = dir.Write("empty.bin", "");

        Assert.Equal("00000000", Crc32Service.CalculateChecksum(file));
    }

    [Fact]
    public void CalculateChecksum_MissingFile_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, Crc32Service.CalculateChecksum(@"Z:\нет\такого\файла.pdf"));
    }

    [Theory]
    [InlineData("3FC10558", "3FC10558")]
    [InlineData("3fc10558", "3FC10558")]
    [InlineData(" 3FC10558 ", "3FC10558")] // пробелы/переносы из XML
    public void CompareChecksums_Matches(string a, string b)
    {
        Assert.True(Crc32Service.CompareChecksums(a, b));
    }

    [Theory]
    [InlineData("", "")]           // контрольная сумма не указана в XML и файл не прочитан
    [InlineData("  ", "")]
    [InlineData("3FC10558", "")]
    [InlineData("3FC10558", "29C68BA0")]
    public void CompareChecksums_DoesNotMatch(string a, string b)
    {
        Assert.False(Crc32Service.CompareChecksums(a, b));
    }
}
