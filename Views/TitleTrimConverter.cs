using System;
using System.Globalization;
using System.Windows.Data;

namespace uYouWin.Views
{
    /// <summary>
    /// Shortens a title with a trailing ellipsis when it exceeds a
    /// reasonable length for a grid card. TextTrimming on the bound
    /// TextBlock already handles visual clipping; this converter provides
    /// an additional, length-based cut so extremely long titles don't
    /// wrap oddly before trimming kicks in.
    /// </summary>
    public class TitleTrimConverter : IValueConverter
    {
        private const int MaxLength = 60;

        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            string title = value as string;

            if (string.IsNullOrEmpty(title))
                return string.Empty;

            if (title.Length <= MaxLength)
                return title;

            return title.Substring(0, MaxLength).TrimEnd() + "...";
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
