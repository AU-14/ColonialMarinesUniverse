using Content.Shared.CMU14.Item.Stain;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaTrophySystem
{
    [Dependency] private CMUItemStainSystem _stains = default!;

    private void InitializeRag()
    {
        SubscribeLocalEvent<YautjaPolishingRagComponent, AfterInteractEvent>(OnRagAfterInteract);
        SubscribeLocalEvent<YautjaPolishingRagComponent, YautjaRagWipeDoAfterEvent>(OnRagWipeDoAfter);
    }

    // trophies are handled on InteractUsing before this, so anything left is a stained mob or item
    private void OnRagAfterInteract(Entity<YautjaPolishingRagComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasBloodToWipe(target))
            return;

        args.Handled = true;

        if (!CanUseYautjaTech(args.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-polish-denied"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.DoAfter,
            new YautjaRagWipeDoAfterEvent(),
            ent.Owner,
            target: target,
            used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            DistanceThreshold = 1.5f,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cmu-yautja-rag-wipe-start", ("target", target)), args.User, args.User);
    }

    private void OnRagWipeDoAfter(Entity<YautjaPolishingRagComponent> ent, ref YautjaRagWipeDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;

        var wiped = HasComp<MobStateComponent>(target)
            ? _stains.WipeBloodOffEquipment(target)
            : _stains.IsBloodStained(target) && _stains.TryClean(target);

        if (!wiped)
            return;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-rag-wiped", ("target", target)), args.User, args.User);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):hunter} wiped the blood off {ToPrettyString(target):target}");
    }

    // a mob means everything they have on, otherwise just the one item
    private bool HasBloodToWipe(EntityUid target)
    {
        return HasComp<MobStateComponent>(target)
            ? _stains.HasBloodStainedEquipment(target)
            : _stains.IsBloodStained(target);
    }
}
