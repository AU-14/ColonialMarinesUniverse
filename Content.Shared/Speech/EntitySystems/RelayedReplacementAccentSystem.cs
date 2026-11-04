using Content.Shared.Inventory;
using Content.Shared.Speech.Components;

namespace Content.Shared.Speech.EntitySystems;

/// <summary>
/// Applies replacement accents from equipped inventory without accenting the item itself.
/// </summary>
public sealed partial class RelayedReplacementAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RelayedReplacementAccentComponent, InventoryRelayedEvent<AccentGetEvent>>(OnAccent);
    }

    private void OnAccent(
        Entity<RelayedReplacementAccentComponent> ent,
        ref InventoryRelayedEvent<AccentGetEvent> args)
    {
        if (HasComp<ReplacementAccentComponent>(args.Owner))
            return;

        var accentEvent = args.Args;
        accentEvent.Message = _replacement.ApplyReplacements(accentEvent.Message, ent.Comp.Accent, ent.Owner);
        args.Args = accentEvent;
    }
}
