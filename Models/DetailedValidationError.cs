using System.Xml.Schema;

namespace FileSignatureChecker.Models;

public class DetailedValidationError
{
    public ValidationEventArgs? Args { get; set; }
    public int LineNumber { get; set; }
    public int LinePosition { get; set; }
}