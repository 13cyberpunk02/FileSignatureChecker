using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using FileSignatureChecker.Models;

namespace FileSignatureChecker.Services
{
    public class XmlParserService
    {
        /// <summary>
        /// Элементы схемы пояснительной записки с типом tFile, которые описывают сам файл
        /// (SignFile описывает подпись и разбирается отдельно).
        /// </summary>
        private static readonly HashSet<string> FileElementNames = new(StringComparer.Ordinal)
        {
            "File",    // Document, OtherDocument, Data и т.д.
            "IULFile"  // Информационно-удостоверяющий лист
        };

        /// <summary>
        /// Разбирает пояснительную записку. Файлы собираются из любого контейнера
        /// (Document, OtherDocument, Data, ...), а не только из элементов Document,
        /// иначе такие файлы ошибочно попадают в "не загружены в пояснительную записку".
        /// </summary>
        public static List<Document> ParseXmlFile(string xmlPath)
        {
            try
            {
                var doc = XDocument.Load(xmlPath);
                var documents = new Dictionary<XElement, Document>();

                var fileElements = doc.Descendants()
                    .Where(e => FileElementNames.Contains(e.Name.LocalName) && Child(e, "FileName") != null);

                foreach (var fileElement in fileElements)
                {
                    var container = fileElement.Parent!;
                    if (!documents.TryGetValue(container, out var document))
                    {
                        document = CreateDocument(container);
                        documents[container] = document;
                    }

                    var fileInfo = new XmlFileInfo
                    {
                        FileName = Value(fileElement, "FileName"),
                        FileFormat = Value(fileElement, "FileFormat"),
                        FileChecksum = Value(fileElement, "FileChecksum")
                    };

                    foreach (var signFileElement in fileElement.Elements().Where(e => e.Name.LocalName == "SignFile"))
                    {
                        fileInfo.SignFiles.Add(new SignFileInfo
                        {
                            FileName = Value(signFileElement, "FileName"),
                            FileFormat = Value(signFileElement, "FileFormat"),
                            FileChecksum = Value(signFileElement, "FileChecksum")
                        });
                    }

                    document.Files.Add(fileInfo);
                }

                return documents.Values.ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при парсинге XML файла: {ex.Message}", ex);
            }
        }

        private static Document CreateDocument(XElement container)
        {
            var document = new Document
            {
                DocType = Value(container, "DocType"),
                DocName = Value(container, "DocName"),
                DocNumber = Value(container, "DocNumber"),
                DocDate = Value(container, "DocDate"),
                DocIssueAuthor = Value(container, "DocIssueAuthor")
            };

            // У контейнеров без DocName (например, Data) берём название ближайшего родительского документа
            if (string.IsNullOrWhiteSpace(document.DocName))
            {
                document.DocName = container.Ancestors()
                    .Select(a => Value(a, "DocName"))
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? container.Name.LocalName;
            }

            return document;
        }

        private static XElement? Child(XElement element, string localName)
            => element.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

        private static string Value(XElement element, string localName)
            => Child(element, localName)?.Value.Trim() ?? string.Empty;
    }
}
