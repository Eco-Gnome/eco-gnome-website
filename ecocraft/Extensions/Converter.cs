using System.Globalization;
using MudBlazor;

namespace ecocraft.Extensions;

public static class CultureInvariantConverter
{
    public static Converter<float> DotOrComma = new Converter<float>
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
}
