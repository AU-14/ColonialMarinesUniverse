using Content.Shared.AU14.Factory;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.AU14.Factory;

public sealed partial class AUProcessorMachineSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AUProcessorMachineComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<AUProcessorMachineComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<AUProcessorMachineComponent, BreakageEventArgs>(OnBreakage);
        SubscribeLocalEvent<AUProcessorMachineComponent, DamageChangedEvent>(OnDamageChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AUProcessorMachineComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsProcessing)
                continue;

            comp.TimeRemaining -= frameTime;
            if (comp.TimeRemaining > 0f)
                continue;

            FinishProcessing(uid, comp);
        }
    }

    private void OnInserted(EntityUid uid, AUProcessorMachineComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (comp.IsProcessing || comp.IsBroken)
            return;

        if (args.Container.ID != comp.InputSlotId)
            return;

        if (!_tag.HasTag(args.Entity, comp.InputTag))
            return;

        comp.PendingItem = args.Entity;
        comp.TimeRemaining = comp.ProcessTime;
        comp.IsProcessing = true;

        _audio.PlayPvs(comp.ProcessSound, uid);
    }

    private void OnRemoved(EntityUid uid, AUProcessorMachineComponent comp, EntRemovedFromContainerMessage args)
    {
        if (!comp.IsProcessing || comp.PendingItem != args.Entity)
            return;

        comp.PendingItem = null;
        comp.IsProcessing = false;
        comp.TimeRemaining = 0f;
    }

    private void FinishProcessing(EntityUid uid, AUProcessorMachineComponent comp)
    {
        if (comp.PendingItem.HasValue && !Deleted(comp.PendingItem.Value))
            QueueDel(comp.PendingItem.Value);

        Spawn(comp.Output, Transform(uid).Coordinates);

        comp.PendingItem = null;
        comp.IsProcessing = false;
        comp.TimeRemaining = 0f;

        if (comp.BreakChance > 0f && _random.Prob(comp.BreakChance))
            TriggerFireBreakdown(uid, comp);
    }

    private void TriggerFireBreakdown(EntityUid uid, AUProcessorMachineComponent comp)
    {
        if (!TryComp(uid, out DamageableComponent? damageable))
            return;

        if (!_proto.TryIndex(BurnGroup, out var burnGroup))
            return;

        var spec = new DamageSpecifier(burnGroup, FixedPoint2.New(1000));
        _damageable.TryChangeDamage(uid, spec, ignoreResistances: true, damageable: damageable);

        if (comp.FireSpawn is { } fire)
            Spawn(fire, Transform(uid).Coordinates);
    }

    private void OnBreakage(EntityUid uid, AUProcessorMachineComponent comp, BreakageEventArgs args)
    {
        if (comp.IsBroken)
            return;

        comp.IsBroken = true;
        comp.IsProcessing = false;
        comp.TimeRemaining = 0f;
        comp.PendingItem = null;

        _popup.PopupEntity($"The {Name(uid)} belches flame and seizes up! It needs repairs.", uid, PopupType.LargeCaution);
    }

    private void OnDamageChanged(EntityUid uid, AUProcessorMachineComponent comp, DamageChangedEvent args)
    {
        if (!comp.IsBroken)
            return;

        if (args.Damageable.TotalDamage > FixedPoint2.Zero)
            return;

        comp.IsBroken = false;
        _popup.PopupEntity($"The {Name(uid)} hums back to life.", uid, PopupType.Medium);
    }
}
