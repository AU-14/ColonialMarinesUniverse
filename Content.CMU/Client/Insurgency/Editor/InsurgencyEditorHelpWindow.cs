using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._AU14.Insurgency.Editor;

/// <summary>
///     A plain, scrollable explanation of every field in the INSFOR faction editor, so a host can build a
///     faction without reading code. Opened by the Help button at the top of the editor. Kept as simple
///     bold-heading + paragraph pairs; no markup tricks, so it stays readable in the terminal-style UI.
/// </summary>
public sealed class InsurgencyEditorHelpWindow : DefaultWindow
{
    public InsurgencyEditorHelpWindow()
    {
        Title = "INSFOR Faction Editor - Help";
        MinSize = new Vector2(640, 620);

        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true, HScrollEnabled = false };
        scroll.AddChild(body);
        Contents.AddChild(scroll);
        InsforUiStyle.Apply(this);

        Intro(body,
            "An INSFOR faction is one insurgent cell the CLF leader can pick after spawning. You fill in who they are, " +
            "what money buys them points, what their leader's Heavy Cell Kit can drop, and what each role gets in their " +
            "\"A Package\". Nothing here needs a prototype id typed by hand: every entity, job, and icon is chosen from a " +
            "searchable picker. The server re-checks and clamps everything you save, so you cannot break the round with a bad value.");

        Section(body, "The faction list (left) and the  *  mark",
            "The left column lists every saved faction plus the built-in vanilla CLF at the top. A faction shows a  *  next " +
            "to its name when it is set to oppose the GOVFOR side the current round rolled, i.e. it is a valid pick this round. " +
            "No star just means it does not target this round's GOVFOR; it is still fine to edit. Click a faction to edit it, " +
            "or New faction to start blank.");

        Section(body, "Identity",
            "Title: the faction's name, shown in the pick list and the reveal popup.\n" +
            "Recruited message: the briefing a freshly recruited member reads (for example via the tattoo gun). Blank uses the default CLF line.\n" +
            "Description / Roleplay style: shown in the antag briefing and the reveal popup so members know who they are and how they are meant to play.\n" +
            "Flag entity: an in-world flag prop, picked from the catalog (optional).\n" +
            "Status icon: the faction membership icon members show to each other, picked from the icon list.");

        Section(body, "Default faction (checkbox)",
            "On: this faction is host-authored and saved in the server database; it is offered to leaders whose GOVFOR matches " +
            "the Opposed list below. Off: it is a personal/Custom faction. The Save buttons at the bottom control where it is written.");

        Section(body, "Opposed GOVFOR factions",
            "The GOVFOR platoons (USMC, TWE RMC, UPP, and so on) this faction is allowed to oppose. If the round's GOVFOR is in " +
            "this list, the faction is offered to the leader and gets the  *  in the list. Add as many as you like.");

        Section(body, "Economy - dollars to points",
            "Dollars to points rate: how intel dollars convert to the cell's vendor points.\n" +
            "Also accept plain dollars: when ticked, cash still converts at the analyzer even if you add custom submittables below. " +
            "Untick it for a faction whose economy should ignore money entirely.");

        Section(body, "Analyzer - submittable for points",
            "What the analyzer machine accepts and turns into cell points, beyond plain cash. Each row is an item (picked, never typed) " +
            "and a ratio with two modes:\n" +
            "  - items per point: it takes that many of the item to make one point (good for cheap goods).\n" +
            "  - points per item: one item is worth that many points (good for valuable goods).\n" +
            "Leave the list empty to keep the plain-dollars behavior. The value is always at least 1 so a submission can never mint free points.");

        Section(body, "Default cell-kit machines",
            "Tick the well-known CLF machines (analyzer, intel computer, objectives console, tech tree console, fax) you want the leader's " +
            "Heavy Cell Kit to be able to place. Their money-to-points wiring is the normal CLF behavior; no extra setup is needed.");

        Section(body, "Cell kit - other placeable entities",
            "Any additional single entities the leader can free-place from the Heavy Cell Kit (lamps, barricades, props, and so on). " +
            "Each is picked from the entity picker.");

        Section(body, "Cell kit - vendors",
            "Each vendor the leader can deploy from the kit. Per vendor:\n" +
            "  - Vendor name: the name shown on the deployed vendor and in the kit list.\n" +
            "  - Base model: an existing vendor entity used only for its sprite/collision; its arsenal is replaced by your sections.\n" +
            "  - Wrenchable: can be wrenched down and moved after placing.\n" +
            "  - Invulnerable: the placed vendor will not break or change on damage.\n" +
            "  - Uses cell intel points: items are paid from the cell's shared intel points (money at the intel computer stocks it) " +
            "instead of the buyer's own points.\n" +
            "  - Use base model's own arsenal: ignore the sections below and keep the base entity's built-in stock. Only for reusing a " +
            "fully-made vendor (like the CLF requisitions rack); leave off for a normal custom vendor.");

        Section(body, "Vendor sections and items",
            "A vendor is split into sections (categories). Per section:\n" +
            "  - Section name.\n" +
            "  - Category limit: two optional caps - how many one player may take from this category, and how many all players together may.\n" +
            "Inside a section, each item row is:\n" +
            "  - the entity (picked),\n" +
            "  - points: its cost (0 = free),\n" +
            "  - amount: how many are in stock,\n" +
            "  - max: the ceiling it restocks to.\n" +
            "Leave points blank to make an item free-by-stock only.");

        Section(body, "Role loadouts - A Package",
            "Because the faction is chosen after players spawn, each role's kit is delivered afterwards as an \"A Package\" box. " +
            "Add a row per role: pick the Role (job) and the Contents (entities) it hands out.");

        Section(body, "Saving and applying",
            "Save (server / Default): writes it to the server database as a host faction.\n" +
            "Save as local Custom: writes it to your machine only, so it shows up in the leader's Custom list.\n" +
            "Apply for round: immediately applies this faction to the current round's cell.\n" +
            "Delete: removes a saved faction (the built-in vanilla CLF cannot be deleted).");
    }

    private static void Intro(BoxContainer body, string text)
    {
        body.AddChild(new RichTextLabel { Margin = new Thickness(10, 8) }.SetMessageWrapped(text));
    }

    private static void Section(BoxContainer body, string heading, string text)
    {
        body.AddChild(new Label { Text = heading, StyleClasses = { "LabelHeading" }, Margin = new Thickness(10, 10, 10, 2) });
        body.AddChild(new RichTextLabel { Margin = new Thickness(10, 0, 10, 6) }.SetMessageWrapped(text));
    }
}

file static class HelpLabelExtensions
{
    // Small helper so the long help paragraphs word-wrap instead of running off the window.
    public static RichTextLabel SetMessageWrapped(this RichTextLabel label, string text)
    {
        label.SetMessage(text);
        return label;
    }
}
