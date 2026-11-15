using Content.Shared._RMC14.Standing;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared.CMU14.Surrender;

public sealed class CMUSurrenderMovementSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSurrenderComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<CMUSurrenderComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<CMUSurrenderComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
    }

    private void OnInit(Entity<CMUSurrenderComponent> ent, ref ComponentInit args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnRemove(Entity<CMUSurrenderComponent> ent, ref ComponentRemove args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnRefresh(Entity<CMUSurrenderComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage >= ComponentLifeStage.Stopping
            || !TryComp<MovementSpeedModifierComponent>(ent, out var movement))
        {
            return;
        }

        args.ModifySpeed(0.5f, 0.5f);
        var cap = movement.BaseWalkSpeed;

        if (TryComp<RMCRestComponent>(ent, out var rest) && rest.Resting)
            cap = MathF.Min(cap, rest.RestingSpeed * movement.BaseWalkSpeed);
        args.CapSpeed(cap * 0.5f);
    }
}
