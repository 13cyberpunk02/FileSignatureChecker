using System;
using System.Collections.Generic;
using System.Xml.Linq;
using FileSignatureChecker.Models;

namespace FileSignatureChecker.Services
{
    public class XmlParserService
    {
        public static List<Document> ParseXmlFile(string xmlPath)
        {
            var documents = new List<Document>();

            try
            {
                var doc = XDocument.Load(xmlPath);
                var documentElements = doc.Descendants("Document");

                foreach (var docElement in documentElements)
                {
                    var document = new Document
                    {
                        DocType = docElement.Element("DocType")?.Value ?? string.Empty,
                        DocName = docElement.Element("DocName")?.Value ?? string.Empty,
                        DocNumber = docElement.Element("DocNumber")?.Value ?? string.Empty,
                        DocDate = docElement.Element("DocDate")?.Value ?? string.Empty,
                        DocIssueAuthor = docElement.Element("DocIssueAuthor")?.Value ?? string.Empty
                    };

                    var fileElements = docElement.Elements("File");
                    foreach (var fileElement in fileElements)
                    {
                        var fileInfo = new FileInfo
                        {
                            FileName = fileElement.Element("FileName")?.Value ?? string.Empty,
                            FileFormat = fileElement.Element("FileFormat")?.Value ?? string.Empty,
                            FileChecksum = fileElement.Element("FileChecksum")?.Value ?? string.Empty
                        };

                        var signFileElement = fileElement.Element("SignFile");
                        if (signFileElement != null)
                        {
                            fileInfo.SignFile = new SignFileInfo
                            {
                                FileName = signFileElement.Element("FileName")?.Value ?? string.Empty,
                                FileFormat = signFileElement.Element("FileFormat")?.Value ?? string.Empty,
                                FileChecksum = signFileElement.Element("FileChecksum")?.Value ?? string.Empty
                            };
                        }

                        document.Files.Add(fileInfo);
                    }

                    documents.Add(document);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при парсинге XML файла: {ex.Message}", ex);
            }

            return documents;
        }
    }
}