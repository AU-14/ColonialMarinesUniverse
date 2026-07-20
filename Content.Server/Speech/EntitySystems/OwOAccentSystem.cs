using Content.Server.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class OwOAccentSystem : RelayAccentSystem<OwOAccentComponent>
{
    public sealed partial class OwOAccentSystem : EntitySystem
    {
        [Dependency] private IRobustRandom _random = default!;

    public override string Accentuate(string message, Entity<OwOAccentComponent>? ent = null)
    {
        foreach (var (word, repl) in SpecialWords)
        {
            message = message.Replace(word, repl);
        }

        return message.Replace("!", _random.Pick(Faces))
            .Replace("r", "w").Replace("R", "W")
            .Replace("l", "w").Replace("L", "W");
    }
}
