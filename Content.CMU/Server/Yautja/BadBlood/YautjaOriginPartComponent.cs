namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// Marks a severed body part, and the detached carrier holding it, as cut from a Yautja.
/// The cauldron refuses these, so a hunter's limb cannot be boiled into a "human" bone trophy.
/// </summary>
[RegisterComponent]
public sealed partial class YautjaOriginPartComponent : Component;
