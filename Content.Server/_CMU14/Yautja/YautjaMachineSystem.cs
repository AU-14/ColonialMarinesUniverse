using Content.Shared._CMU14.Yautja;
using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaMachineSystem : EntitySystem
{
    private static readonly EntProtoId HumanSkullPrototype = "CMUYautjaHumanSkullTrophy";
    private static readonly EntProtoId HumanLeftArmBonePrototype = "CMUYautjaHumanLeftArmBoneTrophy";
    private static readonly EntProtoId HumanRightArmBonePrototype = "CMUYautjaHumanRightArmBoneTrophy";
    private static readonly EntProtoId HumanLeftHandBonePrototype = "CMUYautjaHumanLeftHandBoneTrophy";
    private static readonly EntProtoId HumanRightHandBonePrototype = "CMUYautjaHumanRightHandBoneTrophy";
    private static readonly EntProtoId HumanLeftLegBonePrototype = "CMUYautjaHumanLeftLegBoneTrophy";
    private static readonly EntProtoId HumanRightLegBonePrototype = "CMUYautjaHumanRightLegBoneTrophy";
    private static readonly EntProtoId HumanLeftFootBonePrototype = "CMUYautjaHumanLeftFootBoneTrophy";
    private static readonly EntProtoId HumanRightFootBonePrototype = "CMUYautjaHumanRightFootBoneTrophy";
    private static readonly EntProtoId HumanRibcagePrototype = "CMUYautjaHumanRibcageTrophy";

    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaCauldronComponent, ComponentStartup>(OnCauldronStartup);
        SubscribeLocalEvent<YautjaCauldronComponent, ExaminedEvent>(OnCauldronExamined);
        SubscribeLocalEvent<YautjaCauldronComponent, InteractUsingEvent>(OnCauldronInteractUsing);
        SubscribeLocalEvent<YautjaCauldronComponent, YautjaCauldronBoilDoAfterEvent>(OnCauldronBoilDoAfter);
    }

    private void OnCauldronStartup(Entity<YautjaCauldronComponent> ent, ref ComponentStartup args)
    {
        SetCauldronState(ent, ent.Comp.BaseState);
    }

    private void OnCauldronExamined(Entity<YautjaCauldronComponent> ent, ref ExaminedEvent args)
    {
        if (!CanUseYautjaTech(args.Examiner))
            return;

        args.PushMarkup(Loc.GetString("cmu-yautja-cauldron-examine-1"));
        args.PushMarkup(Loc.GetString("cmu-yautja-cauldron-examine-2"));
        args.PushMarkup(Loc.GetString("cmu-yautja-cauldron-examine-3"));
        args.PushMarkup(Loc.GetString("cmu-yautja-cauldron-examine-4"));
    }

    private void OnCauldronInteractUsing(Entity<YautjaCauldronComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!CanUseYautjaTech(args.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-cauldron-denied"), args.User, args.User);
            return;
        }

        if (IsBusyWithCauldron(args.User))
            return;

        if (!TryComp(args.Used, out BodyPartComponent? bodyPart) ||
            bodyPart.Body != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-cauldron-not-limb", ("cauldron", ent.Owner)), args.User, args.User);
            return;
        }

        if (!HasComp<YautjaFlayedComponent>(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-cauldron-limb-not-ready"), args.User, args.User);
            return;
        }

        if (!TryGetBonePrototype(bodyPart, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-cauldron-limb-not-ready"), args.User, args.User);
            return;
        }

        SetCauldronState(ent, ent.Comp.BoilingState);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-cauldron-start", ("limb", args.Used)), args.User, args.User, PopupType.MediumCaution);

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.BoilDelay,
            new YautjaCauldronBoilDoAfterEvent(),
            ent.Owner,
            target: args.Used)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            NeedHand = false,
            BreakOnHandChange = false,
            BreakOnDropItem = false,
            RequireCanInteract = false,
            RangeCheck = false,
            BlockDuplicate = true,
            CancelDuplicate = false,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            ForceVisible = true,
            TargetEffect = "RMCEffectXenoTelegraphRedEmpower",
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            SetCauldronState(ent, ent.Comp.BaseState);
    }

    private void OnCauldronBoilDoAfter(Entity<YautjaCauldronComponent> ent, ref YautjaCauldronBoilDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetCauldronState(ent, ent.Comp.BaseState);

        if (args.Target is not { } limb ||
            Deleted(limb))
        {
            return;
        }

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-cauldron-cancel", ("limb", limb)), args.User, args.User);
            return;
        }

        if (!TryComp(limb, out BodyPartComponent? bodyPart) ||
            bodyPart.Body != null ||
            !HasComp<YautjaFlayedComponent>(limb) ||
            !TryGetBonePrototype(bodyPart, out var prototype))
        {
            return;
        }

        var bone = Spawn(prototype, Transform(ent).Coordinates);
        var trophy = EnsureComp<YautjaTrophyComponent>(bone);
        trophy.Kind = GetTrophyKind(bodyPart);
        trophy.Hunter = args.User;
        trophy.SourceName = Name(limb);
        Dirty(bone, trophy);

        if (bodyPart.PartType == BodyPartType.Head)
            _metadata.SetEntityDescription(bone, Loc.GetString("cmu-yautja-cauldron-skull-desc", ("limb", limb)));

        QueueDel(limb);
    }

    private bool IsBusyWithCauldron(EntityUid user)
    {
        if (!TryComp(user, out DoAfterComponent? doAfter))
            return false;

        foreach (var active in doAfter.DoAfters.Values)
        {
            if (!active.Cancelled &&
                !active.Completed &&
                active.Args.Event is YautjaCauldronBoilDoAfterEvent)
            {
                return true;
            }
        }

        return false;
    }

    private void SetCauldronState(Entity<YautjaCauldronComponent> ent, string state)
    {
        _appearance.SetData(ent.Owner, YautjaCauldronVisuals.State, state);
    }

    private bool CanUseYautjaTech(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               HasComp<YautjaTechAuthorizedComponent>(user);
    }

    private static bool TryGetBonePrototype(BodyPartComponent bodyPart, out EntProtoId prototype)
    {
        switch (bodyPart.PartType)
        {
            case BodyPartType.Head:
                prototype = HumanSkullPrototype;
                return true;
            case BodyPartType.Arm when bodyPart.Symmetry == BodyPartSymmetry.Left:
                prototype = HumanLeftArmBonePrototype;
                return true;
            case BodyPartType.Arm when bodyPart.Symmetry == BodyPartSymmetry.Right:
                prototype = HumanRightArmBonePrototype;
                return true;
            case BodyPartType.Hand when bodyPart.Symmetry == BodyPartSymmetry.Left:
                prototype = HumanLeftHandBonePrototype;
                return true;
            case BodyPartType.Hand when bodyPart.Symmetry == BodyPartSymmetry.Right:
                prototype = HumanRightHandBonePrototype;
                return true;
            case BodyPartType.Leg when bodyPart.Symmetry == BodyPartSymmetry.Left:
                prototype = HumanLeftLegBonePrototype;
                return true;
            case BodyPartType.Leg when bodyPart.Symmetry == BodyPartSymmetry.Right:
                prototype = HumanRightLegBonePrototype;
                return true;
            case BodyPartType.Foot when bodyPart.Symmetry == BodyPartSymmetry.Left:
                prototype = HumanLeftFootBonePrototype;
                return true;
            case BodyPartType.Foot when bodyPart.Symmetry == BodyPartSymmetry.Right:
                prototype = HumanRightFootBonePrototype;
                return true;
            case BodyPartType.Torso:
                prototype = HumanRibcagePrototype;
                return true;
            default:
                prototype = default;
                return false;
        }
    }

    private static YautjaTrophyKind GetTrophyKind(BodyPartComponent bodyPart)
    {
        return bodyPart.PartType switch
        {
            BodyPartType.Head => YautjaTrophyKind.HumanSkull,
            BodyPartType.Arm when bodyPart.Symmetry == BodyPartSymmetry.Left => YautjaTrophyKind.HumanLeftArmBone,
            BodyPartType.Arm when bodyPart.Symmetry == BodyPartSymmetry.Right => YautjaTrophyKind.HumanRightArmBone,
            BodyPartType.Hand when bodyPart.Symmetry == BodyPartSymmetry.Left => YautjaTrophyKind.HumanLeftHandBone,
            BodyPartType.Hand when bodyPart.Symmetry == BodyPartSymmetry.Right => YautjaTrophyKind.HumanRightHandBone,
            BodyPartType.Leg when bodyPart.Symmetry == BodyPartSymmetry.Left => YautjaTrophyKind.HumanLeftLegBone,
            BodyPartType.Leg when bodyPart.Symmetry == BodyPartSymmetry.Right => YautjaTrophyKind.HumanRightLegBone,
            BodyPartType.Foot when bodyPart.Symmetry == BodyPartSymmetry.Left => YautjaTrophyKind.HumanLeftFootBone,
            BodyPartType.Foot when bodyPart.Symmetry == BodyPartSymmetry.Right => YautjaTrophyKind.HumanRightFootBone,
            BodyPartType.Torso => YautjaTrophyKind.HumanRibcage,
            _ => throw new ArgumentOutOfRangeException(nameof(bodyPart)),
        };
    }
}
