using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TylinkInspection.UI.Converters;

public sealed class ResourceLookupConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string resourceKey || string.IsNullOrWhiteSpace(resourceKey))
        {
            return Binding.DoNothing;
        }

        var resource = Application.Current.TryFindResource(resourceKey);
        if (resource is not null)
        {
            return resource;
        }

        return targetType == typeof(Brush) ? Brushes.Transparent : Binding.DoNothing;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
