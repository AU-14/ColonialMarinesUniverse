using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared.StatusEffectNew;

/// <summary>
/// Handler for <see cref="ExaminableStatusEffectComponent"/>.
/// </summary>
public sealed partial class ExaminableStatusEffectSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    [SubscribeLocalEvent]
    private void OnExaminedEvent(Entity<ExaminableStatusEffectComponent> ent, ref StatusEffectRelayedEvent<ExaminedEvent> args)
    {
        EntityUid? canonical = null;
        foreach (var effect in _statusEffects.EnumerateStatusEffects<ExaminableStatusEffectComponent>((args.AppliedTo, null)))
        {
            if (!effect.Comp1.Applied || effect.Comp2.MessageId != ent.Comp.MessageId)
                continue;

            if (canonical is null || effect.Owner.Id < canonical.Value.Id)
                canonical = effect.Owner;
        }

        if (canonical is not { } owner || owner != ent.Owner)
            return;

        using (args.Args.PushGroup(nameof(ExaminableStatusEffectSystem)))
        {
            args.Args.PushMarkup(Loc.GetString(ent.Comp.MessageId, ("target", Identity.Entity(args.AppliedTo, EntityManager))));
        }
    }
}
