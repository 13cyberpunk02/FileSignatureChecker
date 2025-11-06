using CommunityToolkit.Mvvm.ComponentModel;

namespace FileSignatureChecker.Models
{
    public enum CheckStatus
    {
        Success,
        Warning,
        Error,
        Info
    }

    public partial class FileCheckResult : ObservableObject
    {
        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private string _signatureFileName = string.Empty;

        [ObservableProperty]
        private CheckStatus _status;

        [ObservableProperty]
        private string _message = string.Empty;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private bool _fileFound;

        [ObservableProperty]
        private bool _signatureFound;

        [ObservableProperty]
        private string _xmlChecksum = string.Empty;

        [ObservableProperty]
        private string _actualChecksum = string.Empty;
    }
}