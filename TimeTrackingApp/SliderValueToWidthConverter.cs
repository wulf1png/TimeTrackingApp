using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace TimeTrackingApp
{
    public class SliderValueToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3 ||
                !(values[0] is double value) ||
                !(values[1] is double max) ||
                !(values[2] is double width))
                return 0;

            double percent = value / max;
            return Math.Max(0, (width - 20) * percent);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException(); // тебе он не нужен, просто обязательный метод
        }
    }
}
