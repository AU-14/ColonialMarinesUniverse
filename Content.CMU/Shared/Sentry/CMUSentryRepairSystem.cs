using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Repairable;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Tools;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Sentry;

/// <summary>
///     Repairing a broken sentry, one tool after another, while it's deployed: cut away the burnt wiring, weld the
///     frame, rewire it with ten lengths of cable, screw the panels back down, retune its targeting with an access
///     tuner and weld the casing shut. Examining it lists the steps.
/// </summary>
public sealed class CMUSentryRepairSystem : EntitySystem
{
    private static readonly ProtoId<ToolQualityPrototype> Cutting = "Cutting";
    private static readonly ProtoId<ToolQualityPrototype> Welding = "Welding";
    private static readonly ProtoId<ToolQualityPrototype> Screwing = "Screwing";
    private static readonly ProtoId<ToolQualityPrototype> Anchoring = "Anchoring";
    private static readonly ProtoId<StackPrototype> Cable = "RMCCable";
    private static readonly EntProtoId<SkillDefinitionComponent> DelaySkill = "RMCSkillConstruction";

    private const int CableNeeded = 10;
    private const float WeldFuel = 5f;

    private enum Tool : byte
    {
        Cutters,
        Welder,
        Cable,
        Screwdriver,
        Tuner,
    }

    private readonly record struct Step(Tool Tool, float Seconds, string Name);

    private static readonly Step[] Steps =
    [
        new(Tool.Cutters, 4f, "cut"),
        new(Tool.Welder, 6f, "weld-frame"),
        new(Tool.Cable, 7f, "rewire"),
        new(Tool.Screwdriver, 4f, "screw"),
        new(Tool.Tuner, 8f, "tune"),
        new(Tool.Welder, 5f, "weld-casing"),
    ];

