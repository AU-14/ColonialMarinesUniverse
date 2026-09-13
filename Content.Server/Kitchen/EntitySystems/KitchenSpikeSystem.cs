using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.IdentityManagement;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;

namespace Content.Server.Kitchen.EntitySystems;

/// <summary>
/// Preserves RMC's requirement that corpses configured to wait for rot cannot be hooked onto a kitchen spike
/// until they are unrevivable.
/// </summary>
public sealed partial class KitchenSpikeSystem : EntitySystem
{
    [Dependency] private RMCUnrevivableSystem _unrevivable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KitchenSpikeComponent, KitchenSpikeHookAttemptEvent>(OnHookAttempt);
    }

    private void OnHookAttempt(Entity<KitchenSpikeComponent> ent, ref KitchenSpikeHookAttemptEvent args)
    {
        if (CanHook(ent, args.User, args.Victim))
            return;

        args.Cancel();
    }

    private bool CanHook(Entity<KitchenSpikeComponent> spike, EntityUid user, EntityUid victim)
    {
        if (!TryComp<ButcherableComponent>(victim, out var butcherable))
        {
            PopupCannotHook(spike, user, victim, "comp-kitchen-spike-deny-butcher");
            return false;
        }

        if (butcherable.Type != ButcheringType.Spike)
        {
            var message = butcherable.Type == ButcheringType.Knife
                ? "comp-kitchen-spike-deny-butcher-knife"
                : "comp-kitchen-spike-deny-butcher";
            PopupCannotHook(spike, user, victim, message);
            return false;
        }

        if (!butcherable.WaitForRot ||
            _unrevivable.IsUnrevivable(victim))
        {
            return true;
        }

        _popup.PopupEntity(
            Loc.GetString("comp-kitchen-spike-deny-not-rotten",
                ("victim", Identity.Entity(victim, EntityManager)),
                ("this", spike.Owner)),
            victim,
            user);
        return false;
    }

    private void PopupCannotHook(Entity<KitchenSpikeComponent> spike, EntityUid user, EntityUid victim, string message)
    {
        _popup.PopupEntity(
            Loc.GetString(message,
                ("victim", Identity.Entity(victim, EntityManager)),
                ("this", spike.Owner)),
            victim,
            user);
    }
}
