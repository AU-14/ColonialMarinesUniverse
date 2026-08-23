using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Camera;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Body.Part;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Player;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaHellhoundSystem : EntitySystem
{
    private const float FullCircle = MathF.PI * 2;
    private const int DirectionCount = 8;
    private const string HumanSpecies = "Human";

    private static readonly LocId[] Directions =
    {
        "cmu-yautja-hellhound-direction-north",
        "cmu-yautja-hellhound-direction-northeast",
        "cmu-yautja-hellhound-direction-east",
        "cmu-yautja-hellhound-direction-southeast",
        "cmu-yautja-hellhound-direction-south",
        "cmu-yautja-hellhound-direction-southwest",
        "cmu-yautja-hellhound-direction-west",
        "cmu-yautja-hellhound-direction-northwest",
    };

    [Dependency] private IChatManager _chat = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedRMCCameraSystem _rmcCamera = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaHellhoundComponent, YautjaHellhoundSenseOwnerActionEvent>(OnSenseOwner);
        SubscribeLocalEvent<YautjaHellhoundComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<YautjaHellhoundComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<YautjaHellhoundComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<YautjaHellhoundComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
    }

    private void OnSenseOwner(Entity<YautjaHellhoundComponent> ent, ref YautjaHellhoundSenseOwnerActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;

        if (ent.Comp.YautjaOwner is not { } owner ||
            Deleted(owner))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hellhound-no-owner"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var houndCoords = _transform.GetMapCoordinates(ent);
        var ownerCoords = _transform.GetMapCoordinates(owner);
        if (houndCoords.MapId != ownerCoords.MapId)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hellhound-owner-wrong-place"), ent, ent, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-yautja-hellhound-sniffs", ("hellhound", ent.Owner)),
            ent,
            Filter.PvsExcept(ent.Owner),
            true,
            PopupType.Small);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-hellhound-sense-start"), ent, ent);

        SendSenseOwnerResult(ent, owner);
    }

    private void OnStartup(Entity<YautjaHellhoundComponent> ent, ref ComponentStartup args)
    {
        RemCompDeferred<HiveMemberComponent>(ent);
    }

    private void OnGetMeleeDamage(Entity<YautjaHellhoundComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (args.User != ent.Owner ||
            !TryComp<BodyZoneTargetingComponent>(ent, out var targeting))
        {
            return;
        }

        var (partType, _) = SharedBodyZoneTargetingSystem.ToBodyPart(targeting.Selected);
        if (partType is BodyPartType.Arm or BodyPartType.Leg)
            args.Damage *= ent.Comp.LimbTargetDamageMultiplier;
    }

    private void OnExamined(Entity<YautjaHellhoundComponent> ent, ref ExaminedEvent args)
    {
        if (TryComp<HumanoidAppearanceComponent>(args.Examiner, out var humanoid) &&
            humanoid.Species == HumanSpecies)
        {
            args.PushMarkup(Loc.GetString("cmu-yautja-hellhound-examine-human"));
            return;
        }

        if (!HasComp<YautjaComponent>(args.Examiner))
            return;

        if (ent.Comp.YautjaOwner is not { } owner ||
            Deleted(owner))
        {
            args.PushMarkup(Loc.GetString("cmu-yautja-hellhound-examine-no-owner"));
            return;
        }

        args.PushMarkup(Loc.GetString("cmu-yautja-hellhound-examine-owner", ("owner", owner)));
    }

    private void OnMobStateChanged(Entity<YautjaHellhoundComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            RemComp<RMCCameraComponent>(ent);
            _rmcCamera.RefreshCameras(ent.Comp.CameraId);
            return;
        }

        var camera = EnsureComp<RMCCameraComponent>(ent);
        _rmcCamera.SetCameraRename(ent, false, camera);
        _rmcCamera.SetCameraId(ent, ent.Comp.CameraId, camera);
        _rmcCamera.RefreshCameras(ent.Comp.CameraId);
    }

    private void SendSenseOwnerResult(EntityUid hellhound, EntityUid owner)
    {
        if (Deleted(hellhound))
            return;

        if (Deleted(owner))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hellhound-no-owner"), hellhound, hellhound, PopupType.SmallCaution);
            return;
        }

        var houndCoords = _transform.GetMapCoordinates(hellhound);
        var ownerCoords = _transform.GetMapCoordinates(owner);
        if (houndCoords.MapId != ownerCoords.MapId)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hellhound-owner-wrong-place"), hellhound, hellhound, PopupType.SmallCaution);
            return;
        }

        var offset = ownerCoords.Position - houndCoords.Position;
        var distance = (int) MathF.Ceiling(offset.Length());
        var direction = Loc.GetString(GetDirection(offset));
        var message = Loc.GetString("cmu-yautja-hellhound-sense-owner", ("distance", distance), ("direction", direction));

        _popup.PopupEntity(message, hellhound, hellhound);

        if (TryComp<ActorComponent>(hellhound, out var actor))
            _chat.DispatchServerMessage(actor.PlayerSession, message, suppressLog: true);
    }

    private static LocId GetDirection(Vector2 offset)
    {
        if (offset.LengthSquared() <= 0.001f)
            return Directions[0];

        var angle = MathF.Atan2(offset.X, offset.Y);
        if (angle < 0)
            angle += FullCircle;

        var sector = FullCircle / DirectionCount;
        var index = (int) MathF.Floor((angle + sector / 2f) / sector) % DirectionCount;
        return Directions[index];
    }
}
