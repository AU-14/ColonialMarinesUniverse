using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Damage.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Weapons.Stats;

/// <summary>
///     Lists a gun's fire rate, accuracy, scatter and spread in its weapon stats examine window, under its damage,
///     instead of in the description. The fire mode and safety stay in the description.
/// </summary>
public sealed partial class CMUWeaponStatsSystem : EntitySystem
{
    private const string StatColor = "yellow";

    [Dependency] private RMCSelectiveFireSystem _selectiveFire = default!;

    private readonly List<string> _lines = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<GunComponent, DamageExamineEvent>(OnDamageExamine, after: [typeof(SharedMeleeWeaponSystem)]);
    }

    private void OnDamageExamine(Entity<GunComponent> ent, ref DamageExamineEvent args)
    {
        if (!ent.Comp.ShowExamineText || HasComp<XenoComponent>(args.User))
            return;

        _lines.Clear();
        _lines.Add(Loc.GetString("gun-fire-rate-examine", ("color", StatColor), ("fireRate", $"{ent.Comp.FireRateModified:0.0}")));

        if (TryComp(ent, out RMCWeaponAccuracyComponent? accuracy))
        {
            _lines.Add(Loc.GetString("rmc-examine-text-weapon-accuracy",
                ("colour", StatColor),
                ("accuracy", accuracy.ModifiedAccuracyMultiplier)));
        }

        if (TryComp(ent, out RMCSelectiveFireComponent? selective))
        {
            _lines.Add(Loc.GetString("rmc-examine-text-scatter-max", ("colour", StatColor), ("scatter", ent.Comp.MaxAngleModified.Degrees)));
            _lines.Add(Loc.GetString("rmc-examine-text-scatter-min", ("colour", StatColor), ("scatter", ent.Comp.MinAngleModified.Degrees)));
            if (_selectiveFire.GetShotsToMaxScatter((ent, selective), ent.Comp.SelectedMode) is { } shots)
                _lines.Add(Loc.GetString("rmc-examine-text-shots-to-max-scatter", ("colour", StatColor), ("shots", shots)));
        }

        if (TryComp(ent, out GunSpreadModifierComponent? spread))
        {
            var percentage = Math.Round(spread.Spread * 100);
            var loc = percentage < 100 ? "examine-gun-spread-modifier-reduction" : "examine-gun-spread-modifier-increase";
            percentage = percentage < 100 ? 100 - percentage : percentage - 100;
            _lines.Add(Loc.GetString(loc, ("percentage", percentage)));
        }

        var message = args.Message;
        if (!message.IsEmpty)
        {
            message.PushNewline();
            message.PushNewline();
        }

        message.AddMarkupOrThrow(Loc.GetString("cmu-weapon-stats-header"));
        foreach (var line in _lines)
        {
            message.PushNewline();
            message.AddMarkupOrThrow(line);
        }
    }
}
