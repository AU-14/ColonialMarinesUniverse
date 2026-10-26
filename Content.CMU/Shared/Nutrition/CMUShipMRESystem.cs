using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Storage;
using Content.Shared.CMU14.Marines;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;

namespace Content.Shared.CMU14.Nutrition;

/// <summary>
/// Nobody opens an MRE aboard the warship while the mess hall is serving. Once a dropship has been hijacked and
/// crashed into the ship, all bets are off.
/// </summary>
public sealed partial class CMUShipMRESystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MRERequireOpenBeforeStorageComponent, OpenableOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnOpenAttempt(Entity<MRERequireOpenBeforeStorageComponent> ent, ref OpenableOpenAttemptEvent args)
    {
        if (args.Cancelled || args.User is not { } user)
            return;

        if (!HasComp<WarshipComponent>(_transform.GetMap(user)) || AnyHijacked())
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("cmu-mre-ship-mess-hall"), ent, user);
    }

    private bool AnyHijacked()
    {
        var dropships = EntityQueryEnumerator<DropshipComponent>();
        while (dropships.MoveNext(out var dropship))
        {
            if (dropship.Crashed)
                return true;
        }

        return false;
    }
}
