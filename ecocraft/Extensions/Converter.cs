using System.Globalization;
using MudBlazor;

namespace ecocraft.Extensions;

public static class CultureInvariantConverter
{
    public static readonly Converter<float> DotOrCommaFloat = new Converter<float>
    {
        SetFunc = value => $"{value}",
        GetFunc = number =>
        {
            if (String.IsNullOrWhiteSpace(number)) return 0;

            number = number.Replace(',', '.');

            if (float.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }

            return 0;
        },
    };

    public static readonly Converter<float?> DotOrCommaFloatNull = new Converter<float?>
    {
        SetFunc = value => value is null ? null : $"{Math.Round((float)value, 2)}",
        GetFunc = number =>
        {
            if (String.IsNullOrWhiteSpace(number)) return null;

            number = number.Replace(',', '.');

            if (float.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }

            return null;
        },
    };
}
