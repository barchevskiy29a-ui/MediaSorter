using System.Globalization;
using System.Windows.Data;
using MediaSorter.Models;

namespace MediaSorter.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileStatus status)
        {
            return status switch
            {
                FileStatus.Moved => "#107C10",
                FileStatus.Moving => "#0078D4",
                FileStatus.Scanned => "#605E5C",
                FileStatus.Skipped => "#605E5C",
                FileStatus.SkippedDuplicate => "#FFB900",
                FileStatus.SkippedAlreadySorted => "#605E5C",
                FileStatus.Error => "#D13438",
                FileStatus.ErrorAccessDenied => "#D13438",
                FileStatus.ErrorFileInUse => "#FFB900",
                FileStatus.ErrorPathTooLong => "#D13438",
                _ => "#323130"
            };
        }
        return "#323130";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

