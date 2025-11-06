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
        private string fileName = string.Empty;

        [ObservableProperty]
        private string signatureFileName = string.Empty;

        [ObservableProperty]
        private CheckStatus status;

        [ObservableProperty]
        private string message = string.Empty;

        [ObservableProperty]
        private string filePath = string.Empty;

        [ObservableProperty]
        private bool fileFound;

        [ObservableProperty]
        private bool signatureFound;

        [ObservableProperty]
        private string xmlChecksum = string.Empty;

        [ObservableProperty]
        private string actualChecksum = string.Empty;
    }
}
