using System.Globalization;
using MudBlazor;

namespace ecocraft.Extensions;

public static class CultureInvariantConverter
{
    public static readonly IConverter<decimal, string> DotOrCommaDecimal = Conversions.From<decimal, string>(
        value => $"{value:0.##}",
        number =>
        {
            if (string.IsNullOrWhiteSpace(number))
            {
                return 0m;
            }

            number = number.Replace(',', '.');
            return decimal.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0m;
        });

    public static readonly IConverter<decimal?, string?> DotOrCommaDecimalNull = Conversions.From<decimal?, string?>(
        value => value is null ? null : $"{Math.Round(value.Value, 2, MidpointRounding.AwayFromZero):0.##}",
        number =>
        {
            if (string.IsNullOrWhiteSpace(number))
            {
                return null;
            }

            number = number.Replace(',', '.');
            return decimal.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : null;
        });
}
