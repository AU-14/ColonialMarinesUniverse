using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public const string CrtUiColorGreen = "green";
    public const string CrtUiColorBlue = "blue";
    public const string CrtUiColorOrange = "orange";
    public const string CrtUiColorRed = "red";
    public const string CrtUiColorPurple = "purple";
    public const string CrtUiColorDefault = "#46FF8E";

    public static readonly CVarDef<bool> CrtUiEnabled =
        CVarDef.Create("accessibility.crt_ui_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> CrtUiColor =
        CVarDef.Create("accessibility.crt_ui_color", CrtUiColorDefault, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> ChatEnableRunechatBubbles =
        CVarDef.Create("chat.enable_runechat_bubbles", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> ChatRunechatBubbleScale =
        CVarDef.Create("chat.runechat_bubble_scale", 0.85f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> ConstructionMenuImproved =
        CVarDef.Create("ui.construction_menu_improved", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
