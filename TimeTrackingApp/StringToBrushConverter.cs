using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TimeTrackingApp
{
    public class StringToBrushConverter : IValueConverter
    {
        public Brush NormalBrush { get; set; }
        public Brush ErrorBrush { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && s.Equals("Error", StringComparison.OrdinalIgnoreCase))
                return ErrorBrush;
            return NormalBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}