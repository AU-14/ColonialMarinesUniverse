using Content.Server.Explosion.EntitySystems;
using Content.Shared._CMU14.Explosion;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Map;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Server._CMU14.Explosion;

public sealed partial class CMUBangaloreSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private CollisionWakeSystem _collisionWake = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUBangaloreComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CMUBangaloreComponent, CMUBangaloreDeployDoAfterEvent>(OnDeployDoAfter);
    }

    private void OnUseInHand(Entity<CMUBangaloreComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !CanDeployPopup(ent, args.User))
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.PlacementDelay, new CMUBangaloreDeployDoAfterEvent(), ent, ent, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDeployDoAfter(Entity<CMUBangaloreComponent> ent, ref CMUBangaloreDeployDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        if (!CanDeployPopup(ent, args.User))
            return;

        var coordinates = _transform.GetMoverCoordinates(args.User);
        _transform.SetCoordinates(ent, coordinates);
        _transform.AnchorEntity(ent);
        _physics.SetBodyType(ent, BodyType.Static);
        _collisionWake.SetEnabled(ent, false);

        // No longer an item once placed - can't be picked back up.
        RemComp<ItemComponent>(ent);

        _audio.PlayPvs(ent.Comp.DeploySound, ent);

        if (TryComp(ent, out RMCExplosiveDeleteComponent? delete))
            _trigger.HandleTimerTrigger(ent, args.User, delete.Delay, delete.BeepInterval, delete.InitialBeepDelay, delete.BeepSound);
    }

    private bool CanDeployPopup(Entity<CMUBangaloreComponent> ent, EntityUid user)
    {
        if (_container.IsEntityInContainer(user))
        {
            _popup.PopupClient(Loc.GetString("rmc-explosive-deploy-container", ("explosive", ent)), user, user, PopupType.SmallCaution);
            return false;
        }

        var coordinates = _transform.GetMoverCoordinates(user);
        if (_rmcMap.IsTileBlocked(coordinates))
        {
            _popup.PopupClient(Loc.GetString("cmu-bangalore-deploy-fail-blocked"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }
}
