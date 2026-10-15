using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Item.Stain;

/// <summary>
/// Lets someone with blood or acid on their clothes rub it off on a wall.
/// </summary>
public sealed class CMUWallWipeSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly TimeSpan WipeDelay = TimeSpan.FromSeconds(6);
    private const float WipeRange = 1.6f;

    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUItemStainSystem _stains = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TagComponent, GetVerbsEvent<Verb>>(OnWallVerbs);
        SubscribeLocalEvent<TagComponent, CMUWallWipeDoAfterEvent>(OnWipeDoAfter);
    }

    private void OnWallVerbs(Entity<TagComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        // Walls are solid, so check plain distance rather than line of sight to the wall.
        if (!args.CanInteract || !_tag.HasTag(ent.Comp, WallTag) ||
            !_transform.InRange(args.User, ent.Owner, WipeRange) ||
            !_stains.HasBloodStainedEquipment(args.User))
        {
            return;
        }

        var user = args.User;
        var wall = ent.Owner;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-wall-wipe-verb"),
            Act = () => StartWipe(user, wall),
        });
    }

    private void StartWipe(EntityUid user, EntityUid wall)
    {
        // No do-after target: its reach check can fail against a solid wall. Moving away cancels it instead.
        var doAfter = new DoAfterArgs(EntityManager, user, WipeDelay, new CMUWallWipeDoAfterEvent(), wall)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnWipeDoAfter(Entity<TagComponent> ent, ref CMUWallWipeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        _stains.WipeBloodOffEquipment(args.User);
        _popup.PopupClient(Loc.GetString("cmu-wall-wipe-done", ("wall", ent.Owner)), args.User, args.User);
    }
}

[Serializable, NetSerializable]
public sealed partial class CMUWallWipeDoAfterEvent : SimpleDoAfterEvent;
