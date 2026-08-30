using Content.Shared.Jittering;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Chemistry.Effects;

[ByRefEvent]
public record struct CureChemicalAddictionEvent;

[RegisterComponent]
public sealed partial class ChemicalAddictionComponent : Component
{
    [DataField]
    public Dictionary<string, ChemicalAddictionEntry> Addictions = new();
}

[DataDefinition]
public sealed partial class ChemicalAddictionEntry
{
    [DataField]
    public TimeSpan LastDose;

    [DataField]
    public TimeSpan NextMessage;

    [DataField]
    public bool Craving;
}

public sealed partial class ChemicalAddictionSystem : EntitySystem
{
    private static readonly TimeSpan CravingDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MessageInterval = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan ShakeDelay = TimeSpan.FromMinutes(7);

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChemicalAddictionComponent, CureChemicalAddictionEvent>(OnCure);
    }

    public void AddOrSatisfy(EntityUid target, string reagent)
    {
        var addictions = EnsureComp<ChemicalAddictionComponent>(target);
        if (!addictions.Addictions.TryGetValue(reagent, out var entry))
        {
            entry = new ChemicalAddictionEntry();
            addictions.Addictions[reagent] = entry;
        }

        var wasCraving = entry.Craving;
        entry.LastDose = _timing.CurTime;
        entry.NextMessage = entry.LastDose + CravingDelay;
        entry.Craving = false;

        if (wasCraving)
            _popup.PopupEntity(Loc.GetString("cmu-chemical-addiction-satisfied", ("chemical", reagent)), target, target);
    }

    public bool IsAddicted(EntityUid target, string reagent)
        => TryComp<ChemicalAddictionComponent>(target, out var comp) && comp.Addictions.ContainsKey(reagent);

    private void OnCure(Entity<ChemicalAddictionComponent> ent, ref CureChemicalAddictionEvent args)
    {
        RemCompDeferred<ChemicalAddictionComponent>(ent);
        _popup.PopupEntity(Loc.GetString("cmu-chemical-addiction-cured"), ent, ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ChemicalAddictionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            foreach (var (chemical, entry) in comp.Addictions)
            {
                var elapsed = now - entry.LastDose;
                if (elapsed < CravingDelay)
                    continue;

                if (!entry.Craving)
                {
                    entry.Craving = true;
                    _popup.PopupEntity(Loc.GetString("cmu-chemical-addiction-onset", ("chemical", chemical)), uid, uid,
                        PopupType.MediumCaution);
                }

                if (now >= entry.NextMessage)
                {
                    entry.NextMessage = now + MessageInterval;
                    _popup.PopupEntity(Loc.GetString("cmu-chemical-addiction-craving", ("chemical", chemical)), uid, uid,
                        PopupType.SmallCaution);
                    if (elapsed >= ShakeDelay)
                        _jitter.DoJitter(uid, TimeSpan.FromSeconds(4), true, 8, 4);
                }
            }

        }
    }
}
