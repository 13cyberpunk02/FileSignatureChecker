using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FileSignatureChecker.Converters;

public class ValidationResultColorConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return Brushes.Black;

        var isSuccess = values[0] is bool success && success;
        var isLoading = values[1] is bool loading && loading;

        if (isLoading)
        {
            return new SolidColorBrush(Color.FromRgb(33, 150, 243)); 
        }
        
        return isSuccess ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) : 
            new SolidColorBrush(Color.FromRgb(244, 67, 54));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}