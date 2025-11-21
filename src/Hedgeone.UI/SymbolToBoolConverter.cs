using System.Globalization;
using System.Windows.Data;

namespace Hedgeone.UI;

/// <summary>
/// RadioButton용 Symbol 값 변환기
/// </summary>
public class SymbolToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string symbol && parameter is string paramSymbol)
        {
            return symbol == paramSymbol;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string paramSymbol)
        {
            return paramSymbol;
        }
        return Binding.DoNothing;
    }
}
