using FileSignatureChecker.Services;

namespace FileSignatureChecker.Tests;

public class XmlParserServiceTests
{
    private const string ExplanatoryNote = """
        <?xml version="1.0" encoding="utf-8"?>
        <ExplanatoryNote SchemaVersion="01.05">
          <ProjectInitialDocuments>
            <Document>
              <DocType>99.99</DocType>
              <DocName>Задание на проектирование</DocName>
              <DocNumber>№ 1</DocNumber>
              <DocDate>2023-05-03</DocDate>
              <DocIssueAuthor>Заказчик</DocIssueAuthor>
              <File>
                <FileName>задание.pdf</FileName>
                <FileFormat>pdf</FileFormat>
                <FileChecksum> 3FC10558 </FileChecksum>
                <SignFile>
                  <FileName>задание.pdf.sig</FileName>
                  <FileFormat>sig</FileFormat>
                  <FileChecksum>29C68BA0</FileChecksum>
                </SignFile>
                <SignFile>
                  <FileName>задание.pdf.2.sig</FileName>
                  <FileFormat>sig</FileFormat>
                  <FileChecksum>11111111</FileChecksum>
                </SignFile>
              </File>
              <File>
                <FileName>приложение.xlsx</FileName>
                <FileFormat>xlsx</FileFormat>
                <FileChecksum>22222222</FileChecksum>
              </File>
            </Document>
          </ProjectInitialDocuments>
          <Section10>
            <SectionContent>
              <CivilDefense>
                <OtherDocument>
                  <DocType>08.01</DocType>
                  <DocName>Мероприятия ГО</DocName>
                  <DocNumber>ГО-1</DocNumber>
                  <DocDate>2024-01-15</DocDate>
                  <File>
                    <FileName>го.pdf</FileName>
                    <FileFormat>pdf</FileFormat>
                    <FileChecksum>33333333</FileChecksum>
                  </File>
                </OtherDocument>
              </CivilDefense>
            </SectionContent>
          </Section10>
          <EngineeringSurveyDocuments>
            <Document>
              <DocName>Отчёт ИГИ</DocName>
              <File>
                <FileName>иги.pdf</FileName>
                <FileChecksum>44444444</FileChecksum>
              </File>
              <IULFile>
                <FileName>иги_иул.pdf</FileName>
                <FileChecksum>55555555</FileChecksum>
              </IULFile>
            </Document>
          </EngineeringSurveyDocuments>
          <AdditionalData>
            <DocName>Дополнительные материалы</DocName>
            <Data>
              <File>
                <FileName>доп.pdf</FileName>
                <FileChecksum>66666666</FileChecksum>
              </File>
            </Data>
          </AdditionalData>
        </ExplanatoryNote>
        """;

    private static List<Models.Document> Parse(string xml)
    {
        using var dir = new TempDirectory();
        return XmlParserService.ParseXmlFile(dir.Write("note.xml", xml));
    }

    [Fact]
    public void ParsesDocumentFieldsFilesAndAllSignatures()
    {
        var doc = Parse(ExplanatoryNote).Single(d => d.DocName == "Задание на проектирование");

        Assert.Equal("99.99", doc.DocType);
        Assert.Equal("№ 1", doc.DocNumber);
        Assert.Equal("2023-05-03", doc.DocDate);
        Assert.Equal("Заказчик", doc.DocIssueAuthor);
        Assert.Equal(new[] { "задание.pdf", "приложение.xlsx" }, doc.Files.Select(f => f.FileName));

        var main = doc.Files[0];
        Assert.Equal("3FC10558", main.FileChecksum); // пробелы обрезаны
        Assert.Equal(new[] { "задание.pdf.sig", "задание.pdf.2.sig" }, main.SignFiles.Select(s => s.FileName));
        Assert.Empty(doc.Files[1].SignFiles);
    }

    [Fact]
    public void ParsesFilesFromOtherDocument()
    {
        // Регрессия: раньше учитывались только элементы <Document>,
        // и файлы из OtherDocument попадали в "не загружен в пояснительную записку"
        var doc = Parse(ExplanatoryNote).Single(d => d.DocName == "Мероприятия ГО");

        Assert.Equal("го.pdf", Assert.Single(doc.Files).FileName);
        Assert.Equal("08.01", doc.DocType);
    }

    [Fact]
    public void ParsesInformationCertificationSheet()
    {
        var doc = Parse(ExplanatoryNote).Single(d => d.DocName == "Отчёт ИГИ");

        Assert.Equal(new[] { "иги.pdf", "иги_иул.pdf" }, doc.Files.Select(f => f.FileName));
    }

    [Fact]
    public void DataWithoutDocName_TakesNameFromParent()
    {
        var doc = Parse(ExplanatoryNote).Single(d => d.Files.Any(f => f.FileName == "доп.pdf"));

        Assert.Equal("Дополнительные материалы", doc.DocName);
    }

    [Fact]
    public void CountsEveryFileExactlyOnce()
    {
        var files = Parse(ExplanatoryNote).SelectMany(d => d.Files).Select(f => f.FileName).ToList();

        Assert.Equal(6, files.Count);
        Assert.Equal(files.Count, files.Distinct().Count());
        Assert.DoesNotContain("задание.pdf.sig", files); // подписи не считаются отдельными файлами
    }

    [Fact]
    public void SupportsDefaultNamespace()
    {
        const string xml = """
            <ExplanatoryNote xmlns="urn:test">
              <Document>
                <DocName>Раздел</DocName>
                <File><FileName>a.pdf</FileName><FileChecksum>1</FileChecksum>
                  <SignFile><FileName>a.pdf.sig</FileName><FileChecksum>2</FileChecksum></SignFile>
                </File>
              </Document>
            </ExplanatoryNote>
            """;

        var file = Parse(xml).Single().Files.Single();

        Assert.Equal("a.pdf", file.FileName);
        Assert.Equal("a.pdf.sig", file.SignFiles.Single().FileName);
    }

    [Fact]
    public void MalformedXml_ThrowsWithRussianMessage()
    {
        var ex = Assert.Throws<Exception>(() => Parse("<ExplanatoryNote><Document></ExplanatoryNote>"));

        Assert.StartsWith("Ошибка при парсинге XML файла", ex.Message);
    }
}
