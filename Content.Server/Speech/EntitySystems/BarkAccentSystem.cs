using Content.Server.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class BarkAccentSystem : RelayAccentSystem<BarkAccentComponent>
{
    public sealed partial class BarkAccentSystem : EntitySystem
    {
        [Dependency] private IRobustRandom _random = default!;

    public override string Accentuate(string message, Entity<BarkAccentComponent>? ent = null)
    {
        foreach (var (word, repl) in SpecialWords)
        {
            message = message.Replace(word, repl);
        }

        return message.Replace("!", _random.Pick(Barks))
            .Replace("l", "r").Replace("L", "R");
    }
}
