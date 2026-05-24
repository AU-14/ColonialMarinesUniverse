using Content.Client._RMC14.Sprite;
using Content.Shared._RMC14.Sprite;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Charge;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared._RMC14.Xenonids.Movement;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Rest;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._RMC14.Xenonids;

public sealed partial class XenoVisualizerSystem : VisualizerSystem<XenoComponent>
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private RMCSpriteSystem _rmcSprite = default!;

    private EntityQuery<XenoAnimateMovementComponent> _animateQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoComponent, KnockedDownEvent>(OnXenoKnockedDown);
        SubscribeLocalEvent<XenoComponent, StatusEffectEndedEvent>(OnXenoStatusEffectEnded);
        SubscribeLocalEvent<XenoComponent, GetDrawDepthEvent>(OnXenoGetDrawDepth);

        _animateQuery = GetEntityQuery<XenoAnimateMovementComponent>();
    }

    private void OnXenoKnockedDown(Entity<XenoComponent> xeno, ref KnockedDownEvent args)
    {
        UpdateSprite(xeno.Owner);
    }

    private void OnXenoStatusEffectEnded(Entity<XenoComponent> xeno, ref StatusEffectEndedEvent args)
    {
        UpdateSprite(xeno.Owner);
    }

    private void OnXenoGetDrawDepth(Entity<XenoComponent> ent, ref GetDrawDepthEvent args)
    {
        if (!_mobState.IsDead(ent))
            return;

        if (args.DrawDepth > DrawDepth.DeadMobs)
            args.DrawDepth = DrawDepth.DeadMobs;
    }

    protected override void OnAppearanceChange(EntityUid uid, XenoComponent component, ref AppearanceChangeEvent args)
    {
        var sprite = args.Sprite;
        UpdateSprite((uid, sprite, null, args.Component, null, null));
        _rmcSprite.UpdateDrawDepth(uid);
    }

    public void UpdateSprite(Entity<SpriteComponent?, MobStateComponent?, AppearanceComponent?, InputMoverComponent?, ThrownItemComponent?, XenoLeapingComponent?, KnockedDownComponent?> entity)
    {
        var (_, sprite, mobState, appearance, input, thrown, leaping, knocked) = entity;
        if (!Resolve(entity, ref sprite, ref appearance, false))
            return;

        var state = MobState.Alive;
        if (Resolve(entity, ref mobState, false))
            state = mobState.CurrentState;

        Resolve(entity, ref input, ref thrown, ref leaping, ref knocked, false);
        if (knocked != null && state != MobState.Dead)
            state = MobState.Critical;

        if (sprite is not { BaseRSI: { } rsi } ||
            !SpriteSystem.LayerMapTryGet((entity.Owner, sprite), XenoVisualLayers.Base, out var layer, false))
        {
            return;
        }

        // TODO RMC14 split this up into multiple systems with ordered event subscription
        // TODO RMC14 please god
        string? oviState = null;
        switch (state)
        {
            case MobState.Critical:
                if (rsi.TryGetState("crit", out _))
                    SpriteSystem.LayerSetRsiState((entity.Owner, sprite), layer, "crit");
                break;
            case MobState.Dead:
                if (HasComp<ParasiteSpentComponent>(entity))
                {
                    SpriteSystem.LayerSetRsiState((entity.Owner, sprite), layer, "impregnated");
                    break;
                }
                if (rsi.TryGetState("dead", out _))
                    SpriteSystem.LayerSetRsiState((entity.Owner, sprite), layer, "dead");
                break;
            default:
                if (HasComp<XenoAttachedOvipositorComponent>(entity) &&
                    TryComp(entity, out XenoOvipositorCapableComponent? capable))
                {
                    oviState = capable.AttachedState;
                    break;
                }

                if (AppearanceSystem.TryGetData(entity, XenoVisualLayers.Base, out XenoRestState resting, appearance) &&
                    resting == XenoRestState.Resting &&
                    rsi.TryGetState("sleeping", out _))
                {
                    SpriteSystem.LayerSetRsiState((entity.Owner, sprite), layer, "sleeping");
                    break;
                }

                if (rsi.TryGetState("thrown", out _) &&
                    IsThrown((entity, leaping, thrown, null)))
                {
                    SpriteSystem.LayerSetRsiState((entity.Owner, sprite), layer, "thrown");
                    break;
                }

                if (AppearanceSystem.TryGetData(entity, XenoVisualLayers.Fortify, out bool fortify, appearance) &&
                    fortify &&
                    rsi.TryGetState("fortify", out _))
                {
                    SpriteSystem.LayerSetRsiState((entity.Owner, sprite), layer, "fortify");
                    break;
                }

                if (AppearanceSystem.TryGetData(entity, XenoVisualLayers.Crest, out bool crest, appearance) &&
                    crest &&
                    rsi.TryGetState("crest", out _))
                {
                    SpriteSystem.LayerSetRsiState((entity.Owner, sprite), layer, "crest");
                    break;
                }

                if (AppearanceSystem.TryGetData(entity, XenoVisualLayers.Burrow, out bool burrowed, appearance) &&
                    burrowed &&
                    rsi.TryGetState("burrowed", out _))
                {
                    SpriteSystem.LayerSetRsiState((entity.Owner, sprite), layer, "burrowed");
                    break;
                }

                if (input?.HeldMoveButtons > MoveButtons.None &&
                    input.HeldMoveButtons != MoveButtons.Walk &&
                    rsi.TryGetState("running", out _))
                {
                    SpriteSystem.LayerSetRsiState((entity.Owner, sprite), layer, "running");
                    break;
                }

                if (rsi.TryGetState("alive", out _))
                    SpriteSystem.LayerSetRsiState((entity.Owner, sprite), layer, "alive");

                break;
        }

        if (!SpriteSystem.LayerMapTryGet((entity.Owner, sprite), XenoVisualLayers.Ovipositor, out var oviLayer, false))
            return;

        if (oviState == null)
        {
            SpriteSystem.LayerSetVisible((entity.Owner, sprite), oviLayer, false);
            SpriteSystem.LayerSetVisible((entity.Owner, sprite), layer, true);
            return;
        }

        SpriteSystem.LayerSetRsiState((entity.Owner, sprite), oviLayer, oviState);
        SpriteSystem.LayerSetVisible((entity.Owner, sprite), oviLayer, true);
        SpriteSystem.LayerSetVisible((entity.Owner, sprite), layer, false);
    }

    private bool IsThrown(Entity<XenoLeapingComponent?, ThrownItemComponent?, ActiveXenoToggleChargingComponent?> xeno)
    {
        return xeno.Comp1 != null ||
               xeno.Comp2 != null ||
               Resolve(xeno, ref xeno.Comp3, false) && xeno.Comp3.Stage > 0;
    }

    public override void Update(float frameTime)
    {
        var xenoQuery = EntityQueryEnumerator<XenoComponent>();
        while (xenoQuery.MoveNext(out var uid, out _))
        {
            if (_animateQuery.HasComp(uid))
                continue;

            UpdateSprite(uid);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        var animateQuery = EntityQueryEnumerator<XenoAnimateMovementComponent>();
        while (animateQuery.MoveNext(out var uid, out _))
        {
            UpdateSprite(uid);
        }
    }
}
