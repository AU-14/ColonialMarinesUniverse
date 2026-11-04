using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> CharacterDescription =
        CVarDef.Create("cmu.character_description", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> MaxShortExamineLength =
        CVarDef.Create("cmu.character_description_short_length", 100, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> MaxCharacterDescriptionFieldLength =
        CVarDef.Create("cmu.character_description_field_length", 512, CVar.SERVER | CVar.REPLICATED);
}
