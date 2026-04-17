using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace ProceduralSFXCompanion.Utilities;

public class PercentageConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double height && parameter is string ratioStr && double.TryParse(ratioStr, out var ratio))
        {
            return height * ratio;
        }
        return value ?? 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public static class EnumUtils
{
    public static string GetEnumDescription(Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                                                          .FirstOrDefault() as DescriptionAttribute;
        return attribute?.Description ?? value.ToString();
    }
}

public static class StringUtils
{
    public static string SplitCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
    
        // Inserts a space before every capital letter that follows a lowercase letter
        return Regex.Replace(input, "([a-z])([A-Z])", "$1 $2");
    }
}