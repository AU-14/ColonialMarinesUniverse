using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Threats.Mobs;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMULurkerStationaryCloakComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan InvisibilityDelay = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public TimeSpan LastMovementAt;

    [DataField, AutoNetworkedField]
    public float Opacity = 1f;

    [DataField, AutoNetworkedField]
    public bool FullyInvisible;

    [DataField, AutoNetworkedField]
    public bool PounceFromCloak;
}

public sealed partial class CMULurkerStationaryCloakSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMULurkerStationaryCloakComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMULurkerStationaryCloakComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMULurkerStationaryCloakComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<CMULurkerStationaryCloakComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<CMULurkerStationaryCloakComponent, XenoLeapHitEvent>(OnLeapHit);
        SubscribeLocalEvent<XenoLeapingComponent, ComponentStartup>(OnLeapStarted);
        SubscribeLocalEvent<XenoLeapingComponent, ComponentRemove>(OnLeapEnded);
        SubscribeLocalEvent<EntityActiveInvisibleComponent, ComponentRemove>(OnActiveInvisibleRemoved);
    }

    private void OnStartup(Entity<CMULurkerStationaryCloakComponent> lurker, ref ComponentStartup args)
    {
        lurker.Comp.LastMovementAt = _timing.CurTime;
        Dirty(lurker);
    }

    private void OnShutdown(Entity<CMULurkerStationaryCloakComponent> lurker, ref ComponentShutdown args)
    {
        RemCompDeferred<EntityActiveInvisibleComponent>(lurker);
    }

    private void OnMoveInput(Entity<CMULurkerStationaryCloakComponent> lurker, ref MoveInputEvent args)
    {
        if (args.HasDirectionalMovement && !HasComp<XenoLeapingComponent>(lurker))
            BreakCloak(lurker);
    }

    private void OnMove(Entity<CMULurkerStationaryCloakComponent> lurker, ref MoveEvent args)
    {
        if (!HasComp<XenoLeapingComponent>(lurker))
            BreakCloak(lurker);
    }

    private void OnLeapStarted(Entity<XenoLeapingComponent> lurker, ref ComponentStartup args)
    {
        if (!TryComp<CMULurkerStationaryCloakComponent>(lurker, out var cloak))
            return;

        cloak.PounceFromCloak = cloak.FullyInvisible;
        Dirty(lurker, cloak);
    }

    private void OnLeapEnded(Entity<XenoLeapingComponent> lurker, ref ComponentRemove args)
    {
        if (TryComp<CMULurkerStationaryCloakComponent>(lurker, out var cloak))
            BreakCloak((lurker.Owner, cloak));
    }

    private void OnLeapHit(Entity<CMULurkerStationaryCloakComponent> lurker, ref XenoLeapHitEvent args)
    {
        if (lurker.Comp.PounceFromCloak)
            _pulling.TryStartPull(lurker, args.Hit);

        BreakCloak(lurker);
    }

    private void OnActiveInvisibleRemoved(Entity<EntityActiveInvisibleComponent> ent, ref ComponentRemove args)
    {
        if (TryComp<CMULurkerStationaryCloakComponent>(ent, out var cloak) && cloak.FullyInvisible)
            BreakCloak((ent.Owner, cloak));
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMULurkerStationaryCloakComponent>();
        while (query.MoveNext(out var uid, out var cloak))
        {
            if (cloak.PounceFromCloak && HasComp<XenoLeapingComponent>(uid))
                continue;

            var progress = cloak.InvisibilityDelay <= TimeSpan.Zero
                ? 1f
                : Math.Clamp((float) ((now - cloak.LastMovementAt).TotalSeconds / cloak.InvisibilityDelay.TotalSeconds), 0f, 1f);
            var opacity = 1f - progress;
            if (MathF.Abs(cloak.Opacity - opacity) >= 0.01f)
            {
                cloak.Opacity = opacity;
                Dirty(uid, cloak);
            }

            var fullyInvisible = progress >= 1f;
            if (cloak.FullyInvisible != fullyInvisible)
            {
                cloak.FullyInvisible = fullyInvisible;
                Dirty(uid, cloak);
            }

            if (!fullyInvisible)
                continue;

            var invisible = EnsureComp<EntityActiveInvisibleComponent>(uid);
            if (invisible.Opacity != 0f)
            {
                invisible.Opacity = 0f;
                Dirty(uid, invisible);
            }
        }
    }

    private void BreakCloak(Entity<CMULurkerStationaryCloakComponent> lurker)
    {
        lurker.Comp.LastMovementAt = _timing.CurTime;
        lurker.Comp.Opacity = 1f;
        lurker.Comp.FullyInvisible = false;
        lurker.Comp.PounceFromCloak = false;
        Dirty(lurker);
        RemCompDeferred<EntityActiveInvisibleComponent>(lurker);
    }
}