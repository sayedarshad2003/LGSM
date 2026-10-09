using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using LGMS.Models;

namespace LGMS.Desktop.Common;

public class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is Visibility v && v == Visibility.Visible) ^ Invert;
    }
}

public class IntEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value != null && parameter != null)
        {
            if (int.TryParse(value.ToString(), out int current) && int.TryParse(parameter.ToString(), out int target))
            {
                return current == target ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class IntEqualsToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value != null && parameter != null)
        {
            if (int.TryParse(value.ToString(), out int current) && int.TryParse(parameter.ToString(), out int target))
            {
                return current == target;
            }
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null && int.TryParse(parameter.ToString(), out int target))
        {
            return target;
        }
        return Binding.DoNothing;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
        if (Invert) isNull = !isNull;
        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CountToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = 0;
        if (value is int i) count = i;
        else if (value is System.Collections.ICollection coll) count = coll.Count;

        bool hasItems = count > 0;
        if (Invert) hasItems = !hasItems;
        return hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ChangeStatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush AddedBrush = new(Color.FromRgb(34, 197, 94));     // #22c55e Emerald
    private static readonly SolidColorBrush ModifiedBrush = new(Color.FromRgb(234, 179, 8));   // #eab308 Amber
    private static readonly SolidColorBrush DeletedBrush = new(Color.FromRgb(239, 68, 68));    // #ef4444 Red
    private static readonly SolidColorBrush RenamedBrush = new(Color.FromRgb(168, 85, 247));   // #a855f7 Purple
    private static readonly SolidColorBrush UntrackedBrush = new(Color.FromRgb(56, 189, 248)); // #38bdf8 Cyan
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(148, 163, 184));  // #94a3b8 Slate

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ChangeStatus status)
        {
            return status switch
            {
                ChangeStatus.Added => AddedBrush,
                ChangeStatus.Modified => ModifiedBrush,
                ChangeStatus.Deleted => DeletedBrush,
                ChangeStatus.Renamed or ChangeStatus.Copied => RenamedBrush,
                ChangeStatus.Untracked => UntrackedBrush,
                _ => DefaultBrush
            };
        }
        return DefaultBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class DiffLineToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush AdditionBg = new(Color.FromArgb(40, 34, 197, 94));
    private static readonly SolidColorBrush AdditionFg = new(Color.FromRgb(22, 163, 74));
    private static readonly SolidColorBrush DeletionBg = new(Color.FromArgb(40, 239, 68, 68));
    private static readonly SolidColorBrush DeletionFg = new(Color.FromRgb(220, 38, 38));
    private static readonly SolidColorBrush HunkBg = new(Color.FromArgb(35, 14, 165, 233));
    private static readonly SolidColorBrush HunkFg = new(Color.FromRgb(2, 132, 199));
    private static readonly SolidColorBrush TransparentBg = new(Colors.Transparent);

    public bool IsForeground { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var line = value as string ?? string.Empty;
        if (line.StartsWith("+++") || line.StartsWith("---"))
        {
            return IsForeground ? HunkFg : HunkBg;
        }
        if (line.StartsWith("+"))
        {
            return IsForeground ? AdditionFg : AdditionBg;
        }
        if (line.StartsWith("-"))
        {
            return IsForeground ? DeletionFg : DeletionBg;
        }
        if (line.StartsWith("@@"))
        {
            return IsForeground ? HunkFg : HunkBg;
        }

        if (IsForeground)
        {
            // Default text foreground will use dynamic resource in control
            return new SolidColorBrush(Color.FromRgb(100, 116, 139));
        }

        return TransparentBg;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
