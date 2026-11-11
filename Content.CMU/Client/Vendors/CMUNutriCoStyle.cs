using Content.Client.CMU14.Telephone;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Vendors;

/// <summary>
///     The look of NutriCo's machines, taken from their sprites: a grey steel cabinet with a dark trim, the orange and
///     white NutriCo stripe, a column of green status lights and a dark menu display.
/// </summary>
public static class CMUNutriCoStyle
{
    public static readonly Color Steel = Color.FromHex("#7e8386");
    public static readonly Color SteelLight = Color.FromHex("#a0a0a0");
    public static readonly Color Trim = Color.FromHex("#383838");
    public static readonly Color TrimDark = Color.FromHex("#232323");
    public static readonly Color Display = Color.FromHex("#2c2828");
    public static readonly Color DisplayRow = Color.FromHex("#3a3535");
    public static readonly Color Phosphor = Color.FromHex("#cfe8d2");
    public static readonly Color PhosphorDim = Color.FromHex("#7f9683");
    public static readonly Color Orange = Color.FromHex("#f39b1d");
    public static readonly Color White = Color.FromHex("#efefef");
    public static readonly Color Led = Color.FromHex("#2fbf3a");
    public static readonly Color LedOff = Color.FromHex("#1d3a1f");
    public static readonly Color Ink = Color.FromHex("#1e1e1e");
    public static readonly Color KeyFace = Color.FromHex("#595757");
    public static readonly Color KeyFaceLit = Color.FromHex("#6e6a5e");
    public static readonly Color Warning = Color.FromHex("#e8573c");

    public static Font Font(string path, int size) => CMUPhoneStyle.Font(path, size);

    /// <summary>The cabinet's header: the NutriCo stripe, the machine's name, and the status lights.</summary>
    public static Control Header(string model, int lights, out List<PanelContainer> leds)
    {
        var plate = CMUPhoneStyle.Panel(Steel, Trim, 2, 10, 8);
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };

        var brand = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2, HorizontalExpand = true };
        var stripes = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 1 };
        stripes.AddChild(new PanelContainer { MinSize = new System.Numerics.Vector2(70, 5), HorizontalAlignment = Control.HAlignment.Left, PanelOverride = CMUPhoneStyle.Box(Orange) });
        stripes.AddChild(new PanelContainer { MinSize = new System.Numerics.Vector2(70, 3), HorizontalAlignment = Control.HAlignment.Left, PanelOverride = CMUPhoneStyle.Box(White) });
        brand.AddChild(stripes);
        var names = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        names.AddChild(CMUPhoneStyle.Text("NutriCo", Font(CMUPhoneStyle.SansBold, 18), Ink));
        var modelLabel = CMUPhoneStyle.Text(model, Font(CMUPhoneStyle.Sans, 18), White);
        names.AddChild(modelLabel);
        brand.AddChild(names);
        row.AddChild(brand);

        var lightPanel = CMUPhoneStyle.Panel(Trim, TrimDark, 1, 6, 4);
        lightPanel.VerticalAlignment = Control.VAlignment.Center;
        var lightColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3 };
        leds = new List<PanelContainer>();
        for (var i = 0; i < lights; i++)
        {
            var lightRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            lightRow.AddChild(new PanelContainer { MinSize = new System.Numerics.Vector2(12, 3), PanelOverride = CMUPhoneStyle.Box(SteelLight) });
            var led = new PanelContainer { MinSize = new System.Numerics.Vector2(6, 3), PanelOverride = CMUPhoneStyle.Box(Led) };
            lightRow.AddChild(led);
            leds.Add(led);
            lightColumn.AddChild(lightRow);
        }

        lightPanel.AddChild(lightColumn);
        row.AddChild(lightPanel);
        plate.AddChild(row);
        return plate;
    }

    public static void SetLed(PanelContainer led, bool on)
    {
        led.PanelOverride = CMUPhoneStyle.Box(on ? Led : LedOff);
    }

    /// <summary>The vent grille along the bottom of the cabinet.</summary>
    public static Control Footer()
    {
        var footer = CMUPhoneStyle.Panel(Trim, TrimDark, 1, 10, 4);
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 3, HorizontalAlignment = Control.HAlignment.Center };
        for (var i = 0; i < 24; i++)
            row.AddChild(new PanelContainer { MinSize = new System.Numerics.Vector2(3, 10), PanelOverride = CMUPhoneStyle.Box(TrimDark) });
        footer.AddChild(row);
        return footer;
    }

    /// <summary>A key on the cabinet, grey with a dark edge.</summary>
    public static Button Key(string text, int minWidth = 0, bool lit = false)
    {
        return CMUPhoneStyle.Key(text, Font(CMUPhoneStyle.SansBold, 11), lit ? KeyFaceLit : KeyFace, TrimDark,
            lit ? Orange : White, minWidth);
    }
}
