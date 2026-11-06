using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Vehicle.VBIED;

/// <summary>
/// Offers rigging a held IED inside a civilian vehicle, and looking any vehicle over for one. Whether a
/// vehicle is rigged stays on the server: the rig verbs show either way and only the server answers an
/// inspection.
/// </summary>
public sealed partial class CMUVBIEDSystem : EntitySystem
{
    public static readonly TimeSpan InspectDelay = TimeSpan.FromSeconds(5);

    private static readonly SpriteSpecifier InspectIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/examine.svg.192dpi.png"));

    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVBIEDRiggableComponent, GetVerbsEvent<InteractionVerb>>(OnRiggableGetVerbs);
        SubscribeLocalEvent<GridVehicleMoverComponent, GetVerbsEvent<Verb>>(OnVehicleGetVerbs);
    }

    private void OnRiggableGetVerbs(Entity<CMUVBIEDRiggableComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null ||
            args.Using is not { } ied || !HasComp<CMUVBIEDComponent>(ied))
        {
            return;
        }

        var user = args.User;
        var vehicle = ent.Owner;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-vbied-verb-rig-impact"),
            Act = () => TryStartRig(user, vehicle, ied, CMUVBIEDTrigger.Impact),
            Priority = 1,
        });
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-vbied-verb-rig-proximity"),
            Act = () => TryStartRig(user, vehicle, ied, CMUVBIEDTrigger.Proximity),
        });
    }

    private void OnVehicleGetVerbs(Entity<GridVehicleMoverComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var vehicle = ent.Owner;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-vbied-verb-inspect"),
            Icon = InspectIcon,
            Act = () => TryStartInspect(user, vehicle),
        });
    }

    public bool TryStartRig(EntityUid user, EntityUid vehicle, EntityUid ied, CMUVBIEDTrigger trigger)
    {
        if (!TryComp(ied, out CMUVBIEDComponent? vbied))
            return false;

        var args = new DoAfterArgs(EntityManager, user, vbied.RigDelay, new CMUVBIEDRigDoAfterEvent(trigger), vehicle,
            target: vehicle, used: ied)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        _popup.PopupClient(Loc.GetString("cmu-vbied-rig-start", ("vehicle", vehicle)), vehicle, user);
        return true;
    }

    public bool TryStartInspect(EntityUid user, EntityUid vehicle)
    {
        var args = new DoAfterArgs(EntityManager, user, InspectDelay, new CMUVBIEDInspectDoAfterEvent(), vehicle,
            target: vehicle)
        {
            BreakOnMove = true,
            NeedHand = false,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        _popup.PopupClient(Loc.GetString("cmu-vbied-inspect-start", ("vehicle", vehicle)), vehicle, user);
        return true;
    }
}
