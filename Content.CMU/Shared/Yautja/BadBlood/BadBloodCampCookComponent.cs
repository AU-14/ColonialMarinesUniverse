using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// The Lodge spit. Bad Bloods cook whole prey corpses and loose prey meat on it, and fill their flasks at it.
/// The cooking itself is in the server's <c>BadBloodCampSystem</c> spit partial.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BadBloodCampCookComponent : Component
{
    [DataField]
    public TimeSpan CorpseCookTime = TimeSpan.FromSeconds(25);

    [DataField]
    public TimeSpan MeatCookTime = TimeSpan.FromSeconds(4);

    /// <summary>
    /// The recipe whose cooked cut a human corpse comes off the spit as.
    /// </summary>
    [DataField]
    public ProtoId<CMUYautjaPreyCookRecipePrototype> HumanCorpseRecipe = "CMUYautjaPreyCookHumanMeat";

    [DataField]
    public int HumanCorpseMinCuts = 4;

    [DataField]
    public int HumanCorpseMaxCuts = 5;

    [DataField]
    public ProtoId<CMUYautjaPreyCookRecipePrototype> XenoCorpseRecipe = "CMUYautjaPreyCookXenoMeat";

    [DataField]
    public int XenoCorpseMinCuts = 2;

    [DataField]
    public int XenoCorpseMaxCuts = 3;

    /// <summary>
    /// Holds the corpse while it turns.
    /// </summary>
    [DataField]
    public string BodyContainerId = "cmu-bad-blood-spit-body";

    /// <summary>
    /// What a flask is filled with.
    /// </summary>
    [DataField]
    public string RefillReagent = "Water";
}

[Serializable, NetSerializable]
public sealed partial class BadBloodSpitCookCorpseDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class BadBloodSpitCookMeatDoAfterEvent : SimpleDoAfterEvent;
