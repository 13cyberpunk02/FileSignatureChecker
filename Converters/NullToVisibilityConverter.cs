using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FileSignatureChecker.Converters;

/// <summary>
/// Показывает элемент если строка не null и не пустая
/// </summary>

public class NullToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}