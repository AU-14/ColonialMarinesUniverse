using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// Shared Yautja rules: raised thralls carry a visible corruption on examine.
/// </summary>
public sealed partial class YautjaRestrictionsSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaThrallComponent, ExaminedEvent>(OnThrallExamined);
    }

    private void OnThrallExamined(Entity<YautjaThrallComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Raised)
            args.PushMarkup(Loc.GetString("cmu-yautja-thrall-raised-examine"));
    }
}
