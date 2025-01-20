using Hi3Helper;
using Hi3Helper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using Windows.Globalization.NumberFormatting;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher.Pages
{
    public partial class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string langInfo)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The target must be a boolean");

            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string langInfo)
        {
            throw new NotSupportedException();
        }
    }

    public partial class BooleanVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => (bool)value ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public partial class InverseBooleanVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => !(bool)value ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public partial class DoubleRound2Converter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => Math.Round((double)value, 2);
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public partial class DoubleRound3Converter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => Math.Round((double)value, 3);
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public partial class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => value is string asString && !string.IsNullOrEmpty(asString) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public partial class FileSizeToStringLiteralConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string input) => ConverterTool.SummarizeSizeSimple((long)value);
        public object ConvertBack(object value, Type targetType, object parameter, string input) => new NotImplementedException();
    }

    public partial class DownloadSpeedLimitToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not double asDouble)
            {
                return Locale.Lang._Misc.IsBytesNotANumber;
            }

            long valBFromM = (long)(asDouble * (1 << 20));
            return valBFromM > 0 ?
                string.Format(Locale.Lang._Misc.IsBytesMoreThanBytes, valBFromM, string.Format(Locale.Lang._Misc.SpeedPerSec, ConverterTool.SummarizeSizeSimple(valBFromM))) :
                string.Format(Locale.Lang._Misc.IsBytesUnlimited);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class DownloadChunkSizeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not double asDouble)
            {
                return Locale.Lang._Misc.IsBytesNotANumber;
            }

            int valBFromM = (int)(asDouble * (1 << 20));
            return valBFromM > 0 ?
                string.Format(Locale.Lang._Misc.IsBytesMoreThanBytes, valBFromM, ConverterTool.SummarizeSizeSimple(valBFromM)) :
                string.Format(Locale.Lang._Misc.IsBytesUnlimited);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public partial class DoubleFormatter : INumberFormatter2, INumberParser
    {
        private const string Format = "{0:F5}";
        public string FormatDouble(double value) => string.Format(Format, value);
        public double? ParseDouble(string text) => double.TryParse(text, out var dbl) ? dbl : null;

        public string FormatInt(long value) => throw new NotSupportedException();
        public string FormatUInt(ulong value) => throw new NotSupportedException();
        public long? ParseInt(string text) => throw new NotSupportedException();
        public ulong? ParseUInt(string text) => throw new NotSupportedException();
    }

    public partial class CapsuleCornerRadiusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not double d)
            {
                return 0;
            }

            double padding = 0;
            if (parameter is double asDouble)
            {
                padding = asDouble;
            }
            return (d - padding) / 2.0;

        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
