using ecocraft.BuildingPlanner.Model;

namespace ecocraft.BuildingPlanner;

// Room requirements des tables (RequireRoomMaterialTier / RequireRoomVolume / RequireRoomContainment) :
// tier effectif = base + Σ bumps des modules installés, plafonné à MaxBlockTier ; volume cumulatif de tous les
// objets de la pièce ; un bump non satisfait laisse la table valide mais désactive ses modules.
public static class RoomRequirementChecker
{
    public static List<TableCheck> Check(BuildContext ctx, IReadOnlyDictionary<string, RoomStats> roomStats, IReadOnlyDictionary<string, string> roomIdByObjectId)
    {
        var results = new List<TableCheck>();
        var volumeUsedByRoom = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var obj in ctx.Objects.Where(o => o.Placed && o.Info is not null))
        {
            var volume = obj.Info!.Requirements?.Volume ?? 0;
            if (volume > 0 && roomIdByObjectId.TryGetValue(obj.Doc.Id, out var roomId))
                volumeUsedByRoom[roomId] = volumeUsedByRoom.GetValueOrDefault(roomId) + volume;
        }

        foreach (var obj in ctx.Objects.Where(o => o.Info is not null && (o.Info.Requirements?.Any ?? false)))
        {
            var info = obj.Info!;
            var req = info.Requirements!;
            roomIdByObjectId.TryGetValue(obj.Doc.Id, out var roomId);
            var stats = roomId is not null && roomStats.TryGetValue(roomId, out var s) ? s : null;
            var inRoom = obj.Placed && stats is not null;
            var contained = stats?.Contained ?? false;

            var bump = ctx.Catalog.ModuleTierBumpByTable.GetValueOrDefault(info.Name);
            float? effective = req.MaterialTier is { } baseTier ? MathF.Min(baseTier + bump, ctx.Catalog.Build.MaxBlockTier) : null;
            var roomTier = stats?.AverageTier ?? 0f;
            var volumeUsed = roomId is not null ? volumeUsedByRoom.GetValueOrDefault(roomId) : 0;
            var roomVolume = stats?.Volume ?? 0;

            // RoomRequirementsComponent : la table exige son tier de base ; le bump des modules non atteint
            // laisse la table fonctionnelle mais désactive les modules.
            var tierOk = inRoom && contained && (req.MaterialTier is null || roomTier >= req.MaterialTier.Value);
            var modulesOk = inRoom && contained && (effective is null || roomTier >= effective.Value);

            var volumeOk = inRoom && contained && volumeUsed <= roomVolume;
            var containmentOk = !req.RequiresContainment || (inRoom && contained);

            results.Add(new TableCheck
            {
                ObjectId = obj.Doc.Id,
                Type = info.Name,
                RoomId = roomId,
                BaseTier = req.MaterialTier,
                ModuleBump = bump,
                EffectiveTier = effective,
                RoomTier = roomTier,
                TierOk = tierOk,
                ModulesOk = modulesOk,
                TierGap = effective is { } e && inRoom ? MathF.Max(0f, EcoMath.RoundF2(e - roomTier)) : effective ?? 0f,
                RequiredVolume = req.Volume,
                RoomVolume = roomVolume,
                RoomVolumeUsed = volumeUsed,
                VolumeOk = volumeOk,
                VolumeGap = inRoom ? Math.Max(0, volumeUsed - roomVolume) : req.Volume,
                RequiresContainment = req.RequiresContainment,
                ContainmentOk = containmentOk,
                InRoom = inRoom,
            });
        }

        return results;
    }
}
