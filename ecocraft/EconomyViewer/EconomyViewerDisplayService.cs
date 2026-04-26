using System.Globalization;
using ecocraft.Models;
using MudBlazor;

namespace ecocraft.Services;

public class EconomyViewerDisplayService(LocalizationService localizationService)
{
    public string FormatDecimal(decimal? value)
    {
        return value is null
            ? "-"
            : Math.Round(value.Value, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.CurrentUICulture);
    }

    public string FormatSignedDecimal(decimal? value)
    {
        if (value is null)
        {
            return "-";
        }

        var rounded = Math.Round(value.Value, 2, MidpointRounding.AwayFromZero);
        return rounded.ToString("+0.##;-0.##;0", CultureInfo.CurrentUICulture);
    }

    public string FormatSignedPercent(decimal? value)
    {
        if (value is null)
        {
            return "-";
        }

        var rounded = Math.Round(value.Value, 2, MidpointRounding.AwayFromZero);
        return rounded.ToString("+0.##;-0.##;0", CultureInfo.CurrentUICulture) + " %";
    }

    public string FormatSpread(EconomyGlobalRow row, bool showSpreadAsPercent)
    {
        if (!showSpreadAsPercent)
        {
            return FormatDecimal(row.Spread);
        }

        var spreadPercent = GetSpreadPercentValue(row);
        if (spreadPercent is null)
        {
            return "-";
        }

        var rounded = Math.Round(spreadPercent.Value, 2, MidpointRounding.AwayFromZero);
        return rounded.ToString("0.##", CultureInfo.CurrentUICulture) + " %";
    }

    public string FormatSpread(EconomyGlobalPlayerDetailRow row, bool showSpreadAsPercent)
    {
        if (!showSpreadAsPercent)
        {
            return FormatDecimal(row.Spread);
        }

        var spreadPercent = GetSpreadPercentValue(row);
        if (spreadPercent is null)
        {
            return "-";
        }

        var rounded = Math.Round(spreadPercent.Value, 2, MidpointRounding.AwayFromZero);
        return rounded.ToString("0.##", CultureInfo.CurrentUICulture) + " %";
    }

    public object GetSpreadSortValue(EconomyGlobalRow row, bool showSpreadAsPercent)
    {
        if (!showSpreadAsPercent)
        {
            return row.Spread ?? decimal.MaxValue;
        }

        return GetSpreadPercentValue(row) ?? decimal.MaxValue;
    }

    public string FormatBooleanSetting(bool? value)
    {
        if (value is null)
        {
            return "-";
        }

        return localizationService.GetTranslation(value.Value
            ? "EconomyViewer.Drilldown.Values.Enabled"
            : "EconomyViewer.Drilldown.Values.Disabled");
    }

    public string FormatMarginTypeSetting(MarginType? marginType)
    {
        if (marginType is null)
        {
            return "-";
        }

        return localizationService.GetTranslation(marginType.Value switch
        {
            MarginType.MarkUp => "PriceCalculator.MarginType.MarkUp",
            MarginType.GrossMargin => "PriceCalculator.MarginType.GrossMargin",
            _ => marginType.Value.ToString()
        });
    }

    public string FormatCalorieCostPer1000Setting(decimal? calorieCost)
    {
        if (calorieCost is null)
        {
            return "-";
        }

        return $"{FormatDecimal(calorieCost)} / 1000 cal";
    }

    public Color GetStatusColor(string status)
    {
        return status switch
        {
            "Higher" => Color.Warning,
            "Lower" => Color.Success,
            "Same" => Color.Info,
            "OnlyPlayer1" => Color.Primary,
            "OnlyPlayer2" => Color.Secondary,
            _ => Color.Default
        };
    }

    public string GetComparisonStatusTooltip(string status, string? player1Name, string? player2Name)
    {
        var player1DisplayName = string.IsNullOrWhiteSpace(player1Name)
            ? localizationService.GetTranslation("EconomyViewer.Drilldown.Player1")
            : player1Name;
        var player2DisplayName = string.IsNullOrWhiteSpace(player2Name)
            ? localizationService.GetTranslation("EconomyViewer.Drilldown.Player2")
            : player2Name;
        return localizationService.GetTranslation($"EconomyViewer.Drilldown.StatusTooltip.{status}", player1DisplayName, player2DisplayName);
    }

    private decimal? GetSpreadPercentValue(EconomyGlobalRow row)
    {
        if (row.Spread is null || row.PriceMin is null)
        {
            return null;
        }

        if (row.Spread == 0)
        {
            return 0;
        }

        if (row.PriceMin == 0)
        {
            return null;
        }

        return row.Spread.Value / row.PriceMin.Value * 100m;
    }

    private decimal? GetSpreadPercentValue(EconomyGlobalPlayerDetailRow row)
    {
        if (row.Spread is null || row.PriceMin is null)
        {
            return null;
        }

        if (row.Spread == 0)
        {
            return 0;
        }

        if (row.PriceMin == 0)
        {
            return null;
        }

        return row.Spread.Value / row.PriceMin.Value * 100m;
    }
}

