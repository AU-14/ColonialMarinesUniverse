using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Content.Shared._RMC14.Ordnance;
using Content.Shared._RMC14.Ordnance.Signaler;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Ordnance;

public sealed class RMCOrdnanceAssemblySystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private static readonly EntProtoId AssemblyPrototype = "RMCOrdnanceAssembly";
    private static readonly ProtoId<TagPrototype> LockedTag = "RMCOrdnanceAssemblyLocked";
    private static readonly ProtoId<TagPrototype> TimerTag = "RMCTimerAssembly";
    private static readonly ProtoId<TagPrototype> ProximityTag = "RMCProximityAssembly";
    private static readonly ProtoId<TagPrototype> SignalerTag = "RMCSignalerAssembly";
    private static readonly ProtoId<TagPrototype> DoubleIgniterTag = "RMCDoubleIgniterAssembly";
    private static readonly ProtoId<ToolQualityPrototype> PryingQuality = "Prying";
    private static readonly ProtoId<ToolQualityPrototype> ScrewingQuality = "Screwing";
    private static readonly SoundSpecifier InsertSound = new SoundPathSpecifier("/Audio/_RMC14/Weapons/Guns/Reload/grenade_insert.ogg");
    private static readonly SoundSpecifier ArmSound = new SoundPathSpecifier("/Audio/_RMC14/Explosion/armbomb.ogg");
    private static readonly float[] TimerOptions = { 3f, 5f, 10f, 30f };
    private static readonly float[] ProximityOptions = { 0.5f, 1f, 1.5f, 2f, 2.5f };

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCOrdnancePartComponent, InteractUsingEvent>(OnPartInteractUsing);
        SubscribeLocalEvent<RMCOrdnancePartComponent, GetVerbsEvent<AlternativeVerb>>(OnPartVerbs);
        SubscribeLocalEvent<RMCOrdnanceAssemblyComponent, InteractUsingEvent>(OnAssemblyInteractUsing);
        SubscribeLocalEvent<RMCOrdnanceAssemblyComponent, GetVerbsEvent<AlternativeVerb>>(OnAssemblyVerbs);

        Subs.BuiEvents<RMCOrdnanceAssemblyComponent>(OrdnanceSignalerUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnAssemblyUiOpened);
            subs.Event<SelectOrdnanceSignalerFrequencyMessage>(OnAssemblyFrequencyChange);
        });
    }

    private void OnPartInteractUsing(Entity<RMCOrdnancePartComponent> target, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<RMCOrdnancePartComponent>(args.Used, out var usedPart))
            return;

        if (!IsValidCombination(usedPart.PartType, target.Comp.PartType))
        {
            _popup.PopupEntity(Loc.GetString("rmc-ordnance-assembly-incompatible"), target, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        args.Handled = true;

        var assemblyEnt = Spawn(AssemblyPrototype, _transform.GetMapCoordinates(args.User));
        var assembly = EnsureComp<RMCOrdnanceAssemblyComponent>(assemblyEnt);
        assembly.LeftPartType = usedPart.PartType;
        assembly.RightPartType = target.Comp.PartType;
        assembly.IsLocked = false;
        assembly.TimerDelay = 5f;
        assembly.SignalFrequency = GetInitialFrequency(args.Used, target.Owner);
        assembly.ProximityRange = 1.5f;

        QueueDel(args.Used);
        QueueDel(target.Owner);

        UpdateVisuals((assemblyEnt, assembly));
        UpdateTags((assemblyEnt, assembly));
        _audio.PlayPredicted(InsertSound, assemblyEnt, args.User);
        _popup.PopupEntity(Loc.GetString("rmc-ordnance-assembly-combined"), assemblyEnt, args.User);
    }

    private void OnAssemblyInteractUsing(Entity<RMCOrdnanceAssemblyComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_toolSystem.HasQuality(args.Used, PryingQuality))
        {
            if (ent.Comp.IsLocked)
            {
                _popup.PopupEntity(Loc.GetString("rmc-ordnance-assembly-pry-locked"), ent, args.User, PopupType.SmallCaution);
                args.Handled = true;
                return;
            }

            Disassemble(ent, args.User);
            args.Handled = true;
            return;
        }

        if (_toolSystem.HasQuality(args.Used, ScrewingQuality))
        {
            ToggleLocked(ent, args.User);
            args.Handled = true;
        }
    }

    private void OnAssemblyVerbs(Entity<RMCOrdnanceAssemblyComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (HasType(ent.Comp, RMCOrdnancePartType.RMCOrdnanceTimer))
            AddTimerVerbs(ent, ref args);

        if (HasType(ent.Comp, RMCOrdnancePartType.RMCOrdnanceSignaler))
            AddFrequencyUiVerb(ent, ref args);

        if (HasType(ent.Comp, RMCOrdnancePartType.RMCOrdnanceProximitySensor))
            AddProximityVerbs(ent, ref args);
    }

    private void OnPartVerbs(Entity<RMCOrdnancePartComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (ent.Comp.PartType != RMCOrdnancePartType.RMCOrdnanceSignaler)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Category = TriggerSystem.TimerOptions,
            Text = Loc.GetString("rmc-ordnance-frequency-configure"),
            Act = () => _ui.TryOpenUi(ent.Owner, OrdnanceSignalerUiKey.Key, user),
        });
    }

    private void AddTimerVerbs(Entity<RMCOrdnanceAssemblyComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;

        foreach (var option in TimerOptions)
        {
            var current = IsClose(option, ent.Comp.TimerDelay);
            args.Verbs.Add(new AlternativeVerb
            {
                Category = TriggerSystem.TimerOptions,
                Text = current
                    ? Loc.GetString("rmc-ordnance-timer-current", ("time", option))
                    : Loc.GetString("rmc-ordnance-timer-set", ("time", option)),
                Disabled = current,
                Priority = -(int) (option * 10),
                Act = () =>
                {
                    ent.Comp.TimerDelay = option;
                    Dirty(ent);
                    _popup.PopupEntity(Loc.GetString("rmc-ordnance-timer-popup", ("time", option)), ent, user);
                }
            });
        }
    }

    private void AddFrequencyUiVerb(Entity<RMCOrdnanceAssemblyComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Category = TriggerSystem.TimerOptions,
            Text = Loc.GetString("rmc-ordnance-frequency-configure"),
            Act = () => _ui.TryOpenUi(ent.Owner, OrdnanceSignalerUiKey.Key, user),
        });
    }

    private void AddProximityVerbs(Entity<RMCOrdnanceAssemblyComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;

        foreach (var option in ProximityOptions)
        {
            var current = IsClose(option, ent.Comp.ProximityRange);
            args.Verbs.Add(new AlternativeVerb
            {
                Category = TriggerSystem.TimerOptions,
                Text = current
                    ? Loc.GetString("rmc-ordnance-proximity-current", ("range", option))
                    : Loc.GetString("rmc-ordnance-proximity-set", ("range", option)),
                Disabled = current,
                Priority = -(int) (option * 10),
                Act = () =>
                {
                    ent.Comp.ProximityRange = option;
                    Dirty(ent);
                    _popup.PopupEntity(
                        Loc.GetString("rmc-ordnance-proximity-popup", ("range", option)),
                        ent,
                        user);
                }
            });
        }
    }

    private void ToggleLocked(Entity<RMCOrdnanceAssemblyComponent> ent, EntityUid user)
    {
        ent.Comp.IsLocked = !ent.Comp.IsLocked;
        Dirty(ent);

        UpdateVisuals(ent);
        UpdateTags(ent);
        _audio.PlayPredicted(ArmSound, ent, user);

        if (ent.Comp.IsLocked)
            _popup.PopupEntity(Loc.GetString("rmc-ordnance-assembly-locked"), ent, user);
        else
            _popup.PopupEntity(Loc.GetString("rmc-ordnance-assembly-unlocked"), ent, user);
    }

    private void Disassemble(Entity<RMCOrdnanceAssemblyComponent> ent, EntityUid user)
    {
        var coords = Transform(ent).Coordinates;

        if (ent.Comp.LeftPartType is { } left)
            Spawn(GetPartProto(left), coords);

        if (ent.Comp.RightPartType is { } right)
            Spawn(GetPartProto(right), coords);

        _audio.PlayPredicted(InsertSound, ent, user);
        _popup.PopupEntity(Loc.GetString("rmc-ordnance-assembly-disassembled"), ent, user);
        QueueDel(ent);
    }

    private void UpdateVisuals(Entity<RMCOrdnanceAssemblyComponent> ent)
    {
        if (ent.Comp.LeftPartType is { } left)
            _appearance.SetData(ent, RMCAssemblyVisualKey.LeftType, left);

        if (ent.Comp.RightPartType is { } right)
            _appearance.SetData(ent, RMCAssemblyVisualKey.RightType, right);

        _appearance.SetData(ent, RMCAssemblyVisualKey.Locked, ent.Comp.IsLocked);
    }

    private void UpdateTags(Entity<RMCOrdnanceAssemblyComponent> ent)
    {
        _tags.RemoveTag(ent, LockedTag);
        _tags.RemoveTag(ent, TimerTag);
        _tags.RemoveTag(ent, ProximityTag);
        _tags.RemoveTag(ent, SignalerTag);
        _tags.RemoveTag(ent, DoubleIgniterTag);

        if (!ent.Comp.IsLocked)
            return;

        _tags.AddTag(ent, LockedTag);

        switch (GetAssemblyKind(ent.Comp))
        {
            case RMCOrdnanceAssemblyKind.Timer:
                _tags.AddTag(ent, TimerTag);
                break;
            case RMCOrdnanceAssemblyKind.Proximity:
                _tags.AddTag(ent, ProximityTag);
                break;
            case RMCOrdnanceAssemblyKind.Signaler:
                _tags.AddTag(ent, SignalerTag);
                break;
            case RMCOrdnanceAssemblyKind.DoubleIgniter:
                _tags.AddTag(ent, DoubleIgniterTag);
                break;
        }
    }

    private static bool IsValidCombination(RMCOrdnancePartType first, RMCOrdnancePartType second)
    {
        if (first == RMCOrdnancePartType.RMCOrdnanceIgniter && second == RMCOrdnancePartType.RMCOrdnanceIgniter)
            return true;

        return first == RMCOrdnancePartType.RMCOrdnanceIgniter || second == RMCOrdnancePartType.RMCOrdnanceIgniter;
    }

    private static bool HasType(RMCOrdnanceAssemblyComponent comp, RMCOrdnancePartType type)
    {
        return comp.LeftPartType == type || comp.RightPartType == type;
    }

    private static bool IsClose(float a, float b)
    {
        return Math.Abs(a - b) < 0.001f;
    }

    private void OnAssemblyUiOpened(Entity<RMCOrdnanceAssemblyComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!HasType(ent.Comp, RMCOrdnancePartType.RMCOrdnanceSignaler))
            return;

        UpdateAssemblyUi(ent);
    }

    private void OnAssemblyFrequencyChange(Entity<RMCOrdnanceAssemblyComponent> ent, ref SelectOrdnanceSignalerFrequencyMessage args)
    {
        if (!HasType(ent.Comp, RMCOrdnancePartType.RMCOrdnanceSignaler))
            return;

        if (args.Frequency != 0)
        {
            ent.Comp.SignalFrequency = args.Frequency;
            Dirty(ent);
        }

        UpdateAssemblyUi(ent);

        if (args.Frequency != 0)
        {
            _popup.PopupEntity(
                Loc.GetString("rmc-ordnance-frequency-popup", ("frequency", args.Frequency.FrequencyToString())),
                ent,
                args.Actor);
        }
    }

    private void UpdateAssemblyUi(Entity<RMCOrdnanceAssemblyComponent> ent)
    {
        _ui.SetUiState(ent.Owner, OrdnanceSignalerUiKey.Key, new OrdnanceSignalerBoundUIState((int) ent.Comp.SignalFrequency));
    }

    private static RMCOrdnanceAssemblyKind GetAssemblyKind(RMCOrdnanceAssemblyComponent comp)
    {
        if (HasType(comp, RMCOrdnancePartType.RMCOrdnanceTimer))
            return RMCOrdnanceAssemblyKind.Timer;

        if (HasType(comp, RMCOrdnancePartType.RMCOrdnanceSignaler))
            return RMCOrdnanceAssemblyKind.Signaler;

        if (HasType(comp, RMCOrdnancePartType.RMCOrdnanceProximitySensor))
            return RMCOrdnanceAssemblyKind.Proximity;

        return RMCOrdnanceAssemblyKind.DoubleIgniter;
    }

    private uint GetInitialFrequency(EntityUid first, EntityUid second)
    {
        if (TryComp<DeviceNetworkComponent>(first, out var firstNet) && firstNet.ReceiveFrequency is { } firstFreq)
            return firstFreq;

        if (TryComp<DeviceNetworkComponent>(second, out var secondNet) && secondNet.ReceiveFrequency is { } secondFreq)
            return secondFreq;

        return 1280;
    }

    private static EntProtoId GetPartProto(RMCOrdnancePartType type)
    {
        return type switch
        {
            RMCOrdnancePartType.RMCOrdnanceIgniter => "RMCOrdnanceIgniter",
            RMCOrdnancePartType.RMCOrdnanceTimer => "RMCOrdnanceTimer",
            RMCOrdnancePartType.RMCOrdnanceSignaler => "RMCOrdnanceSignaler",
            RMCOrdnancePartType.RMCOrdnanceProximitySensor => "RMCOrdnanceProximitySensor",
            _ => "RMCOrdnanceIgniter",
        };
    }

    private enum RMCOrdnanceAssemblyKind : byte
    {
        DoubleIgniter,
        Timer,
        Signaler,
        Proximity,
    }
}

