using Content.Shared.AU14.Salvage;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Server.AU14.Salvage;

public sealed partial class SalvageSpawnerSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SalvageSpawnerComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<SalvageSpawnerComponent, SalvageSpawnerDoAfterEvent>(OnDoAfter);
    }

    private void OnInteractHand(EntityUid uid, SalvageSpawnerComponent comp, InteractHandEvent args)
    {
        if (args.Handled)
            return;
        if (comp.Loot.Count == 0)
            return;

        _popup.PopupEntity("You rummage through the debris...", uid, args.User);

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            comp.DoAfterTime,
            new SalvageSpawnerDoAfterEvent { User = GetNetEntity(args.User) },
            uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void OnDoAfter(EntityUid uid, SalvageSpawnerComponent comp, SalvageSpawnerDoAfterEvent args)
    {
        if (args.Cancelled || comp.Loot.Count == 0)
            return;

        var user = GetEntity(args.User);
        var pick = _random.Pick(comp.Loot);
        Spawn(pick, Transform(user).Coordinates);
        _popup.PopupEntity("You find something worth salvaging!", uid, user);
    }
}
