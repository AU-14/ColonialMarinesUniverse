using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Traits.PanicProne;
using Content.Shared.CMU14.Traits.SubstanceAddiction;
using Content.Shared.EntityEffects;
using Content.Shared.Medical;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using JetBrains.Annotations;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Traits.Conditions;

/// <summary>
///     Propranolol, a beta blocker: ends a cardiac episode, keeps sprinting from straining a weak heart for a while and
///     takes the edge off panic.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUBetaBlockerEffect : EntityEffectBase<CMUBetaBlockerEffect>
{
    /// <summary>Seconds of protection from the latest dose.</summary>
    [DataField]
    public float Duration = 900f;

    /// <summary>Panic taken off each time it metabolizes.</summary>
    [DataField]
    public double PanicRelief = 3;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-beta-blocker-guidebook", ("minutes", (int) (Duration / 60)));
}

public sealed partial class CMUBetaBlockerHeartEffectSystem : EntityEffectSystem<CMUHeartConditionComponent, CMUBetaBlockerEffect>
{
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private IGameTiming _timing = default!;

    protected override void Effect(Entity<CMUHeartConditionComponent> entity, ref EntityEffectEvent<CMUBetaBlockerEffect> args)
    {
        var heart = entity.Comp;
        heart.Strain = 0;
        heart.StrainBlockedUntil = _timing.CurTime + TimeSpan.FromSeconds(args.Effect.Duration);

        if (!heart.InEpisode)
            return;

        heart.InEpisode = false;
        Dirty(entity);
        _speed.RefreshMovementSpeedModifiers(entity.Owner);
    }
}

public sealed partial class CMUBetaBlockerPanicEffectSystem : EntityEffectSystem<PanicComponent, CMUBetaBlockerEffect>
{
    [Dependency] private PanicSystem _panic = default!;

    protected override void Effect(Entity<PanicComponent> entity, ref EntityEffectEvent<CMUBetaBlockerEffect> args)
    {
        _panic.AddPanic((entity.Owner, entity.Comp), -args.Effect.PanicRelief);
    }
}

/// <summary>Meclizine, an anti-vertigo drug: clears dizziness and keeps it away for a while.</summary>
[UsedImplicitly]
public sealed partial class CMUAntiVertigoEffect : EntityEffectBase<CMUAntiVertigoEffect>
{
    [DataField]
    public float Duration = 1200f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-anti-vertigo-guidebook", ("minutes", (int) (Duration / 60)));
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUAntiVertigoComponent : Component
{
    [DataField]
    public TimeSpan Until;
}

public sealed partial class CMUAntiVertigoEffectSystem : EntityEffectSystem<StatusEffectContainerComponent, CMUAntiVertigoEffect>
{
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;

    protected override void Effect(Entity<StatusEffectContainerComponent> entity, ref EntityEffectEvent<CMUAntiVertigoEffect> args)
    {
        var until = _timing.CurTime + TimeSpan.FromSeconds(args.Effect.Duration);
        var antiVertigo = EnsureComp<CMUAntiVertigoComponent>(entity);
        if (until > antiVertigo.Until)
            antiVertigo.Until = until;

        _status.TryRemoveStatusEffect(entity, CMUAntiVertigoSystem.Woozy);
    }
}

public sealed partial class CMUAntiVertigoSystem : EntitySystem
{
    public static readonly EntProtoId Woozy = "StatusEffectWoozy";

    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUAntiVertigoComponent, BeforeStatusEffectAddedEvent>(OnBeforeStatusEffect);
    }

    private void OnBeforeStatusEffect(Entity<CMUAntiVertigoComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        if (args.Effect == Woozy && _timing.CurTime < ent.Comp.Until)
            args.Cancelled = true;
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUAntiVertigoComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.Until)
                RemCompDeferred<CMUAntiVertigoComponent>(uid);
        }
    }
}

/// <summary>
///     Disulfiram, the alcohol deterrent: while it's in effect, any alcohol makes them violently sick. It also eases an
///     alcoholic's cravings.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUAlcoholDeterrentEffect : EntityEffectBase<CMUAlcoholDeterrentEffect>
{
    [DataField]
    public float Duration = 1800f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-alcohol-deterrent-guidebook", ("minutes", (int) (Duration / 60)));
}

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUAlcoholDeterrentComponent : Component
{
    [DataField]
    public TimeSpan Until;

    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan ReactionCooldown = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan Dizziness = TimeSpan.FromSeconds(8);

    public TimeSpan NextCheck;
    public TimeSpan NextReaction;
}

public sealed partial class CMUAlcoholDeterrentEffectSystem : EntityEffectSystem<BloodstreamComponent, CMUAlcoholDeterrentEffect>
{
    [Dependency] private IGameTiming _timing = default!;

    protected override void Effect(Entity<BloodstreamComponent> entity, ref EntityEffectEvent<CMUAlcoholDeterrentEffect> args)
    {
        var until = _timing.CurTime + TimeSpan.FromSeconds(args.Effect.Duration);
        var deterrent = EnsureComp<CMUAlcoholDeterrentComponent>(entity);
        if (until > deterrent.Until)
            deterrent.Until = until;
    }
}

public sealed partial class CMUAlcoholDeterrentSystem : EntitySystem
{
    [Dependency] private CMUSubstanceAddictionSystem _addiction = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCReagentSystem _reagents = default!;
    [Dependency] private SharedRMCBloodstreamSystem _rmcBloodstream = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private VomitSystem _vomit = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUAlcoholDeterrentComponent, IngestingEvent>(OnIngesting);
    }

    private void OnIngesting(Entity<CMUAlcoholDeterrentComponent> ent, ref IngestingEvent args)
    {
        if (!_net.IsClient && _timing.CurTime < ent.Comp.Until && HasAlcohol(args.Split))
            React(ent);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUAlcoholDeterrentComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextCheck)
                continue;

            comp.NextCheck = now + comp.CheckInterval;
            if (now >= comp.Until)
            {
                RemCompDeferred<CMUAlcoholDeterrentComponent>(uid);
                continue;
            }

            if (TryComp(uid, out CMUSubstanceAddictionComponent? addiction) && addiction.AnyAlcohol)
                _addiction.EaseCraving((uid, addiction));

            if (HasAlcoholInBody(uid))
                React((uid, comp));
        }
    }

    private void React(Entity<CMUAlcoholDeterrentComponent> ent)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextReaction)
            return;

        ent.Comp.NextReaction = now + ent.Comp.ReactionCooldown;
        _status.TryUpdateStatusEffectDuration(ent, CMUAntiVertigoSystem.Woozy, ent.Comp.Dizziness);
        _popup.PopupEntity(Loc.GetString("cmu-alcohol-deterrent-reaction"), ent, ent, PopupType.MediumCaution);
        _vomit.Vomit(ent);
    }

    private bool HasAlcoholInBody(EntityUid uid)
    {
        if (_rmcBloodstream.TryGetChemicalSolution(uid, out _, out var chemicals) && HasAlcohol(chemicals))
            return true;

        // Alcohol breaks down into ethanol, which sits in the metabolites solution.
        return TryComp(uid, out BloodstreamComponent? bloodstream) &&
               _solution.TryGetSolution(uid, bloodstream.MetabolitesSolutionName, out _, out var metabolites) &&
               HasAlcohol(metabolites);
    }

    private bool HasAlcohol(Solution solution)
    {
        foreach (var reagent in solution.Contents)
        {
            if (_reagents.TryIndex(reagent.Reagent.Prototype, out var proto) && proto.Alcohol)
                return true;
        }

        return false;
    }
}
