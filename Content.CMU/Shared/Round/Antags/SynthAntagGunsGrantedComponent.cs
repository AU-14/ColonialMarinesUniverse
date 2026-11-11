using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Round.Antags;

/// <summary>
/// The synth's gun restriction was lifted because its mind holds an antagonist role. Marks that the grant
/// came from antag status, so it is taken back when the last antag role goes (a reset key) and never
/// touches synths that could fire guns anyway.
/// </summary>
[RegisterComponent]
public sealed partial class SynthAntagGunsGrantedComponent : Component;
