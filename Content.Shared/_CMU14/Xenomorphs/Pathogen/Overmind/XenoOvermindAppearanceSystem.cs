using Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;


[ByRefEvent]
public readonly record struct CMUXenoOvermindFormChangedEvent(bool Incorporeal);

public sealed partial class CMUXenoOvermindAppearanceSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoOvermindAppearanceComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CMUXenoOvermindAppearanceComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnInit(Entity<CMUXenoOvermindAppearanceComponent> ent, ref ComponentInit args)
    {
        UpdateSprite(ent);
    }

    private void OnMobStateChanged(Entity<CMUXenoOvermindAppearanceComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            // No dead sprite - just play the disappear animation.
            // BlightCoreSystem will delete the entity after the transition window elapses.
            var comp = ent.Comp;
            comp.InTransition = true;
            comp.TransitionEndsAt = _timing.CurTime + comp.TransitionDuration;
            Dirty(ent);
            _appearance.SetData(ent, OvermindVisuals.VisualState, OvermindVisualState.Dying);
        }
    }

    /// <summary>
    /// Begin a form change if allowed. Returns false if blocked (in transition,
    /// or trying to manifest with less than full HP).
    /// Caller is responsible for HP check before calling.
    /// </summary>
    public bool TryBeginFormChange(Entity<CMUXenoOvermindAppearanceComponent> ent)
    {
        var comp = ent.Comp;

        if (comp.InTransition)
            return false;

        comp.InTransition = true;
        comp.TransitionEndsAt = _timing.CurTime + comp.TransitionDuration;
        Dirty(ent);

        var animState = comp.Incorporeal
            ? OvermindVisualState.Appearing   // eye into manifested
            : OvermindVisualState.Disappearing; // manifested into eye

        _appearance.SetData(ent, OvermindVisuals.VisualState, animState);

        return true;
    }

    /// <summary>
    /// Immediately force the Overmind back to incorporeal form (used by fire,
    /// off-weed checks, and return_to_core). Does NOT play the disappear animation.
    /// </summary>
    public void ForceIncorporeal(Entity<CMUXenoOvermindAppearanceComponent> ent)
    {
        var comp = ent.Comp;
        comp.InTransition = false;
        comp.Incorporeal = true;
        Dirty(ent);
        UpdateSprite(ent);

        var ev = new CMUXenoOvermindFormChangedEvent(true);
        RaiseLocalEvent(ent.Owner, ref ev);
    }
    
    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUXenoOvermindAppearanceComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.InTransition)
                continue;

            if (now < comp.TransitionEndsAt)
                continue;

            // Transition finished - flip the state, unless we're dying
            // (in that case BlightCoreSystem handles deletion; don't touch the sprite)
            if (_mobState.IsDead(uid))
            {
                comp.InTransition = false;
                Dirty(uid, comp);
                continue;
            }

            comp.Incorporeal = !comp.Incorporeal;
            comp.InTransition = false;
            Dirty(uid, comp);

            UpdateSprite((uid, comp));

            var ev = new CMUXenoOvermindFormChangedEvent(comp.Incorporeal);
            RaiseLocalEvent(uid, ref ev);
        }
    }

    /// <summary>
    /// Push the correct OvermindVisualState to AppearanceComponent so the
    /// visualizer layer picks it up on both server and client.
    /// </summary>
    private void UpdateSprite(Entity<CMUXenoOvermindAppearanceComponent> ent)
    {
        // Fetch the Overmind component to check Strengthened
        var isStrengthened = TryComp(ent, out CMUXenoOvermindComponent? overmind) && overmind.Strengthened;

        OvermindVisualState state;

        if (ent.Comp.Incorporeal)
        {
            state = OvermindVisualState.Incorporeal;
        }
        else
        {
            state = isStrengthened
                ? OvermindVisualState.ManifestedStrengthened  // overmind_manifested (strengthened tint/layer)
                : OvermindVisualState.Manifested;             // overmind_manifested
        }

        _appearance.SetData(ent, OvermindVisuals.VisualState, state);
    }

    /// <summary>
    /// Called by BlightCoreSystem when the 10-minute strengthen timer fires.
    /// Re-evaluates the sprite so the strengthened visual kicks in immediately.
    /// </summary>
    public void OnStrengthened(EntityUid uid)
    {
        if (TryComp(uid, out CMUXenoOvermindAppearanceComponent? comp))
            UpdateSprite((uid, comp));
    }
}