using System.Numerics;
using Content.Server.Decals;
using Content.Shared.Administration.Logs;
using Content.Shared.CMU14.Weapons.Gauss;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Weapons.Gauss;

/// <summary>
///     Vaporizes the holder of an overcharged M60 gauss gun: the gun is blown out of their hands, they stand
///     frozen and blackening for a moment, then they're gone, leaving their boots smoking on a scorch mark.
/// </summary>
public sealed class CMUGaussVaporizeSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public void Start(EntityUid user, EntityUid gun)
    {
        if (HasComp<CMUGaussVaporizingComponent>(user))
            return;

        // Added first, so the pain of the blast can't make them scream over the scream below.
        var comp = AddComp<CMUGaussVaporizingComponent>(user);
        comp.Start = _timing.CurTime;
        comp.End = comp.Start + comp.Duration;
        Dirty(user, comp);

        var coords = _transform.GetMoverCoordinates(user);
        var facing = _transform.GetWorldRotation(user).ToWorldVec();

        // The blast kicks the gun a short way back from where its holder was facing.
        if (!_hands.TryDrop(user, gun, coords, checkActionBlocker: false))
            _transform.DropNextTo(gun, user);

        _throwing.TryThrow(gun, -facing * comp.FlingDistance, comp.FlingSpeed, user: user, recoil: false, playSound: false);

        var scream = CompOrNull<HumanoidProfileComponent>(user)?.Sex == Sex.Female ? comp.ScreamFemale : comp.Scream;
        _audio.PlayPvs(scream, user);
        _audio.PlayPvs(comp.StartSound, coords);

        _adminLog.Add(LogType.Gib, LogImpact.High,
            $"{ToPrettyString(user):player} was vaporized by an overcharged {ToPrettyString(gun):gun}");
        _popup.PopupCoordinates(Loc.GetString("cmu-gauss-vaporized", ("user", user)), coords, PopupType.LargeCaution);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUGaussVaporizingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.End && !TerminatingOrDeleted(uid))
                Finish((uid, comp));
        }
    }

    private void Finish(Entity<CMUGaussVaporizingComponent> ent)
    {
        var coords = _transform.GetMoverCoordinates(ent);

        // Their own boots if they had any on, otherwise a stock pair. Anything tucked into them goes too.
        if (!_inventory.TryUnequip(ent, "shoes", out var boots, silent: true, force: true))
            boots = Spawn(ent.Comp.Boots, coords);

        foreach (var container in _container.GetAllContainers(boots.Value))
        {
            _container.CleanContainer(container);
        }

        // Set down upright, rather than keeping whatever rotation they had on the holder's feet.
        _transform.SetCoordinates(boots.Value, coords);
        _transform.SetLocalRotation(boots.Value, Angle.Zero);
        SpawnAttachedTo(ent.Comp.Smoke, new EntityCoordinates(boots.Value, Vector2.Zero));

        // Decals are placed by their bottom-left corner, so centre it on the tile they stood on.
        _decals.TryAddDecal(ent.Comp.Decal, coords.Offset(new Vector2(-0.5f, -0.5f)), out _, cleanable: true);
        _audio.PlayPvs(ent.Comp.EndSound, coords);

        // Deleting them takes everything they carried with them; their mind is moved to a ghost.
        QueueDel(ent);
    }
}
