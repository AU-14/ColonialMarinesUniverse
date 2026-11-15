/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared.CMU14.Chemistry.Effects.Special;

namespace Content.Shared._RMC14.Chemistry.Effects;

// Same private [Dependency] + internal accessor pattern as RMCChemicalEffectSystem.Dependencies.cs.
// Fold these two lines into that file if you'd rather not keep a separate partial.
public sealed partial class RMCChemicalEffectSystem
{
    [Dependency] private HyperthrottlingSystem _languageGrant = default!;

    internal HyperthrottlingSystem LanguageGrant => _languageGrant;
}
