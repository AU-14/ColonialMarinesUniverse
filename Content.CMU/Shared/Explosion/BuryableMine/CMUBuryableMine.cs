using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Tag;
using Content.Shared.Toggleable;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Explosion.BuryableMine;

/// <summary>
///     Ported from cmss13 PR 3615. A landmine that has to be dug in with a shovel after placing it, arms a few seconds
///     later, and with <see cref="HeavyTrigger"/> only goes off under something heavy: vehicles and big xenos.
///     It has no IFF, so it doesn't care whose side the heavy thing is on.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUBuryableMineComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Buried;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? ArmAt;

    [DataField]
    public TimeSpan BuryDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan ArmDelay = TimeSpan.FromSeconds(3);

    /// <summary>Only vehicles and big xenos set it off; people only sometimes, and smaller xenos never.</summary>
    [DataField]
    public bool HeavyTrigger = true;

    [DataField]
    public float HumanTriggerChance = 0.25f;

    [DataField]
    public ProtoId<TagPrototype> ShovelTag = "Shovel";

    [DataField]
    public SoundSpecifier BurySound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    [DataField]
    public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Empty/empty.ogg");
}

[Serializable, NetSerializable]
public sealed partial class CMUBuryMineDoAfterEvent : SimpleDoAfterEvent;

public sealed class CMUBuryableMineSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUBuryableMineComponent, ClaymoreDeployDoafterEvent>(OnDeployed, after: [typeof(SharedRMCLandmineSystem)]);
        SubscribeLocalEvent<CMUBuryableMineComponent, ClaymoreDisarmDoafterEvent>(OnDisarmed, after: [typeof(SharedRMCLandmineSystem)]);
        SubscribeLocalEvent<CMUBuryableMineComponent, InteractUsingEvent>(OnInteractUsing, before: [typeof(SharedRMCLandmineSystem)]);
        SubscribeLocalEvent<CMUBuryableMineComponent, CMUBuryMineDoAfterEvent>(OnBury);
        SubscribeLocalEvent<CMUBuryableMineComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<CMUBuryableMineComponent, ExaminedEvent>(OnExamined);
    }

    private void OnDeployed(Entity<CMUBuryableMineComponent> ent, ref ClaymoreDeployDoafterEvent args)
    {
        if (args.Cancelled || !TryComp(ent, out RMCLandmineComponent? mine) || !mine.Armed)
            return;

        // Placed with the pin pulled, but it does nothing until it's dug in.
        mine.Armed = false;
        mine.Factions.Clear();
        Dirty(ent.Owner, mine);
        ent.Comp.Buried = false;
        ent.Comp.ArmAt = null;
        Dirty(ent);
        _appearance.SetData(ent, ToggleableVisuals.Enabled, false);
        _popup.PopupClient(Loc.GetString("cmu-buryable-mine-placed", ("mine", ent)), ent, args.User);
    }

    private void OnDisarmed(Entity<CMUBuryableMineComponent> ent, ref ClaymoreDisarmDoafterEvent args)
    {
        if (args.Cancelled)
            return;

        ent.Comp.Buried = false;
        ent.Comp.ArmAt = null;
        Dirty(ent);
    }

    private void OnInteractUsing(Entity<CMUBuryableMineComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Buried || !Transform(ent).Anchored || !_tag.HasTag(args.Used, ent.Comp.ShovelTag))
            return;

        args.Handled = true;
        if (TryComp(args.Used, out ItemToggleComponent? toggle) && !_itemToggle.IsActivated((args.Used, toggle)))
        {
            _popup.PopupClient(Loc.GetString("cmu-buryable-mine-unfold", ("tool", args.Used)), ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.BuryDelay, new CMUBuryMineDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupClient(Loc.GetString("cmu-buryable-mine-burying", ("mine", ent)), ent, args.User);
    }

    private void OnBury(Entity<CMUBuryableMineComponent> ent, ref CMUBuryMineDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Buried)
            return;

        args.Handled = true;
        ent.Comp.Buried = true;
        ent.Comp.ArmAt = _timing.CurTime + ent.Comp.ArmDelay;
        Dirty(ent);
        _audio.PlayPredicted(ent.Comp.BurySound, ent, args.User);
        _appearance.SetData(ent, ToggleableVisuals.Enabled, true);
        _popup.PopupClient(Loc.GetString("cmu-buryable-mine-buried", ("mine", ent)), ent, args.User);
    }

    private void OnStepTriggerAttempt(Entity<CMUBuryableMineComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (!ent.Comp.HeavyTrigger || args.Cancelled)
            return;

        var tripper = args.Tripper;
        if (HasComp<GridVehicleMoverComponent>(tripper) || HasComp<VehicleComponent>(tripper))
            return;

        if (HasComp<XenoComponent>(tripper))
        {
            if (TryComp(tripper, out RMCSizeComponent? size) && size.Size >= RMCSizes.Big)
                return;
        }
        else if (_net.IsServer && _random.Prob(ent.Comp.HumanTriggerChance))
        {
            return;
        }

        // Not heavy enough to set it off.
        args.Cancelled = true;
        if (_net.IsServer)
        {
            _audio.PlayPvs(ent.Comp.ClickSound, ent);
            _popup.PopupEntity(Loc.GetString("cmu-buryable-mine-click", ("mine", ent)), ent, tripper, PopupType.MediumCaution);
        }
    }

    private void OnExamined(Entity<CMUBuryableMineComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Buried)
            args.PushMarkup(Loc.GetString("cmu-buryable-mine-examine-armed"));
        else
            args.PushMarkup(Loc.GetString("cmu-buryable-mine-examine-instructions", ("seconds", (int) ent.Comp.ArmDelay.TotalSeconds)));
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUBuryableMineComponent, RMCLandmineComponent>();
        while (query.MoveNext(out var uid, out var buryable, out var mine))
        {
            if (buryable.ArmAt is not { } armAt || now < armAt)
                continue;

            buryable.ArmAt = null;
            Dirty(uid, buryable);
            if (!buryable.Buried)
                continue;

            mine.Armed = true;
            mine.Factions.Clear();
            Dirty(uid, mine);
        }
    }
}
