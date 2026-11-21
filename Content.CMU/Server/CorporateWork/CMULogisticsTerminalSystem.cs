using System.Linq;
using Content.Server.Popups;
using Content.Shared.CMU14.CorporateWork;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.CorporateWork;

public sealed class CMULogisticsTerminalSystem : EntitySystem
{
    [Dependency] private CMUCorporateWorkSystem _work = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const string Task = "logistics";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMULogisticsTerminalComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CMULogisticsTerminalComponent, CMULogisticsStartBuiMsg>(OnStart);
        SubscribeLocalEvent<CMULogisticsTerminalComponent, CMULogisticsSubmitBuiMsg>(OnSubmit);
    }

    private void OnUiOpened(Entity<CMULogisticsTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnStart(Entity<CMULogisticsTerminalComponent> ent, ref CMULogisticsStartBuiMsg args)
    {
        TryStartCase(ent, args.Tier, args.Actor);
    }

    private void OnSubmit(Entity<CMULogisticsTerminalComponent> ent, ref CMULogisticsSubmitBuiMsg args)
    {
        Submit(ent, args.Loaded, args.Actor);
    }

    /// <summary>
    ///     Opens a new manifest at the given tier, replacing any open one.
    /// </summary>
    public bool TryStartCase(Entity<CMULogisticsTerminalComponent> ent, int tier, EntityUid user)
    {
        if (tier < 0 || tier >= ent.Comp.Tiers.Count)
            return false;

        if (_timing.CurTime < ent.Comp.NextCaseAt)
        {
            _popup.PopupEntity(Loc.GetString("cmu-logistics-cooldown"), ent, user);
            return false;
        }

        ent.Comp.Case = GenerateCase(ent.Comp, tier);
        ent.Comp.LastResult = null;
        UpdateUi(ent);
        return true;
    }

    /// <summary>
    ///     Ships the chosen crates and pays the corporate budget. An overloaded shuttle is refused.
    /// </summary>
    public CMULogisticsResult? Submit(Entity<CMULogisticsTerminalComponent> ent, IEnumerable<int> loadedCrates, EntityUid user)
    {
        if (ent.Comp.Case is not { } manifest)
            return null;

        var loaded = loadedCrates
            .Where(i => i >= 0 && i < manifest.Crates.Count)
            .Distinct()
            .Select(i => manifest.Crates[i])
            .ToList();

        if (loaded.Sum(c => c.Weight) > manifest.WeightLimit || loaded.Sum(c => c.Hazard) > manifest.HazardLimit)
        {
            _popup.PopupEntity(Loc.GetString("cmu-logistics-overloaded"), ent, user);
            return null;
        }

        var value = loaded.Sum(c => c.Value);
        var ratio = manifest.BestValue > 0 ? value / (float) manifest.BestValue : 0f;
        var reward = ratio >= ent.Comp.MinimumRatio ? ent.Comp.Tiers[manifest.Tier].Reward * ratio : 0f;
        var payout = _work.Pay(user, Task, reward);

        var result = new CMULogisticsResult(value, manifest.BestValue, payout);
        ent.Comp.LastResult = result;
        ent.Comp.Case = null;
        ent.Comp.NextCaseAt = _timing.CurTime + ent.Comp.Cooldown;
        UpdateUi(ent);
        return result;
    }

    private CMULogisticsCase GenerateCase(CMULogisticsTerminalComponent comp, int tierIndex)
    {
        var tier = comp.Tiers[tierIndex];
        var names = _prototypes.Index(comp.Cargo).Values.ToList();
        _random.Shuffle(names);
        var count = Math.Min(tier.Crates, names.Count);

        var crates = new List<CMULogisticsCrate>(count);
        for (var i = 0; i < count; i++)
        {
            var weight = _random.Next(1, 11);
            var hazard = _random.Next(0, 4);
            // Heavier and more dangerous cargo is usually worth more, but not always.
            var value = weight * _random.Next(30, 91) + hazard * _random.Next(20, 61);
            crates.Add(new CMULogisticsCrate(names[i], weight, hazard, value));
        }

        var weightLimit = Math.Max(1, (int) MathF.Round(crates.Sum(c => c.Weight) * comp.LimitFraction));
        var hazardLimit = Math.Max(1, (int) MathF.Round(crates.Sum(c => c.Hazard) * comp.LimitFraction));

        return new CMULogisticsCase
        {
            Tier = tierIndex,
            Crates = crates,
            WeightLimit = weightLimit,
            HazardLimit = hazardLimit,
            BestValue = BestValue(crates, weightLimit, hazardLimit),
        };
    }

    /// <summary>
    ///     Best load under both limits. Manifests are small, so every combination is checked.
    /// </summary>
    public static int BestValue(IReadOnlyList<CMULogisticsCrate> crates, int weightLimit, int hazardLimit)
    {
        var best = 0;
        for (var mask = 0; mask < 1 << crates.Count; mask++)
        {
            int weight = 0, hazard = 0, value = 0;
            for (var i = 0; i < crates.Count; i++)
            {
                if ((mask & (1 << i)) == 0)
                    continue;

                weight += crates[i].Weight;
                hazard += crates[i].Hazard;
                value += crates[i].Value;
            }

            if (weight <= weightLimit && hazard <= hazardLimit && value > best)
                best = value;
        }

        return best;
    }

    private void UpdateUi(Entity<CMULogisticsTerminalComponent> ent)
    {
        var tiers = ent.Comp.Tiers
            .Select(t => new CMULogisticsTierInfo(t.Name, t.Crates, t.Reward))
            .ToList();

        var manifest = ent.Comp.Case;
        var state = new CMULogisticsTerminalBuiState(
            tiers,
            manifest?.Tier,
            manifest?.Crates ?? new List<CMULogisticsCrate>(),
            manifest?.WeightLimit ?? 0,
            manifest?.HazardLimit ?? 0,
            ent.Comp.NextCaseAt,
            ent.Comp.LastResult);

        _ui.SetUiState(ent.Owner, CMULogisticsTerminalUi.Key, state);
    }
}
