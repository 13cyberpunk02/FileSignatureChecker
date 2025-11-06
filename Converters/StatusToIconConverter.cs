using System;
using System.Globalization;
using System.Windows.Data;
using FileSignatureChecker.Models;
using MaterialDesignThemes.Wpf;

namespace FileSignatureChecker.Converters
{
    public class StatusToIconConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is CheckStatus status)
            {
                return status switch
                {
                    CheckStatus.Success => PackIconKind.CheckCircle,
                    CheckStatus.Warning => PackIconKind.AlertCircle,
                    CheckStatus.Error => PackIconKind.CloseCircle,
                    CheckStatus.Info => PackIconKind.InformationOutline,
                    _ => PackIconKind.HelpCircle
                };
            }
            return PackIconKind.HelpCircle;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}