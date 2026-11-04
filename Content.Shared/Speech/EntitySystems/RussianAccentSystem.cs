using Content.Shared.Speech.Components;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class RussianAccentSystem : RelayAccentSystem<RussianAccentComponent>
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override string Accentuate(string message, Entity<RussianAccentComponent>? ent = null)
    {
        // RMC14: keep word replacements while omitting Cyrillic lookalike substitutions for accessibility.
        return _replacement.ApplyReplacements(message, "russian", ent?.Owner);
    }
}
