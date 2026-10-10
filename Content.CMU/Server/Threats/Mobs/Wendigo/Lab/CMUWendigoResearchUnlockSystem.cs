using System;
using System.Linq;
using Content.Server._RMC14.Requisitions;
using Content.Server.CMU14.Round;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Requisitions;

namespace Content.Server.CMU14.Threats.Mobs.Wendigo.Lab;

/// <summary>
/// Research clearance rewards: every WY Lab clearance increase credits the corporate ASRS account,
/// and Weyland-Yutani factions unlock the MH32 crate in their ASRS "Research" category at clearance 3.
/// </summary>
public sealed partial class CMUWendigoResearchUnlockSystem : EntitySystem
{
    public const string MH32Crate = "CMUCrateMH32";
    public const int MH32Cost = 3500;
    public const int UnlockClearance = 3;
    public const int ClearanceIncreaseReward = 500;
    public const string ResearchCategory = "Research";
    private const string CorporateFaction = "corporate";
    private const string WeylandYutaniPlatoon = "WEYU";

    [Dependency] private RequisitionsSystem _reqsys = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoons = default!;
    [Dependency] private SharedResearchDataTerminalSystem _research = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RequisitionsComputerComponent, CMURequisitionsCatalogBuiltEvent>(OnCatalogBuilt);
    }

    /// <summary>Called by the research terminal system after a clearance change (not credit-only updates).</summary>
    public void OnClearanceChanged(string faction, int oldClearance, int newClearance)
    {
        if (newClearance > oldClearance
            && string.Equals(faction, CorporateFaction, StringComparison.OrdinalIgnoreCase))
        {
            _reqsys.ChangeBudget(ClearanceIncreaseReward * (newClearance - oldClearance), CorporateFaction);
        }

        if (oldClearance >= UnlockClearance
            || newClearance < UnlockClearance
            || !IsWeylandYutaniFaction(faction))
        {
            return;
        }

        var query = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (string.Equals(comp.Faction, faction, StringComparison.OrdinalIgnoreCase))
                TryAddMH32(uid, comp);
        }
    }

    /// <summary>Corporate always; govfor/opfor only while their selected platoon is Weyland-Yutani.</summary>
    public bool IsWeylandYutaniFaction(string? faction)
    {
        if (string.IsNullOrEmpty(faction))
            return false;

        if (string.Equals(faction, CorporateFaction, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(faction, "govfor", StringComparison.OrdinalIgnoreCase))
            return _platoons.SelectedGovforPlatoon?.ID == WeylandYutaniPlatoon;

        if (string.Equals(faction, "opfor", StringComparison.OrdinalIgnoreCase))
            return _platoons.SelectedOpforPlatoon?.ID == WeylandYutaniPlatoon;

        return false;
    }

    private void OnCatalogBuilt(Entity<RequisitionsComputerComponent> ent, ref CMURequisitionsCatalogBuiltEvent args)
    {
        var faction = ent.Comp.Faction;
        if (string.IsNullOrEmpty(faction)
            || !IsWeylandYutaniFaction(faction)
            || _research.GetClearance(faction) < UnlockClearance)
        {
            return;
        }

        TryAddMH32(ent.Owner, ent.Comp);
    }

    private void TryAddMH32(EntityUid uid, RequisitionsComputerComponent comp)
    {
        // Every console gets a per-console "Research" category from AddResearchTerminalToCatalog at map init.
        var category = comp.Categories.FirstOrDefault(c => c.Name == ResearchCategory);
        if (category == null)
        {
            Log.Warning($"ASRS console {ToPrettyString(uid)} has no {ResearchCategory} category; MH-32 not added.");
            return;
        }

        if (category.Entries.Any(entry => entry.Crate.Id == MH32Crate))
            return;

        _reqsys.AddEntryToCategory(uid, comp, ResearchCategory, new RequisitionsEntry { Cost = MH32Cost, Crate = MH32Crate });
        Dirty(uid, comp);

        // A zero budget change re-sends the ASRS UI state so already open windows show the new entry.
        if (comp.Faction != null)
            _reqsys.ChangeBudget(0, comp.Faction);
    }
}
