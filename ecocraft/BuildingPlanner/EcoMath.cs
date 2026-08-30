namespace ecocraft.BuildingPlanner;

// Répliques exactes des fonctions de Eco.Shared.Utils.MathUtil / Mathf utilisées par le housing.
public static class EcoMath
{
    // MathUtil.DiminishingReturn : 1 / (1/m)^i.
    public static float DiminishingReturn(float dimReturn, float val) => 1f / (float)Math.Pow(1 / dimReturn, val);

    // MathUtil.DiminishingReturnExtra : Min(val, range × (1 − 1 / (1/d)^(val/range))).
    public static float DiminishingReturnExtra(float dimReturn, float val, float range)
        => MathF.Min(val, range * ((-1f / (float)Math.Pow(1 / dimReturn, val / range)) + 1f));

    // Math.Round(x, Mathf.AcceptedFractionalDigitsCount = 2) — arrondi bancaire (ToEven), comme dans le jeu.
    public static float Round2(float value) => (float)Math.Round(value, 2);

    // MathF.Round(x, 2) du RoomChecker.
    public static float RoundF2(float value) => MathF.Round(value, 2);

    // RoomTier.ApplyToValue.
    public static float ApplyRoomTier(float softCap, float hardCap, float diminishingReturnPercent, float inVal)
    {
        if (inVal == 0) return 0;
        if (inVal < softCap) return inVal;
        var valAbove = inVal - softCap;
        return softCap + DiminishingReturnExtra(diminishingReturnPercent, valAbove, hardCap - softCap);
    }
}
