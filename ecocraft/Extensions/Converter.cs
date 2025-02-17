using System.Globalization;
using MudBlazor;

namespace ecocraft.Extensions;

public static class CultureInvariantConverter
{
    public static readonly Converter<decimal> DotOrCommaDecimal = new Converter<decimal>
    {
        SetFunc = value => $"{value:0.##}",
        GetFunc = number =>
        {
            if (String.IsNullOrWhiteSpace(number)) return 0;

            number = number.Replace(',', '.');

            return decimal.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
        },
    };

    public static readonly Converter<decimal?> DotOrCommaDecimalNull = new Converter<decimal?>
    {
        SetFunc = value => value is null ? null : $"{Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero):0.##}",
        GetFunc = number =>
        {
            if (String.IsNullOrWhiteSpace(number)) return null;

            number = number.Replace(',', '.');

            if (decimal.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
            {
                return result;
            }

            return null;
        },
    };
}
