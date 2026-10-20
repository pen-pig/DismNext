using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DismPlusNext
{
    /// <summary>常用 XAML 转换器集合（以静态属性形式供 {x:Static} 引用）。</summary>
    public static class Converters
    {
        public static readonly IValueConverter StringToVisibility = new StringToVisibilityConverter();
        public static readonly IValueConverter BoolToVisibility = new BoolToVisibilityConverter();
        public static readonly IValueConverter InverseBoolToVisibility = new InverseBoolToVisibilityConverter();
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            return string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool v = value is bool b && b;
            return v ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool v = value is bool b && b;
            return v ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
