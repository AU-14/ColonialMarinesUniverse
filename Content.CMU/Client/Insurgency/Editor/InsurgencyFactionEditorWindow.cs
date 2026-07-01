using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._AU14.Insurgency.CustomFactions;
using Content.Shared._AU14.Insurgency;
using Content.Shared._AU14.Insurgency.Editor;
using Content.Shared._RMC14.Vendors;
using Content.Shared.AU14.util;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.Insurgency.Editor;

/// <summary>
///     Programmatic editor window for INSFOR Default factions. Built in code rather than XAML to
///     avoid the XAML codegen traps and to keep the many list editors in one readable place.
///
///     Quality-of-life first: nobody types a prototype id. Entities are chosen from a searchable
///     sprite picker, and jobs / ships / faction icons from searchable option lists. Free text is
///     only for genuine free text (titles, descriptions) and numbers (costs, amounts).
///
///     The window edits a working copy of a <see cref="FactionDefinition"/> and sends the whole
///     thing to the server on Save. The server clamps and revalidates before storing, so this UI is
///     only a convenience; it never has the final say on any value.
/// </summary>
public sealed class InsurgencyFactionEditorWindow : DefaultWindow
{
    // A built sub-editor: its root control plus a reader that pulls the current value out of it.
    private sealed record Editor<T>(Control Control, Func<T> Read);

    private readonly Action<int?, bool, FactionDefinition> _onSave;
    private readonly Action<int> _onDelete;
    private readonly Action<int> _onSelect;

    private readonly IPrototypeManager _prototype;
    private readonly InsurgencyCustomFactionStore _customStore = new();
    private readonly BoxContainer _list;
    private readonly BoxContainer _pane;

    private List<EditorFactionEntry> _factions = new();
    private string? _govforPlatoon;

    public InsurgencyFactionEditorWindow(
        Action<int?, bool, FactionDefinition> onSave,
        Action<int> onDelete,
        Action<int> onSelect)
    {
        _onSave = onSave;
        _onDelete = onDelete;
        _onSelect = onSelect;
        _prototype = IoCManager.Resolve<IPrototypeManager>();

        Title = "INSFOR Faction Editor";
        MinSize = new Vector2(980, 660);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, VerticalExpand = true };

