using System.Collections.Generic;

namespace FileSignatureChecker.Models
{
    public class Document
    {
        public string DocType { get; set; } = string.Empty;
        public string DocName { get; set; } = string.Empty;
        public string DocNumber { get; set; } = string.Empty;
        public string DocDate { get; set; } = string.Empty;
        public string DocIssueAuthor { get; set; } = string.Empty;
        public List<FileInfo> Files { get; set; } = [];
    }

    public class FileInfo
    {
        public string FileName { get; init; } = string.Empty;
        public string FileFormat { get; set; } = string.Empty;
        public string FileChecksum { get; init; } = string.Empty;
        public SignFileInfo? SignFile { get; set; }
    }

    public class SignFileInfo
    {
        public string FileName { get; init; } = string.Empty;
        public string FileFormat { get; set; } = string.Empty;
        public string FileChecksum { get; init; } = string.Empty;
    }
}
