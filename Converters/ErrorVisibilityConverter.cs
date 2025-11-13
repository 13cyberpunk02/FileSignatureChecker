using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FileSignatureChecker.Converters;

public class ErrorVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return Visibility.Collapsed;

        var isSuccess = values[0] is true;
        var isLoading = values[1] is true;
        
        if (!isSuccess && !isLoading)
        {
            return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}