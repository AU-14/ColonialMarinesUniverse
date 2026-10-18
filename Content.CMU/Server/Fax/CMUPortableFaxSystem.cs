using Content.Shared.CMU14.Fax;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DoAfter;
using Content.Shared.Fax.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Fax;

/// <summary>
///     The journalist's portable fax machine: carried on the back, set up where you're standing, and packed back up
///     once it's empty. The fax machine keeps its name between the two.
/// </summary>
public sealed class CMUPortableFaxSystem : EntitySystem
{
    private static readonly SoundSpecifier DeploySound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
    private static readonly SoundSpecifier PackSound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");

    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPortableFaxComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CMUPortableFaxComponent, CMUPortableFaxDeployDoAfterEvent>(OnDeployDoAfter);
        SubscribeLocalEvent<CMUPortableFaxDeployedComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUPortableFaxDeployedComponent, CMUPortableFaxPackDoAfterEvent>(OnPackDoAfter);
    }

    private bool CanDeployAt(EntityUid user)
    {
        var coords = Transform(user).Coordinates;
        if (!_turf.TryGetTileRef(coords, out var tile) ||
            tile.Value.Tile.IsEmpty ||
            _turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable))
        {
            return false;
        }

        foreach (var _ in _lookup.GetEntitiesInRange<FaxMachineComponent>(coords, 0.4f))
        {
            return false;
        }

        return true;
    }

    private void OnUseInHand(Entity<CMUPortableFaxComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var user = args.User;
        if (!CanDeployAt(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-portable-fax-blocked", ("fax", ent.Owner)), user, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-portable-fax-deploying", ("fax", ent.Owner)), user, user);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.Delay, new CMUPortableFaxDeployDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnDeployDoAfter(Entity<CMUPortableFaxComponent> ent, ref CMUPortableFaxDeployDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || TerminatingOrDeleted(ent))
            return;

        args.Handled = true;

        var user = args.User;
        if (!CanDeployAt(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-portable-fax-blocked", ("fax", ent.Owner)), user, user);
            return;
        }

        var coords = Transform(user).Coordinates.SnapToGrid(EntityManager);
        var fax = Spawn(ent.Comp.Deployed, coords);
        if (ent.Comp.FaxName is { } name && TryComp(fax, out FaxMachineComponent? faxComp))
            faxComp.FaxName = name;

        _audio.PlayPvs(DeploySound, fax, AudioParams.Default.WithVolume(-4));
        _popup.PopupEntity(Loc.GetString("cmu-portable-fax-deployed", ("fax", fax)), user, user);
        Del(ent);
    }

    private void OnGetVerbs(Entity<CMUPortableFaxDeployedComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-portable-fax-pack-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/pickup.svg.192dpi.png")),
            Act = () => StartPacking(ent, user),
        });
    }

    private bool IsEmpty(EntityUid fax)
    {
        return !TryComp(fax, out FaxMachineComponent? comp) ||
               comp.PaperSlot.Item == null && comp.PrintingQueue.Count == 0;
    }

    private void StartPacking(Entity<CMUPortableFaxDeployedComponent> ent, EntityUid user)
    {
        if (!IsEmpty(ent))
        {
            _popup.PopupEntity(Loc.GetString("cmu-portable-fax-not-empty", ("fax", ent.Owner)), ent, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-portable-fax-packing", ("fax", ent.Owner)), ent, user);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.Delay, new CMUPortableFaxPackDoAfterEvent(), ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnPackDoAfter(Entity<CMUPortableFaxDeployedComponent> ent, ref CMUPortableFaxPackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || TerminatingOrDeleted(ent))
            return;

        args.Handled = true;

        var user = args.User;
        if (!IsEmpty(ent))
        {
            _popup.PopupEntity(Loc.GetString("cmu-portable-fax-not-empty", ("fax", ent.Owner)), ent, user);
            return;
        }

        var item = Spawn(ent.Comp.Item, Transform(ent).Coordinates);
        if (TryComp(ent, out FaxMachineComponent? faxComp) && TryComp(item, out CMUPortableFaxComponent? portable))
            portable.FaxName = faxComp.FaxName;

        _audio.PlayPvs(PackSound, item);
        _popup.PopupEntity(Loc.GetString("cmu-portable-fax-packed", ("fax", item)), user, user);
        Del(ent);
        _hands.PickupOrDrop(user, item);
    }
}
