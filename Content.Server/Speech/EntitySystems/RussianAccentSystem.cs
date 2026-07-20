using System.Text;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class RussianAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<RussianAccentComponent, AccentGetEvent>(OnAccent);
    }

    public override string Accentuate(string message, Entity<RussianAccentComponent>? ent = null)
    {
        var accentedMessage = new StringBuilder(_replacement.ApplyReplacements(message, "russian"));

        // RMC CHANGE Removed the letter swaps for accessibility. Accent still contains word swaps like yes and no.

        // for (var i = 0; i < accentedMessage.Length; i++)
        // {
        //     var c = accentedMessage[i];
        //
        //     accentedMessage[i] = c switch
        //     {
        //         'b' => 'в',
        //         'N' => 'И',
        //         'n' => 'и',
        //         'K' => 'К',
        //         'k' => 'к',
        //         'm' => 'м',
        //         'h' => 'н',
        //         't' => 'т',
        //         'R' => 'Я',
        //         'r' => 'я',
        //         'Y' => 'У',
        //         'W' => 'Ш',
        //         'w' => 'ш',
        //         _ => accentedMessage[i]
        //     };
        // }

        return accentedMessage.ToString();
    }
}
