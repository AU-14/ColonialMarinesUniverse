using Content.Shared.CMU14.Round.Roles;
using Content.Shared.CMU14.util;
using Content.Shared.Item;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Systems;

// CMU14: platoons swap starting equipment for their own versions (see PlatoonPrototype.StartingGearOverrides).
public sealed partial class StationSpawningSystem
{
    /// <summary>The platoon swaps in effect while the current character is being equipped, if any.</summary>
    private Dictionary<EntProtoId, EntProtoId>? _cmuGearOverrides;

    /// <summary>
    ///     Applies the side's platoon equipment swaps to everything equipped until the returned scope is disposed:
    ///     the job's starting gear and the player's loadout alike.
    /// </summary>
    private CMUGearOverrideScope CMUPushGearOverrides(RoundJobSide side)
    {
        var platoon = side switch
        {
            RoundJobSide.Govfor => _platoonSpawnRuleSystem.SelectedGovforPlatoon,
            RoundJobSide.Opfor => _platoonSpawnRuleSystem.SelectedOpforPlatoon,
            _ => null,
        };

        var previous = _cmuGearOverrides;
        _cmuGearOverrides = platoon is { StartingGearOverrides.Count: > 0 } ? platoon.StartingGearOverrides : null;
        return new CMUGearOverrideScope(this, previous);
    }

    /// <summary>The starting gear a job spawns with under its side's platoon: the platoon's own set, if it has one.</summary>
    private ProtoId<StartingGearPrototype> CMUPlatoonStartingGear(RoundJobSide side, string jobId, ProtoId<StartingGearPrototype> gear)
    {
        var platoon = side switch
        {
            RoundJobSide.Govfor => _platoonSpawnRuleSystem.SelectedGovforPlatoon,
            RoundJobSide.Opfor => _platoonSpawnRuleSystem.SelectedOpforPlatoon,
            _ => null,
        };

        return platoon != null && platoon.StartingGearJobOverrides.TryGetValue(jobId, out var platoonGear)
            ? platoonGear
            : gear;
    }

    protected override EntProtoId CMUOverrideGear(EntProtoId proto)
    {
        if (_cmuGearOverrides == null ||
            !_cmuGearOverrides.TryGetValue(proto, out var replacement) ||
            !CMUFitsEverything(proto, replacement))
        {
            return proto;
        }

        return replacement;
    }

    /// <summary>
    ///     A container is only swapped for one that holds at least as much, so whatever the starting gear or loadout
    ///     puts in it still fits and nothing is lost.
    /// </summary>
    private bool CMUFitsEverything(EntProtoId original, EntProtoId replacement)
    {
        if (!_prototypeManager.TryIndex(original, out var originalProto) ||
            !originalProto.TryGetComponent(out StorageComponent? originalStorage, EntityManager.ComponentFactory))
        {
            return true;
        }

        if (!_prototypeManager.TryIndex(replacement, out var replacementProto) ||
            !replacementProto.TryGetComponent(out StorageComponent? replacementStorage, EntityManager.ComponentFactory))
        {
            Log.Warning($"Platoon gear override {original} -> {replacement} skipped: the replacement has no storage.");
            return false;
        }

        if (CMUGridArea(replacementStorage) < CMUGridArea(originalStorage) ||
            CMUMaxSizeWeight(replacementStorage) < CMUMaxSizeWeight(originalStorage) ||
            replacementStorage.Whitelist != null && originalStorage.Whitelist == null)
        {
            Log.Warning($"Platoon gear override {original} -> {replacement} skipped: the replacement holds less.");
            return false;
        }

        return true;
    }

    private static int CMUGridArea(StorageComponent storage)
    {
        var area = 0;
        foreach (var box in storage.Grid)
        {
            area += (box.Width + 1) * (box.Height + 1);
        }

        return area;
    }

    private int CMUMaxSizeWeight(StorageComponent storage)
    {
        return storage.MaxItemSize is { } size && _prototypeManager.TryIndex(size, out ItemSizePrototype? sizeProto)
            ? sizeProto.Weight
            : int.MaxValue;
    }

    private readonly struct CMUGearOverrideScope(StationSpawningSystem system, Dictionary<EntProtoId, EntProtoId>? previous)
        : IDisposable
    {
        public void Dispose()
        {
            system._cmuGearOverrides = previous;
        }
    }
}
