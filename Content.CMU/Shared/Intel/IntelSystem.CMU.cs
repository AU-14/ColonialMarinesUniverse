using System.Diagnostics.CodeAnalysis;
using Content.Shared._CMU14.Intel;
using Content.Shared._CMU14.Round.Objectives;
using Content.Shared._CMU14.Round.Objectives.Components;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared._RMC14.Marines;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Intel;

public sealed partial class IntelSystem
{
    [Dependency] private IPrototypeManager _cmuFactionPrototypes = default!;

    private const string DefaultTechTreePrototype = "RMCIntelTechTree";
    private readonly Dictionary<string, string> _cmuTeamTechTreeOverrides = new();

    public Entity<IntelTechTreeComponent> EnsureTechTree(string team)
    {
        if (!CMUFactionTech.TryNormalizeFaction(team, out var faction))
            return EnsureTechTree();

        // Keep the upstream singleton first in entity-query order. Upstream intel objectives
        // continue to use it, while CMU faction trees remain an additive compatibility layer.
        _ = EnsureTechTree();

        if (TryGetTechTree(faction, out var existing))
            return existing.Value;

        var prototype = ResolveFactionTechTreePrototype(faction);
        var uid = Spawn(prototype);
        var component = EnsureComp<IntelTechTreeComponent>(uid);
        component.Team = faction;
        InitializeCurrentCosts(component.Tree);
        Dirty(uid, component);
        return (uid, component);
    }

    public bool TryGetTechTree(
        string team,
        [NotNullWhen(true)] out Entity<IntelTechTreeComponent>? tree)
    {
        if (!CMUFactionTech.TryNormalizeFaction(team, out var faction))
        {
            tree = null;
            return false;
        }

        var query = EntityQueryEnumerator<IntelTechTreeComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!string.Equals(component.Team, faction, StringComparison.OrdinalIgnoreCase))
                continue;

