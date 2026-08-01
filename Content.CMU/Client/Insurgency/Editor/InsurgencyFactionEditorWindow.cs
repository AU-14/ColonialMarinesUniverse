using System;
using System.Collections.Generic;
using System.IO;
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
using Robust.Shared.Utility;

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
    private readonly Action _onExportTemplate;
    private readonly Action<int> _onExportFaction;
    private readonly Action<byte[]> _onImportFaction;

    private readonly IPrototypeManager _prototype;
    private readonly IFileDialogManager _fileDialog = IoCManager.Resolve<IFileDialogManager>();
    private readonly InsurgencyCustomFactionStore _customStore = new();
    private readonly BoxContainer _list;
    private readonly BoxContainer _pane;

    private List<EditorFactionEntry> _factions = new();
    private string? _govforPlatoon;
    private InsurgencyEditorScope _scope = InsurgencyEditorScope.Default;

    public InsurgencyFactionEditorWindow(
        Action<int?, bool, FactionDefinition> onSave,
        Action<int> onDelete,
        Action<int> onSelect,
        Action onExportTemplate,
        Action<int> onExportFaction,
        Action<byte[]> onImportFaction)
    {
        _onSave = onSave;
        _onDelete = onDelete;
        _onSelect = onSelect;
        _onExportTemplate = onExportTemplate;
        _onExportFaction = onExportFaction;
        _onImportFaction = onImportFaction;
        _prototype = IoCManager.Resolve<IPrototypeManager>();

        Title = "INSFOR Faction Editor";
        MinSize = new Vector2(980, 660);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, VerticalExpand = true };

        // Left: help button, faction list + New button.
        var left = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, MinSize = new Vector2(230, 0) };
        var help = new Button { Text = "Help - what do these fields mean?" };
        help.OnPressed += _ => new InsurgencyEditorHelpWindow().OpenCentered();
        left.AddChild(help);

        // The custom flag pipeline (template export + PNG import) was cancelled as too logically
        // complicated; its code was removed entirely (see git history to resurrect it).
        left.AddChild(new Label { Text = "Factions", StyleClasses = { "LabelHeading" } });
        _list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };
        left.AddChild(new ScrollContainer { Children = { _list }, VerticalExpand = true, HorizontalExpand = true });
        var newButton = new Button { Text = "New faction" };
        newButton.OnPressed += _ => BuildPane(null);
        left.AddChild(newButton);

        // Spreadsheet workflow: export a ready-to-fill blank sheet to hand to a player, and import the
        // filled sheet they send back. No setup or macros - the server bakes the dropdowns and reads the
        // file, and validates the import like any untrusted payload. (Per-faction export is in the pane.)
        var exportTemplate = new Button { Text = "Export blank sheet (for a player)" };
        exportTemplate.OnPressed += _ => _onExportTemplate();
        left.AddChild(exportTemplate);

        var importFaction = new Button { Text = "Import filled sheet" };
        importFaction.OnPressed += _ => ImportFactionFromFile();
        left.AddChild(importFaction);

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
        _scope = state.Scope;
        Title = _scope == InsurgencyEditorScope.Custom ? "INSFOR Custom Faction Editor" : "INSFOR Faction Editor";
        RebuildList();
        InsforUiStyle.Apply(this); // improved-construction-menu look; re-run safe
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

        // These four are free-form prose players read, so they get roomy multi-line boxes rather than a
        // single cramped line. Nudge MultilineHeight below to change how tall the boxes start.
        var title = LabeledMultiline("Title", meta.Title);
        var recruited = LabeledMultiline("Recruited message", meta.RecruitedMessage);
        var description = LabeledMultiline("Description", meta.Description);
        var roleplay = LabeledMultiline("Roleplay style", meta.RoleplayText);
        // Flag: pick any catalog entity to use as the faction flag. Its sprite shows on the leader's
        // faction-selection rows and in the member reveal popup. This is the catalog picker only - the old
        // live upload / import-export pipeline stays retired.
        var flag = EntityField("Flag entity", meta.FlagEntity?.Id);
        var icon = IconField("Status icon", meta.StatusIcon?.Id);
        // Icon for members recruited in-round (tattooed) who have no per-job icon - without it they keep the
        // default CLF icon. Falls back to the Status icon above when left empty.
        var recruitIcon = IconField("Recruited-member icon (no per-job icon)", meta.RecruitStatusIcon?.Id);
        var jobIcons = JobIconListEditor(meta.JobStatusIcons);
        var dollars = LabeledLine("Dollars to points rate", def.Economy.DollarsToPointsRate.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // The Custom editor can only author Custom factions, so the toggle disappears and stays off.
        var isDefault = new CheckBox
        {
            Text = "Default faction (host-authored, DB stored)",
            Pressed = _scope == InsurgencyEditorScope.Default && (entry?.IsDefault ?? true),
            Visible = _scope == InsurgencyEditorScope.Default,
        };

        var opposed = PlatoonListEditor("Opposed GOVFOR factions", meta.OpposedGovforFactions);
        // The well-known CLF machines are ticked on/off here; everything else is a free entity list.
        var machines = DefaultMachinesEditor(def.CellKit.PlaceableEntities.Select(p => p.Id));
        var placeables = EntityListEditor("Cell kit: other placeable entities",
            def.CellKit.PlaceableEntities.Select(p => p.Id).Where(id => !IsDefaultMachine(id)));
        // What the analyzer machine accepts for points, and at what ratio. Empty = plain dollars.
        var submissions = PointsSubmissionListEditor(def.Economy.PointsSubmissions);
        // Dollars stay valid alongside any custom submittables unless the author turns them off.
        var includeDollars = new CheckBox { Text = "Also accept plain dollars for points", Pressed = def.Economy.IncludeDollars };
        var vendors = VendorListEditor(def.CellKit.VendorDefinitions);
        var loadouts = RoleLoadoutListEditor(def.RoleLoadouts);

        // Group the fields into top tabs so the editor is not one long scroll: each category is its own page.
        // Explicit height: the pane lives inside a ScrollContainer (infinite vertical), so VerticalExpand
        // alone would collapse the tabs. Each tab page scrolls its own overflow within this height.
        var tabs = new TabContainer { HorizontalExpand = true, VerticalExpand = true, MinSize = new Vector2(0, 560) };
        var pages = new (string Title, Control[] Controls)[]
        {
            ("Faction Info", new Control[] { title.Control, recruited.Control, description.Control,
                roleplay.Control, flag.Control, icon.Control, recruitIcon.Control, jobIcons.Control, isDefault, opposed.Control }),
            ("Economy", new Control[] { dollars.Control, submissions.Control, includeDollars }),
            ("Cell Kit", new Control[] { machines.Control, placeables.Control }),
            ("Vendors", new Control[] { vendors.Control }),
            ("Loadouts", new Control[] { loadouts.Control }),
        };
        for (var i = 0; i < pages.Length; i++)
        {
            tabs.AddChild(TabPage(pages[i].Controls));
            TabContainer.SetTabTitle(tabs.GetChild(i), pages[i].Title);
        }
        _pane.AddChild(tabs);

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
                RecruitStatusIcon = ToIconOrNull(recruitIcon.Read()),
                JobStatusIcons = jobIcons.Read(),
                OpposedGovforFactions = opposed.Read(),
                // Preserve the built-in override marker so re-saving an edited built-in keeps updating the
                // same row instead of spawning a fresh faction (the server also re-stamps it).
                BuiltinOverrideOf = meta.BuiltinOverrideOf,
            },
            Economy =
            {
                DollarsToPointsRate = ParseFloat(dollars.Read(), FactionDefinition.DefaultDollarsToPointsRate),
                PointsSubmissions = submissions.Read(),
                IncludeDollars = includeDollars.Pressed,
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

        var save = new Button { Text = _scope == InsurgencyEditorScope.Custom ? "Save (server / Custom)" : "Save (server / Default)" };
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
            // Export this faction to a filled-in spreadsheet, for editing outside the game or handing a
            // ready-made faction to a player to tweak. The server builds the workbook.
            var exportSheet = new Button { Text = "Export to sheet" };
            exportSheet.OnPressed += _ => _onExportFaction(entry.Id);
            buttons.AddChild(exportSheet);

            // Applying a faction to the round is a Default-editor (host) function; the Custom editor
            // also cannot touch host-authored rows. The server enforces both regardless.
            if (_scope == InsurgencyEditorScope.Default)
            {
                var select = new Button { Text = "Apply for round" };
                select.OnPressed += _ => _onSelect(entry.Id);
                buttons.AddChild(select);
            }

            if (_scope == InsurgencyEditorScope.Default || !entry.IsDefault)
            {
                var delete = new Button { Text = "Delete" };
                delete.OnPressed += _ => _onDelete(entry.Id);
                buttons.AddChild(delete);
            }
        }

        _pane.AddChild(buttons);
        InsforUiStyle.Apply(this); // restyle the freshly built pane controls
    }

    // ----- spreadsheet import / export ------------------------------------------

    /// <summary>
    ///     Called by the EUI when the server returns a generated workbook. Prompts for a save location
    ///     and writes the .xlsx bytes there, defaulting to the server-suggested file name.
    /// </summary>
    public async void SaveWorkbook(byte[] data, string fileName)
    {
        // fileName is the server's suggested name; the native dialog does not take a default, so it is
        // only advisory. The user picks the final path here. Wrapped whole so a locked target path (the
        // file already open in Excel) is reported as a no-op instead of crashing the client.
        try
        {
            var file = await _fileDialog.SaveFile(new FileDialogFilters(new FileDialogFilters.Group("xlsx")));
            if (file == null)
                return;

            await using var stream = file.Value.fileStream;
            await stream.WriteAsync(data);
        }
        catch (Exception)
        {
            // Writing the workbook is best-effort; a failed save just means the user picks again.
        }
    }

    // Opens a filled-in faction spreadsheet and hands its raw bytes to the server, which reads, validates,
    // and stores it. Never parses the file locally; the server has the final say.
    private async void ImportFactionFromFile()
    {
        try
        {
            // Read-only with a shared lock: the player almost always still has the file open in Excel,
            // which keeps a write lock. Requesting write access (the default) would throw a sharing
            // violation; read + FileShare.ReadWrite opens alongside Excel.
            await using var file = await _fileDialog.OpenFile(
                new FileDialogFilters(new FileDialogFilters.Group("xlsx")),
                System.IO.FileAccess.Read,
                System.IO.FileShare.ReadWrite);
            if (file == null)
                return;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();
            if (bytes.Length > 0)
                _onImportFaction(bytes);
        }
        catch (Exception)
        {
            // A locked, malformed, or unreadable file is ignored rather than crashing the client;
            // nothing is sent to the server.
        }
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
    // A small sprite preview shown next to picked values; hidden when the id isn't an entity
    // (platoons, jobs, icons), so the same picker plumbing serves every id type.
    private EntityPrototypeView MakeEntityIcon(string id, out Action<string> setIcon)
    {
        var view = new EntityPrototypeView { MinSize = new Vector2(24, 24), VerticalAlignment = VAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        var icon = view;
        setIcon = value =>
        {
            if (!string.IsNullOrEmpty(value) && _prototype.HasIndex<EntityPrototype>(value))
            {
                icon.SetPrototype(new EntProtoId(value));
                icon.Visible = true;
            }
            else
            {
                icon.Visible = false;
            }
        };
        setIcon(id);
        return view;
    }

    private Editor<string> PickerField(string label, string? current, Action<Action<string>> openPicker)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        box.AddChild(new Label { Text = label, MinSize = new Vector2(190, 0) });

        var selected = current ?? string.Empty;
        var icon = MakeEntityIcon(selected, out var setIcon);
        var button = new Button { Text = PickerText(selected), HorizontalExpand = true };
        button.OnPressed += _ => openPicker(id =>
        {
            selected = id;
            button.Text = PickerText(id);
            setIcon(id);
        });

        var clear = new Button { Text = "Clear" };
        clear.OnPressed += _ =>
        {
            selected = string.Empty;
            button.Text = PickerText(string.Empty);
            setIcon(string.Empty);
        };

        box.AddChild(icon);
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
            var icon = MakeEntityIcon(current, out var setIcon);
            row.AddChild(icon);
            var button = new Button { Text = PickerText(current), HorizontalExpand = true };
            button.OnPressed += _ => openPicker(id =>
            {
                current = id;
                button.Text = PickerText(id);
                setIcon(id);
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

            // Mode 0 = this many items make one point. Mode 1 = one item is worth this many points.
            var mode = new OptionButton();
            mode.AddItem("items per point", 0);
            mode.AddItem("points per item", 1);
            mode.SelectId(entry.PointsPerItemMode ? 1 : 0);
            mode.OnItemSelected += args => mode.SelectId(args.Id);

            var startValue = entry.PointsPerItemMode ? entry.PointsPerItem : entry.AmountPerPoint;
            var value = new LineEdit { Text = startValue.ToString(), MinSize = new Vector2(60, 0), PlaceHolder = "ratio" };

            var remove = new Button { Text = "X" };
            Func<PointsSubmissionEntry> reader = () =>
            {
                var pointsPerItemMode = mode.SelectedId == 1;
                // At least one either way so a submission can never mint infinite points.
                var parsed = Math.Max(1, ParseIntOrNull(value.Text) ?? 1);
                return new PointsSubmissionEntry
                {
                    Entity = new EntProtoId(current),
                    PointsPerItemMode = pointsPerItemMode,
                    AmountPerPoint = pointsPerItemMode ? 15 : parsed,
                    PointsPerItem = pointsPerItemMode ? parsed : 1,
                };
            };
            remove.OnPressed += _ =>
            {
                rows.RemoveChild(row);
                readers.Remove(reader);
            };

            row.AddChild(button);
            row.AddChild(mode);
            row.AddChild(value);
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
            var wrenchable = new CheckBox { Text = "Wrenchable (can be wrenched down and moved)", Pressed = vendor.Wrenchable };
            var invulnerable = new CheckBox { Text = "Invulnerable (base entity won't break / change on damage)", Pressed = vendor.Invulnerable };
            var intelPoints = new CheckBox { Text = "Uses cell intel points (money at the intel computer stocks this vendor)", Pressed = vendor.UsesIntelPoints };
            // For built-in vendors that reuse a fully authored prototype (the CLF requisitions rack): keep
            // the base entity's own arsenal instead of the sections below. Leave off for normal vendors.
            var useBaseSections = new CheckBox { Text = "Use base model's own arsenal (ignore the sections below)", Pressed = vendor.UseBaseModelSections };
            var sections = SectionListEditor(vendor.Sections);

            var remove = new Button { Text = "Remove vendor" };
            Func<FactionVendorDefinition> reader = () => new FactionVendorDefinition
            {
                Name = name.Read(),
                BaseModel = new EntProtoId(model.Read()),
                Sections = sections.Read(),
                Wrenchable = wrenchable.Pressed,
                Invulnerable = invulnerable.Pressed,
                UsesIntelPoints = intelPoints.Pressed,
                UseBaseModelSections = useBaseSections.Pressed,
            };
            remove.OnPressed += _ =>
            {
                rows.RemoveChild(panel);
                readers.Remove(reader);
            };

            inner.AddChild(name.Control);
            inner.AddChild(model.Control);
            inner.AddChild(wrenchable);
            inner.AddChild(invulnerable);
            inner.AddChild(intelPoints);
            inner.AddChild(useBaseSections);
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

    // Per-job status icon overrides: each row is a job (picked, never typed) plus the faction icon its
    // members should show. Empty list = every job uses the faction-wide status icon. Modelled on the same
    // job-selection flow as the A Package / role-loadout editor.
    private Editor<List<FactionJobIcon>> JobIconListEditor(IEnumerable<FactionJobIcon> initial)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        box.AddChild(Header("Per-job status icons (empty = all jobs use the faction icon above)"));
        var rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var readers = new List<Func<FactionJobIcon>>();

        void AddRow(FactionJobIcon entry)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };

            var job = entry.Role;
            var jobButton = new Button { Text = PickerText(job), HorizontalExpand = true };
            jobButton.OnPressed += _ => OpenProtoPicker(Loc.GetString("insfor-picker-job-title"), JobOptions(), id =>
            {
                job = id;
                jobButton.Text = PickerText(id);
            });

            var icon = entry.Icon?.Id ?? string.Empty;
            var iconButton = new Button { Text = PickerText(icon), HorizontalExpand = true };
            iconButton.OnPressed += _ => OpenProtoPicker(Loc.GetString("insfor-picker-icon-title"), IconOptions(), id =>
            {
                icon = id;
                iconButton.Text = PickerText(id);
            });

            var remove = new Button { Text = "X" };
            Func<FactionJobIcon> reader = () => new FactionJobIcon
            {
                Role = job,
                Icon = ToIconOrNull(icon),
            };
            remove.OnPressed += _ =>
            {
                rows.RemoveChild(row);
                readers.Remove(reader);
            };

            row.AddChild(jobButton);
            row.AddChild(iconButton);
            row.AddChild(remove);
            rows.AddChild(row);
            readers.Add(reader);
        }

        foreach (var e in initial)
            AddRow(e);

        var add = new Button { Text = "+ Add per-job icon" };
        add.OnPressed += _ => AddRow(new FactionJobIcon());

        box.AddChild(rows);
        box.AddChild(add);
        // Keep only rows that named both a job and an icon.
        return new Editor<List<FactionJobIcon>>(box, () => readers.Select(r => r())
            .Where(e => !string.IsNullOrWhiteSpace(e.Role) && e.Icon != null)
            .ToList());
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

    // One scrollable tab page holding the given controls stacked vertically. Each category tab uses its own
    // scroll so a long list (vendors, loadouts) never pushes the others off-screen.
    private static ScrollContainer TabPage(params Control[] controls)
    {
        var inner = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        foreach (var c in controls)
            inner.AddChild(c);
        return new ScrollContainer { Children = { inner }, HorizontalExpand = true, VerticalExpand = true };
    }

    private static Editor<string> LabeledLine(string label, string? value)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        box.AddChild(new Label { Text = label, MinSize = new Vector2(190, 0) });
        var line = new LineEdit { Text = value ?? string.Empty, HorizontalExpand = true };
        box.AddChild(line);
        return new Editor<string>(box, () => line.Text.Trim());
    }

    // ---------------------------------------------------------------------
    // How tall the multi-line prose boxes start, in pixels. Bump this up for even more room.
    // ---------------------------------------------------------------------
    private const float MultilineHeight = 90f;

    // A label above a multi-line text box, for prose fields (title, description, roleplay, recruited)
    // where authors want to write more than one line. Reads back the collapsed, trimmed text.
    private static Editor<string> LabeledMultiline(string label, string? value)
    {
        var box = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(0, 0, 0, 6) };
        box.AddChild(new Label { Text = label });

        var edit = new TextEdit
        {
            TextRope = new Rope.Leaf(value ?? string.Empty),
            HorizontalExpand = true,
            MinSize = new Vector2(0, MultilineHeight),
        };
        box.AddChild(edit);

        return new Editor<string>(box, () => Rope.Collapse(edit.TextRope).Trim());
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
