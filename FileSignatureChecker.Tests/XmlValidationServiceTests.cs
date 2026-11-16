using FileSignatureChecker.Models;
using FileSignatureChecker.Services;

namespace FileSignatureChecker.Tests;

public sealed class XmlValidationServiceTests : IDisposable
{
    private static readonly string TestSchema =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Schemas", "TestNote-01-01.xsd");

    private readonly TempDirectory _dir = new();
    private readonly string _schemas;

    public XmlValidationServiceTests()
    {
        _schemas = _dir.Combine("schemas");
        Directory.CreateDirectory(_schemas);
        File.Copy(TestSchema, Path.Combine(_schemas, "TestNote-01-01.xsd"));
    }

    public void Dispose() => _dir.Dispose();

    // ---------- помощники ----------

    private static string Note(
        string inn = "1234567890",
        string personName = "Иванов Иван",
        string total = "100.50",
        string date = "2024-01-15",
        string version = "01.01",
        string schema = "TestNote-01-01.xsd") => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <TestNote xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="{schema}" SchemaVersion="{version}">
          <Organization>
            <Name>ООО «Тест»</Name>
            <INN>{inn}</INN>
          </Organization>
          <Person>
            <Name>{personName}</Name>
          </Person>
          <TotalValue>{total}</TotalValue>
          <DocDate>{date}</DocDate>
        </TestNote>
        """;

    private Task<ValidationResult> Validate(string xml, string fileName = "note.xml")
        => new XmlValidationService(_schemas).ValidateFileAsync(_dir.Write(fileName, xml));

    private static void AssertNoEmptyCards(ValidationResult result)
        => Assert.All(result.Errors, e => Assert.False(string.IsNullOrWhiteSpace(e.Description),
            $"Ошибка №{e.ErrorNumber} без описания: {e.FullMessage}"));

    // ---------- корректный файл ----------

    [Fact]
    public async Task ValidFile_Passes()
    {
        var result = await Validate(Note());

        Assert.True(result.IsValid, string.Join("\n", result.Errors.Select(e => e.FullMessage)));
        Assert.Equal("TestNote-01-01.xsd", result.SchemaFileName);
        Assert.Equal("01.01", result.SchemaVersion);
        Assert.Null(result.SchemaWarning);
    }

    [Fact]
    public async Task GgeExtension_IsValidatedLikeXml()
    {
        var result = await Validate(Note(), "estimate.gge");

        Assert.True(result.IsValid);
    }

    // ---------- ошибки формата (pattern) ----------

    [Fact]
    public async Task PatternError_ExplainsFormatAndShowsValue()
    {
        var result = await Validate(Note(inn: "78400dasd97047"));

        var error = Assert.Single(result.Errors);
        Assert.False(result.IsValid);
        Assert.Contains("не соответствует требуемому формату", error.Description);
        Assert.Contains("ИНН организации", error.Description);
        Assert.Contains("10 цифр или 12 цифр", error.Details);
        Assert.Equal("78400dasd97047", error.CurrentValue);
        Assert.Contains("Строка 5", error.Location);
    }

    [Fact]
    public async Task SameElementNameInDifferentSections_UsesDescriptionOfRightSection()
    {
        // Регрессия: описание бралось у первого xs:element с таким именем в схеме
        // (Organization/Name вместо Person/Name, объявленного через тип tPerson)
        var result = await Validate(Note(personName: "Ivanov"));

        var error = Assert.Single(result.Errors);
        Assert.Contains("ФИО ответственного лица", error.Description);
        Assert.DoesNotContain("Наименование организации", error.Description);
        Assert.Contains("ФИО ответственного лица", error.Path);
        Assert.Equal("Ivanov", error.CurrentValue);
    }

    // ---------- ошибки типа данных ----------

    [Fact]
    public async Task DecimalError_KeepsElementNameAndExplainsType()
    {
        // Регрессия: 'TotalValue' переводилось как 'TotalЗначение'
        var result = await Validate(Note(total: "сто рублей"));

        var error = Assert.Single(result.Errors);
        Assert.Contains("'TotalValue'", error.Description);
        Assert.DoesNotContain("TotalЗначение", error.FullMessage);
        Assert.Contains("Итоговая стоимость", error.Description);
        Assert.Contains("число", error.Details);
        Assert.Equal("сто рублей", error.CurrentValue);
    }

    [Fact]
    public async Task DateError_ExplainsExpectedFormat()
    {
        var result = await Validate(Note(date: "15.01.2024"));

        var error = Assert.Single(result.Errors);
        Assert.Contains("'DocDate'", error.Description);
        Assert.Contains("ГГГГ-ММ-ДД", error.Details);
        Assert.Equal("15.01.2024", error.CurrentValue);
    }

    [Fact]
    public async Task SeveralErrors_AreNumberedAndAllHaveDescriptions()
    {
        var result = await Validate(Note(inn: "abc", personName: "Ivanov", total: "x", date: "y"));

        Assert.Equal(4, result.Errors.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Errors.Select(e => e.ErrorNumber));
        AssertNoEmptyCards(result);
    }

    // ---------- атрибуты и структура ----------

    [Fact]
    public async Task WrongFixedAttribute_DoesNotDumpWholeDocument()
    {
        // Регрессия: для ошибки атрибута "текущим значением" становился текст всего документа
        var result = await Validate(Note(version: "09.09"));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Contains("'SchemaVersion'", error.Description);
        Assert.Contains("фиксированным значением", error.Description);
        Assert.Null(error.CurrentValue);
        Assert.DoesNotContain("Иванов", error.FullMessage);
    }

    [Fact]
    public async Task MissingRequiredElement_HasDescription()
    {
        var xml = Note().Replace("<DocDate>2024-01-15</DocDate>", "");

        var result = await Validate(xml);

        Assert.False(result.IsValid);
        AssertNoEmptyCards(result);
        Assert.Contains("DocDate", result.Errors[0].FullMessage);
    }

    [Fact]
    public async Task UnexpectedElement_HasDescription()
    {
        var xml = Note().Replace("<TotalValue>", "<Extra>1</Extra><TotalValue>");

        var result = await Validate(xml);

        Assert.False(result.IsValid);
        AssertNoEmptyCards(result);
    }

    // ---------- некорректный XML ----------

    [Fact]
    public async Task BrokenMarkup_ReportsLineAndPosition()
    {
        var result = await Validate("<?xml version=\"1.0\"?>\n<Root>\n  <A>1</B>\n</Root>");

        var error = Assert.Single(result.Errors);
        Assert.False(result.IsValid);
        Assert.StartsWith("Строка 3, позиция", error.Location);
        Assert.Contains("нарушена разметка", error.Description);
    }

    // ---------- подбор схемы ----------

    [Fact]
    public async Task SchemaNotFound_ListsAvailableSchemas()
    {
        var result = await Validate(Note(schema: "Unknown-1_01.xsd"));

        var error = Assert.Single(result.Errors);
        Assert.Contains("Не найдена подходящая XSD схема", error.Description);
        Assert.Contains("TestNote-01-01.xsd", error.Description);
    }

    [Fact]
    public async Task FileWithoutSchemaReference_ReportsMissingSchema()
    {
        var xml = Note().Replace("xsi:noNamespaceSchemaLocation=\"TestNote-01-01.xsd\"", "");

        var result = await Validate(xml);

        Assert.False(result.IsValid);
        Assert.Contains("Не найдена подходящая XSD схема", Assert.Single(result.Errors).Description);
    }

    [Fact]
    public async Task OtherVersionOfSchema_UsesLatestAndWarns()
    {
        // Схема 01-03 отсутствует: проверяем по самой новой из семейства (01-02), а не по первой попавшейся
        File.WriteAllText(Path.Combine(_schemas, "TestNote-01-02.xsd"),
            File.ReadAllText(TestSchema).Replace("fixed=\"01.01\"", "fixed=\"01.02\""));

        var result = await Validate(Note(schema: "TestNote-01-03.xsd", version: "01.02"));

        Assert.True(result.IsValid);
        Assert.Equal("TestNote-01-02.xsd", result.SchemaFileName);
        Assert.NotNull(result.SchemaWarning);
        Assert.Contains("TestNote-01-03.xsd", result.SchemaWarning);
    }

    [Fact]
    public async Task SameSchemaNameButDifferentVersion_Warns()
    {
        var result = await Validate(Note(version: "01.07"));

        Assert.NotNull(result.SchemaWarning);
        Assert.Contains("01.07", result.SchemaWarning);
    }

    [Theory]
    [InlineData("01.01")]
    [InlineData("1.1")] // версии сравниваются без ведущих нулей
    public async Task MatchingVersion_NoWarning(string version)
    {
        // Схема требует fixed="01.01", поэтому при "1.1" будет ошибка атрибута — но не предупреждение о версии
        var result = await Validate(Note(version: version));

        Assert.Null(result.SchemaWarning);
    }

    [Fact]
    public void MissingSchemaDirectory_IsNotCreated()
    {
        // Регрессия: папка создавалась при запуске, и в Program Files окно падало без прав на запись
        var missing = _dir.Combine("no-such-folder");

        _ = new XmlValidationService(missing);

        Assert.False(Directory.Exists(missing));
    }

    // ---------- реальные схемы Минстроя ----------

    [Fact]
    public async Task AllMinstroySchemas_AreLoaded()
    {
        var minstroy = Path.Combine(AppContext.BaseDirectory, "MinstroySchemas");
        var expected = Directory.GetFiles(minstroy, "*.xsd").Select(Path.GetFileName).ToList();
        Assert.NotEmpty(expected);

        // Схемы загружаются молча, поэтому проверяем через сообщение "доступные схемы"
        var result = await new XmlValidationService(minstroy).ValidateFileAsync(_dir.Write("x.xml",
            "<Root xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"Nope.xsd\" />"));

        var description = Assert.Single(result.Errors).Description;
        Assert.All(expected, name => Assert.Contains(name!, description));
    }
}