    private static readonly SoundSpecifier CableSound = new SoundPathSpecifier("/Audio/Items/deconstruct.ogg");
    private static readonly SoundSpecifier TunerSound = new SoundPathSpecifier("/Audio/Effects/double_beep.ogg");

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedToolSystem _tools = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSentryBrokenComponent, InteractUsingEvent>(OnInteractUsing,
            before: [typeof(SentrySystem), typeof(RMCRepairableSystem)]);
        SubscribeLocalEvent<CMUSentryBrokenComponent, CMUSentryRepairDoAfterEvent>(OnRepairDoAfter);
        SubscribeLocalEvent<CMUSentryBrokenComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUSentryBrokenComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
    }

    /// <summary>A broken sentry is already wrecked; it takes no more damage until it's fixed.</summary>
    private void OnBeforeDamageChanged(Entity<CMUSentryBrokenComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Damage.AnyPositive())
            args.Cancelled = true;
    }

    private void OnInteractUsing(Entity<CMUSentryBrokenComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var used = args.Used;
        var user = args.User;

        // Packing it up and setting it down still works while it's broken.
        if (_tools.HasQuality(used, Anchoring))
            return;

        if (ToolOf(used) is not { } tool)
            return;

        args.Handled = true;

        if (!TryComp(ent, out SentryComponent? sentry) || sentry.Mode == SentryMode.Item)
        {
            _popup.PopupClient(Loc.GetString("cmu-sentry-repair-deploy-first", ("sentry", ent.Owner)), ent, user, PopupType.SmallCaution);
            return;
        }

        if (ent.Comp.Step >= Steps.Length)
            return;

        var step = Steps[ent.Comp.Step];
        if (step.Tool != tool)
        {
            _popup.PopupClient(Loc.GetString("cmu-sentry-repair-wrong-tool",
                ("sentry", ent.Owner), ("step", StepText(ent.Comp.Step))), ent, user, PopupType.SmallCaution);
            return;
        }

        if (tool == Tool.Cable &&
            (!TryComp(used, out StackComponent? stack) || stack.Count < CableNeeded))
        {
            _popup.PopupClient(Loc.GetString("cmu-sentry-repair-need-cable", ("amount", CableNeeded)), ent, user, PopupType.SmallCaution);
            return;
        }

        var delay = TimeSpan.FromSeconds(step.Seconds) * _skills.GetSkillDelayMultiplier(user, DelaySkill);
        var ev = new CMUSentryRepairDoAfterEvent(ent.Comp.Step);
        bool started;
        switch (tool)
        {
            case Tool.Cutters:
                started = _tools.UseTool(used, user, ent, delay, [Cutting], ev, out _);
                break;
            case Tool.Welder:
                started = _tools.UseTool(used, user, ent, delay, [Welding], ev, out _, WeldFuel);
                break;
            case Tool.Screwdriver:
                started = _tools.UseTool(used, user, ent, delay, [Screwing], ev, out _);
                break;
            default:
                started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, delay, ev, ent, ent, used)
                {
                    BreakOnMove = true,
                    NeedHand = true,
                    BreakOnHandChange = true,
                });
                if (started)
                    _audio.PlayPredicted(tool == Tool.Cable ? CableSound : TunerSound, ent, user);
                break;
        }

        if (started)
        {
            _popup.PopupClient(Loc.GetString($"cmu-sentry-repair-start-{step.Name}", ("sentry", ent.Owner)), ent, user);
        }
    }

    private void OnRepairDoAfter(Entity<CMUSentryBrokenComponent> ent, ref CMUSentryRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Step != ent.Comp.Step || ent.Comp.Step >= Steps.Length)
            return;

        if (!TryComp(ent, out SentryComponent? sentry) || sentry.Mode == SentryMode.Item)
            return;

        var step = Steps[ent.Comp.Step];
        if (step.Tool == Tool.Cable)
        {
            if (args.Used is not { } cable || !_stack.TryUse(cable, CableNeeded))
                return;
        }

        args.Handled = true;
        ent.Comp.Step++;
        Dirty(ent);

        if (ent.Comp.Step < Steps.Length)
        {
            _popup.PopupClient(Loc.GetString($"cmu-sentry-repair-done-{step.Name}", ("sentry", ent.Owner)), ent, args.User);
            return;
        }

        _damageable.ClearAllDamage(ent.Owner);
        RemCompDeferred<CMUSentryBrokenComponent>(ent);
        _popup.PopupPredicted(Loc.GetString("cmu-sentry-repair-finished", ("sentry", ent.Owner)),
            Loc.GetString("cmu-sentry-repair-finished-others", ("sentry", ent.Owner), ("user", args.User)),
            ent, args.User, PopupType.Medium);
    }

    private void OnExamined(Entity<CMUSentryBrokenComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(CMUSentryBrokenComponent), 1))
        {
            args.PushMarkup(Loc.GetString("cmu-sentry-broken-examine", ("sentry", ent.Owner)));
            for (var i = 0; i < Steps.Length; i++)
            {
                var state = i < ent.Comp.Step ? "done" : i == ent.Comp.Step ? "next" : "todo";
                args.PushMarkup(Loc.GetString($"cmu-sentry-repair-step-{state}", ("number", i + 1), ("step", StepText(i))));
            }
        }
    }

    private string StepText(int step)
    {
        return Loc.GetString($"cmu-sentry-repair-step-{Steps[step].Name}", ("amount", CableNeeded));
    }

    private Tool? ToolOf(EntityUid used)
    {
        if (TryComp(used, out StackComponent? stack) && stack.StackTypeId == Cable)
            return Tool.Cable;

        if (HasComp<MultitoolComponent>(used))
            return Tool.Tuner;

        if (_tools.HasQuality(used, Welding))
            return Tool.Welder;

        if (_tools.HasQuality(used, Cutting))
            return Tool.Cutters;

        if (_tools.HasQuality(used, Screwing))
            return Tool.Screwdriver;

        return null;
    }
}
