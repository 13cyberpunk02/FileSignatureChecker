using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FileSignatureChecker.Models;

namespace FileSignatureChecker.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CheckStatus status)
            {
                return status switch
                {
                    CheckStatus.Success => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    CheckStatus.Warning => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    CheckStatus.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                    CheckStatus.Info => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
