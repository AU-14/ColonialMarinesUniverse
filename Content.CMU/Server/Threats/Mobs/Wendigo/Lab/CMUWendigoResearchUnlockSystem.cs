using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server._RMC14.Requisitions;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.Round;
using Content.Server.Fax;
using Content.Shared.Database;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Robust.Shared.Map;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.CMU14.Requisitions;

namespace Content.Server.CMU14.Threats.Mobs.Wendigo.Lab;

/// <summary>
/// Research clearance rewards: every WY Lab clearance increase credits the corporate ASRS account,
/// and Weyland-Yutani factions unlock the MH32 crate in their ASRS "Research" category at clearance 3.
/// </summary>
public sealed class CMUWendigoResearchUnlockSystem : EntitySystem
{
    public const string MH32Crate = "CMUCrateMH32";
    public const int MH32Cost = 3500;
    public const int MH32MaxStock = 1;
    public static readonly TimeSpan MH32Restock = TimeSpan.FromMinutes(30);
    public const int UnlockClearance = 3;
    public const int ClearanceIncreaseReward = 500;
    public const string ResearchCategory = "Research";
    private const string CorporateFaction = "corporate";
    private const string WeylandYutaniPlatoon = "WEYU";

    public const string LabBriefingPaper = "CMUPaperWendigoLabBriefing";

    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly FaxSystem _fax = default!;
    [Dependency] private readonly RequisitionsSystem _reqsys = default!;

    private bool _briefingSent;
    [Dependency] private readonly PlatoonSpawnRuleSystem _platoons = default!;
    [Dependency] private readonly SharedResearchDataTerminalSystem _research = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RequisitionsComputerComponent, CMURequisitionsCatalogBuiltEvent>(OnCatalogBuilt);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    /// <summary>Called by the research terminal system after a clearance change (not credit-only updates).</summary>
    public void OnClearanceChanged(string faction, int oldClearance, int newClearance)
    {
        if (newClearance > oldClearance
            && string.Equals(faction, CorporateFaction, StringComparison.OrdinalIgnoreCase))
        {
            _reqsys.ChangeBudget(ClearanceIncreaseReward * (newClearance - oldClearance), CorporateFaction);

            if (oldClearance < UnlockClearance && newClearance >= UnlockClearance && !_briefingSent)
                _briefingSent = SendLabBriefing();
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

    /// <summary>
    /// Faxes the Project MH briefing to the WY Lab. Fax names differ per map, so the lab's fax is found as the
    /// fax machine closest to a corporate research data terminal on the same map (each multi-Z level is its own map).
    /// </summary>
    public bool SendLabBriefing()
    {
        var targets = new HashSet<EntityUid>();
        var terminals = EntityQueryEnumerator<ResearchDataTerminalComponent, TransformComponent>();
        while (terminals.MoveNext(out _, out var terminal, out var terminalXform))
        {
            if (!string.Equals(terminal.Faction, CorporateFaction, StringComparison.OrdinalIgnoreCase)
                || terminalXform.MapUid is not { } map)
            {
                continue;
            }

            EntityUid? closest = null;
            var closestDistance = float.MaxValue;
            var faxes = EntityQueryEnumerator<FaxMachineComponent, TransformComponent>();
            while (faxes.MoveNext(out var fax, out _, out var faxXform))
            {
                if (faxXform.MapUid != map
                    || !terminalXform.Coordinates.TryDistance(EntityManager, faxXform.Coordinates, out var distance)
                    || distance >= closestDistance)
                {
                    continue;
                }

                closest = fax;
                closestDistance = distance;
            }

            if (closest != null)
                targets.Add(closest.Value);
        }

        if (targets.Count == 0)
        {
            Log.Warning($"No fax machine near a corporate research terminal; {LabBriefingPaper} was not sent.");
            return false;
        }

        foreach (var fax in targets)
        {
            var paper = Spawn(LabBriefingPaper, MapCoordinates.Nullspace);
            if (TryComp(paper, out PaperComponent? paperComp))
            {
                var printout = new FaxPrintout(paperComp.Content, Name(paper), null, LabBriefingPaper,
                    paperComp.StampState, paperComp.StampedBy);
                _fax.Receive(fax, printout);
            }

            Del(paper);
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"Project MH briefing faxed to {targets.Count} WY Lab fax machine(s) at research clearance {UnlockClearance}");
        return true;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _briefingSent = false;
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

        // Supply pressure: one crate in stock, restocked slowly.
        _reqsys.AddEntryToCategory(uid, comp, ResearchCategory, new RequisitionsEntry
        {
            Cost = MH32Cost,
            Crate = MH32Crate,
            MaxStock = MH32MaxStock,
            StartingStock = MH32MaxStock,
            StockReplenishDelay = MH32Restock,
        });
        Dirty(uid, comp);

        // A zero budget change re-sends the ASRS UI state so already open windows show the new entry.
        if (comp.Faction != null)
            _reqsys.ChangeBudget(0, comp.Faction);
    }
}