            tree = (uid, component);
            return true;
        }

        tree = null;
        return false;
    }

    public int GetIntelPoints(string team)
    {
        return TryGetTechTree(team, out var tree)
            ? (int) Math.Floor(tree.Value.Comp.Tree.Points.Double())
            : 0;
    }

    public bool TrySpendIntelPoints(string team, double amount)
    {
        if (!double.IsFinite(amount) || amount < 0 ||
            !TryGetTechTree(team, out var tree))
        {
            return false;
        }

        var cost = FixedPoint2.New(amount);
        if (tree.Value.Comp.Tree.Points < cost)
            return false;

        tree.Value.Comp.Tree.Points -= cost;
        Dirty(tree.Value);
        UpdateTree(tree.Value);
        return true;
    }

    public bool TrySpendFactionWinPoints(string team, int amount)
    {
        if (amount < 0 ||
            !CMUFactionTech.TryNormalizeFaction(team, out var faction) ||
            !TryGetFactionWinPoints(faction, out var available) ||
            available < amount)
        {
            return false;
        }

        if (amount == 0)
            return true;

        var ev = new SpendWinPointsEvent
        {
            Team = faction,
            Amount = amount,
        };
        RaiseLocalEvent(ev);
        return ev.Succeeded;
    }

    public void SetTeamTechTreeOverride(string team, string? prototype)
    {
        if (!CMUFactionTech.TryNormalizeFaction(team, out var faction))
            return;

        if (string.IsNullOrWhiteSpace(prototype))
        {
            _cmuTeamTechTreeOverrides.Remove(faction);
            return;
        }

        if (IsValidTechTreePrototype(prototype))
            _cmuTeamTechTreeOverrides[faction] = prototype;
    }

    public void ClearTeamTechTreeOverrides()
    {
        _cmuTeamTechTreeOverrides.Clear();
    }

    partial void ResolveTechTreeForUser(EntityUid user, ref Entity<IntelTechTreeComponent> tree)
    {
        if (!TryComp(user, out MarineComponent? marine) ||
            !CMUFactionTech.TryNormalizeFaction(marine.Faction, out var faction))
        {
            return;
        }

        tree = EnsureTechTree(faction);
    }

    partial void ResolveTechTreeForTeam(string? team, ref Entity<IntelTechTreeComponent> tree)
    {
        if (CMUFactionTech.TryNormalizeFaction(team, out var faction))
            tree = EnsureTechTree(faction);
    }

    partial void SetIntelObjectivesViewTeam(
        Entity<ViewIntelObjectivesComponent> view,
        Entity<IntelTechTreeComponent> tree)
    {
        view.Comp.Team = CMUFactionTech.TryNormalizeFaction(tree.Comp.Team, out var faction)
            ? faction
            : Team.None;
    }

    partial void IsDefaultTechTree(IntelTechTreeComponent tree, ref bool isDefault)
    {
        if (CMUFactionTech.TryNormalizeFaction(tree.Team, out _))
            isDefault = false;
    }

    partial void TryUpdateFactionTree(Entity<IntelTechTreeComponent> tree, ref bool handled)
    {
        // Once CMU's Team field is present, own all dispatch so the upstream singleton cannot
        // overwrite GovFor, OpFor, or CLF consoles.
        handled = true;
        var factionTree = CMUFactionTech.TryNormalizeFaction(tree.Comp.Team, out var treeFaction);

        FixedPoint2? displayedWinPoints = null;
        if (factionTree && TryGetFactionWinPoints(treeFaction, out var winPoints))
            displayedWinPoints = FixedPoint2.New(winPoints);

        var consoleQuery = EntityQueryEnumerator<TechControlConsoleComponent>();
        while (consoleQuery.MoveNext(out var uid, out var console))
        {
            var factionConsole = CMUFactionTech.TryNormalizeFaction(console.Team, out var consoleFaction);
            if (factionTree != factionConsole ||
                factionTree && !string.Equals(treeFaction, consoleFaction, StringComparison.Ordinal))
            {
                continue;
            }

            console.Tree = displayedWinPoints is { } points
                ? CopyTreeForDisplay(tree.Comp.Tree, points)
                : tree.Comp.Tree;
            Dirty(uid, console);
        }

        var viewQuery = EntityQueryEnumerator<ViewIntelObjectivesComponent>();
        while (viewQuery.MoveNext(out var uid, out var view))
        {
            var factionView = CMUFactionTech.TryNormalizeFaction(view.Team, out var viewFaction);
            if (factionTree != factionView ||
                factionTree && !string.Equals(treeFaction, viewFaction, StringComparison.Ordinal))
            {
                continue;
            }

            view.Tree = tree.Comp.Tree;
            Dirty(uid, view);
        }
    }

    private string ResolveFactionTechTreePrototype(string faction)
    {
        if (_cmuTeamTechTreeOverrides.TryGetValue(faction, out var runtimeOverride) &&
            IsValidTechTreePrototype(runtimeOverride))
        {
            return runtimeOverride;
        }

        var candidate = faction switch
        {
            Team.GovFor => "RMCIntelTechTree_govfor",
            Team.OpFor => "RMCIntelTechTree_opfor",
            Team.CLF => "RMCIntelTechTree_clf",
            _ => DefaultTechTreePrototype,
        };

        return _cmuFactionPrototypes.HasIndex<EntityPrototype>(candidate)
            ? candidate
            : DefaultTechTreePrototype;
    }

    private bool IsValidTechTreePrototype(string prototype)
    {
        return _cmuFactionPrototypes.TryIndex<EntityPrototype>(prototype, out var entity) &&
               entity.TryGetComponent<IntelTechTreeComponent>(out var tree) &&
               tree.Tree.Options.Count > 0;
    }

    private static void InitializeCurrentCosts(IntelTechTree tree)
    {
        foreach (var tier in tree.Options)
        {
            for (var i = 0; i < tier.Count; i++)
            {
                var option = tier[i];
                if (option.CurrentCost == 0)
                    tier[i] = option with { CurrentCost = option.Cost };
            }
        }
    }

    private bool TryGetFactionWinPoints(string faction, out int points)
    {
        points = 0;
        if (_net.IsClient)
            return false;

        var query = EntityQueryEnumerator<CMUObjectiveMasterComponent>();
        while (query.MoveNext(out _, out var master))
        {
            if (!master.IsActive || !master.Factions.TryGetValue(faction, out var data))
                continue;

            points = data.CurrentWinPoints;
            return true;
        }

        return false;
    }

    private static IntelTechTree CopyTreeForDisplay(IntelTechTree source, FixedPoint2 points)
    {
        return new IntelTechTree
        {
            Points = points,
            TotalEarned = source.TotalEarned,
            Documents = source.Documents,
            UploadData = source.UploadData,
            RetrieveItems = source.RetrieveItems,
            Miscellaneous = source.Miscellaneous,
            AnalyzeChemicals = source.AnalyzeChemicals,
            RescueSurvivors = source.RescueSurvivors,
            RecoverCorpses = source.RecoverCorpses,
            ColonyCommunications = source.ColonyCommunications,
            ColonyPower = source.ColonyPower,
            Tier = source.Tier,
            Options = source.Options,
            Clues = source.Clues,
        };
    }
}
