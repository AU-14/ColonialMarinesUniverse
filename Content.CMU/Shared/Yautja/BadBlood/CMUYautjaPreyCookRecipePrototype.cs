using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// one row of the Lodge spit's table: what goes on the fire and what comes off it
[Prototype("cmuYautjaPreyCookRecipe")]
public sealed partial class CMUYautjaPreyCookRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId Input;

    [DataField(required: true)]
    public EntProtoId Cooked;

    [DataField]
    public int Count = 1;
}
