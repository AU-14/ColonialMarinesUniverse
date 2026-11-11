using System.Text.RegularExpressions;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Traits.Speech;

/// <summary>A light lisp: only some words come out lisped.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMULightLispComponent : BaseAccentComponent
{
    /// <summary>Chance each word is lisped.</summary>
    [DataField]
    public float WordChance = 0.3f;
}

public sealed partial class CMULightLispSystem : RelayAccentSystem<CMULightLispComponent>
{
    private static readonly Regex Words = new(@"\S+", RegexOptions.Compiled);

    [Dependency] private FrontalLispSystem _lisp = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override string Accentuate(string message, Entity<CMULightLispComponent>? ent = null)
    {
        var chance = ent?.Comp.WordChance ?? 0.3f;
        return Words.Replace(message, match => _random.Prob(chance) ? _lisp.Accentuate(match.Value) : match.Value);
    }
}