        // Left: faction list + New button.
        var left = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinSize = new Vector2(230, 0) };
        left.AddChild(new Label { Text = "Factions", StyleClasses = { "LabelHeading" } });
        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        left.AddChild(new ScrollContainer { Children = { _list }, VerticalExpand = true, HorizontalExpand = true });
        var newButton = new Button { Text = "New faction" };
        newButton.OnPressed += _ => BuildPane(null);
        left.AddChild(newButton);

        // Right: editing pane.
        _pane = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true };

        root.AddChild(left);
        root.AddChild(new ScrollContainer { Children = { _pane }, HorizontalExpand = true, VerticalExpand = true });
        Contents.AddChild(root);
    }

    public void SetState(InsurgencyFactionEditorEuiState state)
    {
        _factions = state.Factions;
        _govforPlatoon = state.GovforPlatoon;
        RebuildList();
    }

    private void RebuildList()
    {
        _list.RemoveAllChildren();
        foreach (var entry in _factions)
        {
            var opposes = _govforPlatoon != null &&
                          entry.Definition.Metadata.OpposedGovforFactions.Any(g => string.Equals(g, _govforPlatoon, StringComparison.OrdinalIgnoreCase));
            var label = entry.Definition.Metadata.Title;
            if (string.IsNullOrWhiteSpace(label))
                label = $"(untitled #{entry.Id})";
            if (opposes)
                label += "  *"; // matches the round's GOVFOR

            var button = new Button { Text = label };
            button.OnPressed += _ => BuildPane(entry);
            _list.AddChild(button);
        }
    }

    // Builds the editing pane for an existing faction, or a blank new one when entry is null.
    private void BuildPane(EditorFactionEntry? entry)
    {
        _pane.RemoveAllChildren();

        var def = entry?.Definition ?? new FactionDefinition();
        var meta = def.Metadata;

        _pane.AddChild(Header(entry == null ? "New faction" : $"Editing: {NonEmpty(meta.Title, "(untitled)")}"));

        var title = LabeledLine("Title", meta.Title);
        var recruited = LabeledLine("Recruited message", meta.RecruitedMessage);
        var description = LabeledLine("Description", meta.Description);
        var roleplay = LabeledLine("Roleplay style", meta.RoleplayText);
        var flag = EntityField("Flag entity", meta.FlagEntity?.Id);
        var icon = IconField("Status icon", meta.StatusIcon?.Id);
        var dollars = LabeledLine("Dollars to points rate", def.Economy.DollarsToPointsRate.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var isDefault = new CheckBox { Text = "Default faction (host-authored, DB stored)", Pressed = entry?.IsDefault ?? true };

        foreach (var c in new Control[] { title.Control, recruited.Control, description.Control, roleplay.Control, flag.Control, icon.Control, dollars.Control, isDefault })
            _pane.AddChild(c);

        var opposed = PlatoonListEditor("Opposed GOVFOR factions", meta.OpposedGovforFactions);
        _pane.AddChild(opposed.Control);

        // The well-known CLF machines are ticked on/off here; everything else is a free entity list.
        var machines = DefaultMachinesEditor(def.CellKit.PlaceableEntities.Select(p => p.Id));
        _pane.AddChild(machines.Control);

        var placeables = EntityListEditor("Cell kit: other placeable entities",
            def.CellKit.PlaceableEntities.Select(p => p.Id).Where(id => !IsDefaultMachine(id)));
        _pane.AddChild(placeables.Control);

        // What the analyzer machine accepts for points, and at what ratio. Empty = plain dollars.
        var submissions = PointsSubmissionListEditor(def.Economy.PointsSubmissions);
        _pane.AddChild(submissions.Control);

        var vendors = VendorListEditor(def.CellKit.VendorDefinitions);
        _pane.AddChild(vendors.Control);

        var loadouts = RoleLoadoutListEditor(def.RoleLoadouts);
        _pane.AddChild(loadouts.Control);

        // Action buttons.
        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };

        // Reads the whole pane back into a fresh definition. Shared by the server save (Default) and
        // the local save (Custom) so both persist exactly what the editor shows.
        FactionDefinition BuildDef() => new()
        {
            Metadata =
            {
                Title = title.Read(),
                Description = description.Read(),
                RoleplayText = roleplay.Read(),
                RecruitedMessage = recruited.Read(),
                FlagEntity = ToEntProtoIdOrNull(flag.Read()),
                StatusIcon = ToIconOrNull(icon.Read()),
                OpposedGovforFactions = opposed.Read(),
            },
            Economy =
            {
                DollarsToPointsRate = ParseFloat(dollars.Read(), FactionDefinition.DefaultDollarsToPointsRate),
                PointsSubmissions = submissions.Read(),
            },
            CellKit =
            {
                // Merge the ticked machines with the free placeables, machines first, no duplicates.
                PlaceableEntities = machines.Read()
                    .Concat(placeables.Read().Where(s => !IsDefaultMachine(s)))
                    .Distinct()
                    .Select(s => new EntProtoId(s))
                    .ToList(),
                VendorDefinitions = vendors.Read(),
            },
            RoleLoadouts = loadouts.Read(),
        };

        var save = new Button { Text = "Save (server / Default)" };
        save.OnPressed += _ => _onSave(entry?.Id, isDefault.Pressed, BuildDef());
        buttons.AddChild(save);

        // Local save: writes the definition to this machine as a Custom faction, so it shows up in the
        // leader's Custom list. Never touches the server DB.
        var saveLocal = new Button { Text = "Save as local Custom" };
        saveLocal.OnPressed += _ =>
        {
            var def = BuildDef();
            _customStore.Save(NonEmpty(def.Metadata.Title, "faction"), def);
        };
        buttons.AddChild(saveLocal);

        if (entry != null)
        {
            var select = new Button { Text = "Apply for round" };
            select.OnPressed += _ => _onSelect(entry.Id);
            buttons.AddChild(select);

            var delete = new Button { Text = "Delete" };
            delete.OnPressed += _ => _onDelete(entry.Id);
            buttons.AddChild(delete);
        }

        _pane.AddChild(buttons);
    }

    // ----- pickers --------------------------------------------------------------

    private void OpenEntityPicker(Action<string> onPick)
    {
        var window = new InsurgencyEntityPickerWindow();
        window.OnEntitySelected += onPick;
        window.OpenCentered();
    }

    private void OpenProtoPicker(string title, List<(string Id, string Display)> options, Action<string> onPick)
    {
        var window = new InsurgencyProtoPickerWindow(title, options);
        window.OnSelected += onPick;
        window.OpenCentered();
    }

    private List<(string Id, string Display)> JobOptions() => _prototype.EnumeratePrototypes<JobPrototype>()
        .Select(j => (j.ID, $"{j.LocalizedName}  [{j.ID}]"))
        .OrderBy(x => x.Item2, StringComparer.InvariantCultureIgnoreCase)
        .ToList();

    // GOVFOR "factions" are Platoons (USMC, TWE RMC, UPP, and so on). A faction author picks which
    // of these platoons their cell opposes; the round's selected GOVFOR platoon drives the match.
    private List<(string Id, string Display)> PlatoonOptions() => _prototype.EnumeratePrototypes<PlatoonPrototype>()
        .Select(p => (p.ID, string.IsNullOrWhiteSpace(p.Name) ? p.ID : $"{p.Name}  [{p.ID}]"))
        .OrderBy(x => x.Item2, StringComparer.InvariantCultureIgnoreCase)
        .ToList();

    private List<(string Id, string Display)> IconOptions() => _prototype.EnumeratePrototypes<FactionIconPrototype>()
        .Select(i => (i.ID, i.ID))
        .OrderBy(x => x.Item1, StringComparer.InvariantCultureIgnoreCase)
        .ToList();

    // A single-value field backed by a picker: a button showing the current id (or "Choose..."),
    // plus a Clear. Clicking the button opens the given picker.
    private Editor<string> PickerField(string label, string? current, Action<Action<string>> openPicker)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        box.AddChild(new Label { Text = label, MinSize = new Vector2(190, 0) });

        var selected = current ?? string.Empty;
        var button = new Button { Text = PickerText(selected), HorizontalExpand = true };
        button.OnPressed += _ => openPicker(id =>
        {
            selected = id;
            button.Text = PickerText(id);
        });

        var clear = new Button { Text = "Clear" };
        clear.OnPressed += _ =>
        {
            selected = string.Empty;
            button.Text = PickerText(string.Empty);
        };

        box.AddChild(button);
        box.AddChild(clear);
        return new Editor<string>(box, () => selected);
    }

    private Editor<string> EntityField(string label, string? current) =>
        PickerField(label, current, OpenEntityPicker);

    private Editor<string> JobField(string label, string? current) =>
        PickerField(label, current, onPick => OpenProtoPicker(Loc.GetString("insfor-picker-job-title"), JobOptions(), onPick));

    private Editor<string> IconField(string label, string? current) =>
        PickerField(label, current, onPick => OpenProtoPicker(Loc.GetString("insfor-picker-icon-title"), IconOptions(), onPick));

    // A list of ids, each row picked from a picker. The Add button opens the picker and adds the
    // chosen id as a new row.
    private Editor<List<string>> PickerListEditor(string label, IEnumerable<string> initial, Action<Action<string>> openPicker)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        box.AddChild(Header(label));
        var rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var readers = new List<Func<string>>();

        void AddRow(string value)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            var current = value ?? string.Empty;
            var button = new Button { Text = PickerText(current), HorizontalExpand = true };
            button.OnPressed += _ => openPicker(id =>
            {
                current = id;
                button.Text = PickerText(id);
            });

            var remove = new Button { Text = "X" };
            Func<string> reader = () => current;
            remove.OnPressed += _ =>
            {
                rows.RemoveChild(row);
                readers.Remove(reader);
            };

            row.AddChild(button);
            row.AddChild(remove);
            rows.AddChild(row);
            readers.Add(reader);
        }

        foreach (var s in initial)
            AddRow(s);

        var add = new Button { Text = "+ Add" };
        add.OnPressed += _ => openPicker(AddRow);

        box.AddChild(rows);
        box.AddChild(add);
        return new Editor<List<string>>(box, () => readers.Select(r => r()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList());
    }

    private Editor<List<string>> EntityListEditor(string label, IEnumerable<string> initial) =>
        PickerListEditor(label, initial, OpenEntityPicker);

    private Editor<List<string>> PlatoonListEditor(string label, IEnumerable<string> initial) =>
        PickerListEditor(label, initial, onPick => OpenProtoPicker(Loc.GetString("insfor-picker-platoon-title"), PlatoonOptions(), onPick));

    // Submittable-for-points rows: each is an entity (picked, never typed) plus how many of it make one
    // point. Leaving the list empty keeps the analyzer's plain-dollars behavior. Add / change / remove
    // are all here so no value needs hand-editing.
    private Editor<List<PointsSubmissionEntry>> PointsSubmissionListEditor(IEnumerable<PointsSubmissionEntry> initial)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        box.AddChild(Header("Analyzer: submittable for points (empty = plain dollars)"));
        var rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var readers = new List<Func<PointsSubmissionEntry>>();

        void AddRow(PointsSubmissionEntry entry)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };

            var current = entry.Entity.Id;
            var button = new Button { Text = PickerText(current), HorizontalExpand = true };
            button.OnPressed += _ => OpenEntityPicker(id =>
            {
                current = id;
                button.Text = PickerText(id);
            });

            var amount = new LineEdit { Text = entry.AmountPerPoint.ToString(), MinSize = new Vector2(70, 0), PlaceHolder = "per point" };

            var remove = new Button { Text = "X" };
            Func<PointsSubmissionEntry> reader = () => new PointsSubmissionEntry
            {
                Entity = new EntProtoId(current),
                // At least one so a submission can never mint infinite points.
                AmountPerPoint = Math.Max(1, ParseIntOrNull(amount.Text) ?? 15),
            };
            remove.OnPressed += _ =>
            {
                rows.RemoveChild(row);
                readers.Remove(reader);
            };

            row.AddChild(button);
            row.AddChild(new Label { Text = "  per point ", VerticalAlignment = VAlignment.Center });
            row.AddChild(amount);
            row.AddChild(remove);
            rows.AddChild(row);
            readers.Add(reader);
        }

        foreach (var e in initial)
            AddRow(e);

        var add = new Button { Text = "+ Add submittable item" };
        add.OnPressed += _ => AddRow(new PointsSubmissionEntry());

        box.AddChild(rows);
        box.AddChild(add);
        // Drop rows with no entity chosen so a blank picker never becomes a broken entry.
        return new Editor<List<PointsSubmissionEntry>>(box, () => readers.Select(r => r())
            .Where(e => !string.IsNullOrWhiteSpace(e.Entity.Id))
            .ToList());
    }

    // ----- nested structured editors --------------------------------------------

    private Editor<List<FactionVendorDefinition>> VendorListEditor(IEnumerable<FactionVendorDefinition> initial)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        box.AddChild(Header("Cell kit: vendors"));
        var rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var readers = new List<Func<FactionVendorDefinition>>();

        void AddVendor(FactionVendorDefinition vendor)
        {
            var panel = new PanelContainer();
            var inner = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            var name = LabeledLine("Vendor name", vendor.Name);
            var model = EntityField("Base model", vendor.BaseModel.Id);
            var invulnerable = new CheckBox { Text = "Invulnerable (base entity won't break / change on damage)", Pressed = vendor.Invulnerable };
            var intelPoints = new CheckBox { Text = "Uses cell intel points (money at the intel computer stocks this vendor)", Pressed = vendor.UsesIntelPoints };
            var sections = SectionListEditor(vendor.Sections);

            var remove = new Button { Text = "Remove vendor" };
            Func<FactionVendorDefinition> reader = () => new FactionVendorDefinition
            {
                Name = name.Read(),
                BaseModel = new EntProtoId(model.Read()),
                Sections = sections.Read(),
                Invulnerable = invulnerable.Pressed,
                UsesIntelPoints = intelPoints.Pressed,
            };
            remove.OnPressed += _ =>
            {
                rows.RemoveChild(panel);
                readers.Remove(reader);
            };

            inner.AddChild(name.Control);
            inner.AddChild(model.Control);
            inner.AddChild(invulnerable);
            inner.AddChild(intelPoints);
            inner.AddChild(sections.Control);
            inner.AddChild(remove);
            panel.AddChild(inner);
            rows.AddChild(panel);
            readers.Add(reader);
        }

        foreach (var v in initial)
            AddVendor(v);

        var add = new Button { Text = "+ Add vendor" };
        add.OnPressed += _ => AddVendor(new FactionVendorDefinition());

        box.AddChild(rows);
        box.AddChild(add);
        return new Editor<List<FactionVendorDefinition>>(box, () => readers.Select(r => r()).ToList());
    }

    private Editor<List<CMVendorSection>> SectionListEditor(IEnumerable<CMVendorSection> initial)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        box.AddChild(new Label { Text = "Sections" });
        var rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var readers = new List<Func<CMVendorSection>>();

        void AddSection(CMVendorSection section)
        {
            var panel = new PanelContainer();
            var inner = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            var name = LabeledLine("Section name", section.Name);

            // Category take-limits (independent of price/stock): how many items one player may take from
            // this category, and how many all players together may take. Blank means unlimited.
            var perPlayer = new LineEdit { Text = section.Choices?.Amount.ToString() ?? string.Empty, MinSize = new Vector2(70, 0), PlaceHolder = "per-player" };
            var global = new LineEdit { Text = section.SharedJOLimit?.ToString() ?? string.Empty, MinSize = new Vector2(70, 0), PlaceHolder = "global" };
            var limitsRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            limitsRow.AddChild(new Label { Text = "Category limit  ", VerticalAlignment = VAlignment.Center });
            limitsRow.AddChild(perPlayer);
            limitsRow.AddChild(global);

            var entries = EntryListEditor(section.Entries);

            var remove = new Button { Text = "Remove section" };
            Func<CMVendorSection> reader = () =>
            {
                var sectionName = name.Read();
                var perPlayerLimit = ParseIntOrNull(perPlayer.Text);
                return new CMVendorSection
                {
                    Name = sectionName,
                    Entries = entries.Read(),
                    // A per-player limit is a "choice group" keyed to this section's name; the global cap
                    // reuses the shared section limit the vend logic already enforces.
                    Choices = perPlayerLimit is { } p ? (sectionName, p) : null,
                    SharedJOLimit = ParseIntOrNull(global.Text),
                };
            };
            remove.OnPressed += _ =>
            {
                rows.RemoveChild(panel);
                readers.Remove(reader);
            };

            inner.AddChild(name.Control);
            inner.AddChild(limitsRow);
            inner.AddChild(entries.Control);
            inner.AddChild(remove);
            panel.AddChild(inner);
            rows.AddChild(panel);
            readers.Add(reader);
        }

        foreach (var s in initial)
            AddSection(s);

        var add = new Button { Text = "+ Add section" };
        add.OnPressed += _ => AddSection(new CMVendorSection());

        box.AddChild(rows);
        box.AddChild(add);
        return new Editor<List<CMVendorSection>>(box, () => readers.Select(r => r()).ToList());
    }

    private Editor<List<CMVendorEntry>> EntryListEditor(IEnumerable<CMVendorEntry> initial)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        box.AddChild(new Label { Text = "Items (pick entity / points / amount / max)" });
        var rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var readers = new List<Func<CMVendorEntry>>();

        void AddEntry(CMVendorEntry entry)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };

            var selectedId = entry.Id.Id ?? string.Empty;
            var idButton = new Button { Text = PickerText(selectedId), HorizontalExpand = true };
            idButton.OnPressed += _ => OpenEntityPicker(id =>
            {
                selectedId = id;
                idButton.Text = PickerText(id);
            });

            var points = new LineEdit { Text = entry.Points?.ToString() ?? string.Empty, MinSize = new Vector2(70, 0), PlaceHolder = "points" };
            var amount = new LineEdit { Text = entry.Amount?.ToString() ?? string.Empty, MinSize = new Vector2(70, 0), PlaceHolder = "amount" };
            var max = new LineEdit { Text = entry.Max?.ToString() ?? string.Empty, MinSize = new Vector2(70, 0), PlaceHolder = "max" };
            var remove = new Button { Text = "X" };

            Func<CMVendorEntry> reader = () => new CMVendorEntry
            {
                Id = new EntProtoId(selectedId),
                Points = ParseIntOrNull(points.Text),
                Amount = ParseIntOrNull(amount.Text),
                Max = ParseIntOrNull(max.Text),
            };
            remove.OnPressed += _ =>
            {
                rows.RemoveChild(row);
                readers.Remove(reader);
            };

            row.AddChild(idButton);
            row.AddChild(points);
            row.AddChild(amount);
            row.AddChild(max);
            row.AddChild(remove);
            rows.AddChild(row);
            readers.Add(reader);
        }

        foreach (var e in initial)
            AddEntry(e);

        var add = new Button { Text = "+ Add item" };
        add.OnPressed += _ => AddEntry(new CMVendorEntry());

        box.AddChild(rows);
        box.AddChild(add);
        // Only keep entries that actually named an item.
        return new Editor<List<CMVendorEntry>>(box, () => readers.Select(r => r()).Where(e => !string.IsNullOrWhiteSpace(e.Id.Id)).ToList());
    }

    private Editor<List<FactionRoleLoadout>> RoleLoadoutListEditor(IEnumerable<FactionRoleLoadout> initial)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        box.AddChild(Header("Role loadouts (A Package contents)"));
        var rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var readers = new List<Func<FactionRoleLoadout>>();

        void AddLoadout(FactionRoleLoadout loadout)
        {
            var panel = new PanelContainer();
            var inner = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            var role = JobField("Role (job)", loadout.Role);
            var contents = EntityListEditor("Contents", loadout.Contents.Select(c => c.Id));

            var remove = new Button { Text = "Remove loadout" };
            Func<FactionRoleLoadout> reader = () => new FactionRoleLoadout
            {
                Role = role.Read(),
                Contents = contents.Read().Select(s => new EntProtoId(s)).ToList(),
            };
            remove.OnPressed += _ =>
            {
                rows.RemoveChild(panel);
                readers.Remove(reader);
            };

            inner.AddChild(role.Control);
            inner.AddChild(contents.Control);
            inner.AddChild(remove);
            panel.AddChild(inner);
            rows.AddChild(panel);
            readers.Add(reader);
        }

        foreach (var l in initial)
            AddLoadout(l);

        var add = new Button { Text = "+ Add loadout" };
        add.OnPressed += _ => AddLoadout(new FactionRoleLoadout());

        box.AddChild(rows);
        box.AddChild(add);
        return new Editor<List<FactionRoleLoadout>>(box, () => readers.Select(r => r()).Where(l => !string.IsNullOrWhiteSpace(l.Role)).ToList());
    }

    // ----- default cell-kit machines --------------------------------------------

    // The machines the original heavy CLF cell kit deployed. Ticking one adds it to the faction's
    // placeables; their in-game wiring (money at the intel computer -> cell points -> vendors) is the
    // existing CLF behavior, so no extra linking is needed here.
    private static readonly (string Label, string Proto)[] DefaultMachines =
    {
        ("Analyzer machine", "AU14AnalyzerMachineCLF"),
        ("CLF intel computer", "RMCComputerIntelCLF"),
        ("CLF objectives console", "ComputerObjectivesCLF"),
        ("CLF tech tree console", "RMCTechTreeConsoleCLF"),
        ("Fax machine", "CMFaxCLF"),
    };

    private static bool IsDefaultMachine(string proto) =>
        DefaultMachines.Any(m => string.Equals(m.Proto, proto, StringComparison.OrdinalIgnoreCase));

    private Editor<List<string>> DefaultMachinesEditor(IEnumerable<string> currentPlaceables)
    {
        var present = currentPlaceables.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        box.AddChild(Header("Default cell-kit machines"));

        var checks = new List<(string Proto, CheckBox Box)>();
        foreach (var (label, proto) in DefaultMachines)
        {
            var cb = new CheckBox { Text = label, Pressed = present.Contains(proto) };
            box.AddChild(cb);
            checks.Add((proto, cb));
        }

        return new Editor<List<string>>(box, () => checks.Where(c => c.Box.Pressed).Select(c => c.Proto).ToList());
    }

    // ----- small helpers --------------------------------------------------------

    private static Label Header(string text) => new() { Text = text, StyleClasses = { "LabelHeading" } };

    private static Editor<string> LabeledLine(string label, string? value)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        box.AddChild(new Label { Text = label, MinSize = new Vector2(190, 0) });
        var line = new LineEdit { Text = value ?? string.Empty, HorizontalExpand = true };
        box.AddChild(line);
        return new Editor<string>(box, () => line.Text.Trim());
    }

    private static string PickerText(string? id) => string.IsNullOrEmpty(id) ? "Choose..." : id;

    private static string NonEmpty(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static EntProtoId? ToEntProtoIdOrNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : new EntProtoId?(new EntProtoId(value));

    private static ProtoId<FactionIconPrototype>? ToIconOrNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : new ProtoId<FactionIconPrototype>?(new ProtoId<FactionIconPrototype>(value));

    private static float ParseFloat(string value, float fallback) =>
        float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : fallback;

    private static int? ParseIntOrNull(string value) =>
        int.TryParse(value?.Trim(), out var i) ? i : null;
}
