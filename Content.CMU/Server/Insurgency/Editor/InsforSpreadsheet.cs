using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ClosedXML.Excel;
using Content.Shared._AU14.Insurgency;
using Content.Shared._RMC14.Vendors;
using Content.Shared.AU14.util;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Insurgency.Editor;

/// <summary>
///     Builds and reads the faction authoring spreadsheet (.xlsx) with ClosedXML. The whole point is a
///     zero-setup workflow: a host exports a workbook whose dropdowns are already filled with the current
///     entities/icons/jobs/flags/platoons (by their in-game names) and whose Help sheet explains every
///     field; a player fills it in and sends it back; the host imports it. No macros, no catalog import,
///     nothing to enable.
///
///     Security: reading only ever pulls cell values (strings and numbers) - no formulas are evaluated
///     and no macros are run. The caller passes the result through <see cref="InsurgencyFactionValidator"/>
///     before storing it, so a hand-edited or hostile file is clamped and stripped of unknown prototype
///     ids exactly like any other untrusted payload.
/// </summary>
public static class InsforSpreadsheet
{
    public const int MaxWorkbookBytes = 5_000_000;
    private const int MaxArchiveEntries = 256;
    private const long MaxExpandedBytes = 25_000_000;
    private const int MaxWorksheets = 32;
    private const int MaxRowsPerSheet = 10_000;
    private const int MaxColumnsPerSheet = 64;
    private const int MaxUsedCells = 100_000;
    private const int MaxCellTextLength = FactionDefinition.MaxRoleplayTextLength;

    // How many rows of each list sheet get dropdown validation. Set above the largest schema cap
    // (MaxPlaceableEntities = 256) so even a maxed-out faction round-trips with every row validated. A
    // filler can still type past these; the values are validated server-side on import regardless.
    private const int EditRows = 300;

    private static readonly XLColor HeaderColor = XLColor.FromHtml("#C88A2C"); // USCM amber
    private static readonly XLColor HeaderText = XLColor.FromHtml("#2B2213");

    // ---------------------------------------------------------------------
    // Catalog: every pickable id paired with a "Friendly Name [id]" display string.
    // ---------------------------------------------------------------------
    private sealed class Catalog
    {
        public readonly List<(string disp, string id)> Entities = new();
        public readonly List<(string disp, string id)> VendorBases = new();
        public readonly List<(string disp, string id)> Icons = new();
        public readonly List<(string disp, string id)> Jobs = new();
        public readonly List<(string disp, string id)> Flags = new();
        public readonly List<(string disp, string id)> Platoons = new();

        public readonly Dictionary<string, string> EntityDisp = new();
        public readonly Dictionary<string, string> IconDisp = new();
        public readonly Dictionary<string, string> JobDisp = new();
        public readonly Dictionary<string, string> PlatoonDisp = new();
    }

    private static string Disp(string name, string id) => $"{name} [{id}]";

    private static Catalog BuildCatalog(IPrototypeManager protos)
    {
        var c = new Catalog();

        foreach (var p in protos.EnumeratePrototypes<EntityPrototype>()
                     .Where(p => !p.Abstract && !string.IsNullOrWhiteSpace(p.Name))
                     .OrderBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase))
        {
            var d = Disp(p.Name, p.ID);
            c.Entities.Add((d, p.ID));
            c.EntityDisp[p.ID] = d;

            if (p.Components.Keys.Any(k => k.Contains("Vendor", StringComparison.OrdinalIgnoreCase)))
                c.VendorBases.Add((d, p.ID));
            if (p.ID.Contains("Flag", StringComparison.OrdinalIgnoreCase))
                c.Flags.Add((d, p.ID));
        }

        foreach (var p in protos.EnumeratePrototypes<FactionIconPrototype>()
                     .OrderBy(p => p.ID, StringComparer.InvariantCultureIgnoreCase))
        {
            var d = Disp(p.ID, p.ID);
            c.Icons.Add((d, p.ID));
            c.IconDisp[p.ID] = d;
        }

        foreach (var p in protos.EnumeratePrototypes<JobPrototype>()
                     .OrderBy(p => p.LocalizedName, StringComparer.InvariantCultureIgnoreCase))
        {
            var d = Disp(p.LocalizedName, p.ID);
            c.Jobs.Add((d, p.ID));
            c.JobDisp[p.ID] = d;
        }

        foreach (var p in protos.EnumeratePrototypes<PlatoonPrototype>()
                     .OrderBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase))
        {
            var name = string.IsNullOrWhiteSpace(p.Name) ? p.ID : p.Name;
            var d = Disp(name, p.ID);
            c.Platoons.Add((d, p.ID));
            c.PlatoonDisp[p.ID] = d;
        }

        return c;
    }

    // ---------------------------------------------------------------------
    // Build
    // ---------------------------------------------------------------------
    public static byte[] Build(IPrototypeManager protos, FactionDefinition? existing)
    {
        var c = BuildCatalog(protos);
        using var wb = new XLWorkbook();

        // Hidden catalog sheets first, so the list-validation ranges exist.
        var entRange = AddCat(wb, "cat_Entities", c.Entities);
        var vbRange = AddCat(wb, "cat_VendorBases", c.VendorBases);
        var iconRange = AddCat(wb, "cat_Icons", c.Icons);
        var jobRange = AddCat(wb, "cat_Jobs", c.Jobs);
        var flagRange = AddCat(wb, "cat_Flags", c.Flags);
        var platRange = AddCat(wb, "cat_Platoons", c.Platoons);
        var boolRange = AddCat(wb, "cat_Bool",
            new List<(string, string)> { ("TRUE", "TRUE"), ("FALSE", "FALSE") });

        AddHelpSheet(wb);
        AddFactionSheet(wb, existing, iconRange, flagRange, boolRange, c);

        AddListSheet(wb, "OpposedGovfor", new[] { "Platoon" }, new[] { 45.0 },
            new Dictionary<int, IXLRange?> { { 1, platRange } },
            existing?.Metadata.OpposedGovforFactions.Select(g => new[] { Show(c.PlatoonDisp, g) }));

        AddListSheet(wb, "JobIcons", new[] { "Role", "Icon" }, new[] { 45.0, 40.0 },
            new Dictionary<int, IXLRange?> { { 1, jobRange }, { 2, iconRange } },
            existing?.Metadata.JobStatusIcons.Select(j => new[]
                { Show(c.JobDisp, j.Role), Show(c.IconDisp, j.Icon?.Id) }));

        AddListSheet(wb, "PointsSubmissions",
            new[] { "Entity", "PointsPerItemMode", "AmountPerPoint", "PointsPerItem" },
            new[] { 45.0, 18.0, 16.0, 16.0 },
            new Dictionary<int, IXLRange?> { { 1, entRange }, { 2, boolRange } },
            existing?.Economy.PointsSubmissions.Select(s => new[]
            {
                Show(c.EntityDisp, s.Entity.Id), Bool(s.PointsPerItemMode),
                s.AmountPerPoint.ToString(), s.PointsPerItem.ToString(),
            }));

        AddListSheet(wb, "Placeables", new[] { "Entity" }, new[] { 45.0 },
            new Dictionary<int, IXLRange?> { { 1, entRange } },
            existing?.CellKit.PlaceableEntities.Select(p => new[] { Show(c.EntityDisp, p.Id) }));

        AddListSheet(wb, "Vendors",
            new[] { "Name", "BaseModel", "Wrenchable", "Invulnerable", "UsesIntelPoints", "UseBaseModelSections" },
            new[] { 28.0, 45.0, 14.0, 14.0, 16.0, 22.0 },
            new Dictionary<int, IXLRange?> { { 2, vbRange }, { 3, boolRange }, { 4, boolRange }, { 5, boolRange }, { 6, boolRange } },
            existing?.CellKit.VendorDefinitions.Select(v => new[]
            {
                v.Name, Show(c.EntityDisp, v.BaseModel.Id), Bool(v.Wrenchable), Bool(v.Invulnerable),
                Bool(v.UsesIntelPoints), Bool(v.UseBaseModelSections),
            }));

        AddListSheet(wb, "VendorSections",
            new[] { "Vendor", "Section", "PerPlayerLimit", "GlobalLimit" },
            new[] { 28.0, 28.0, 16.0, 14.0 },
            new Dictionary<int, IXLRange?>(),
            existing?.CellKit.VendorDefinitions.SelectMany(v => v.Sections.Select(s => new[]
            {
                v.Name, s.Name, s.Choices?.Amount.ToString() ?? "", s.SharedJOLimit?.ToString() ?? "",
            })));

        AddListSheet(wb, "VendorEntries",
            new[] { "Vendor", "Section", "EntityId", "Points", "Amount", "Max" },
            new[] { 28.0, 28.0, 45.0, 12.0, 12.0, 12.0 },
            new Dictionary<int, IXLRange?> { { 3, entRange } },
            existing?.CellKit.VendorDefinitions.SelectMany(v => v.Sections.SelectMany(s => s.Entries.Select(e => new[]
            {
                v.Name, s.Name, Show(c.EntityDisp, e.Id.Id),
                e.Points?.ToString() ?? "", e.Amount?.ToString() ?? "", e.Max?.ToString() ?? "",
            }))));

        AddListSheet(wb, "RoleLoadouts", new[] { "Role", "Content" }, new[] { 45.0, 45.0 },
            new Dictionary<int, IXLRange?> { { 1, jobRange }, { 2, entRange } },
            existing?.RoleLoadouts.SelectMany(l => l.Contents.Select(content => new[]
                { Show(c.JobDisp, l.Role), Show(c.EntityDisp, content.Id) })));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static IXLRange? AddCat(XLWorkbook wb, string name, List<(string disp, string id)> items)
    {
        var ws = wb.AddWorksheet(name);
        ws.Visibility = XLWorksheetVisibility.Hidden;
        ws.Cell(1, 1).Value = "display";
        ws.Cell(1, 2).Value = "id";
        var r = 2;
        foreach (var (disp, id) in items)
        {
            ws.Cell(r, 1).Value = disp;
            ws.Cell(r, 2).Value = id;
            r++;
        }

        return items.Count == 0 ? null : ws.Range(2, 1, items.Count + 1, 1);
    }

    private static void AddFactionSheet(XLWorkbook wb, FactionDefinition? f,
        IXLRange? iconRange, IXLRange? flagRange, IXLRange? boolRange, Catalog c)
    {
        var ws = wb.AddWorksheet("Faction");
        ws.Cell(1, 1).Value = "Field";
        ws.Cell(1, 2).Value = "Value";
        StyleHeader(ws, 2);
        ws.Column(1).Width = 24;
        ws.Column(2).Width = 90;

        var m = f?.Metadata;
        var rows = new (string label, string value, IXLRange? dv)[]
        {
            ("Title", m?.Title ?? "", null),
            ("Description", m?.Description ?? "", null),
            ("RoleplayText", m?.RoleplayText ?? "", null),
            ("RecruitedMessage", m?.RecruitedMessage ?? "", null),
            ("StatusIcon", Show(c.IconDisp, m?.StatusIcon?.Id), iconRange),
            ("FlagEntity", Show(c.EntityDisp, m?.FlagEntity?.Id), flagRange),
            ("DollarsToPointsRate", (f?.Economy.DollarsToPointsRate ?? 1f).ToString(CultureInfo.InvariantCulture), null),
            ("IncludeDollars", Bool(f?.Economy.IncludeDollars ?? true), boolRange),
        };
        for (var i = 0; i < rows.Length; i++)
        {
            var row = i + 2;
            ws.Cell(row, 1).Value = rows[i].label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = rows[i].value;
            if (rows[i].dv is { } dvRange)
                ws.Cell(row, 2).CreateDataValidation().List(dvRange, true);
        }

        ws.SheetView.FreezeRows(1);
    }

    // A styled list sheet: header row, per-column dropdowns down EditRows, and any pre-filled rows.
    private static void AddListSheet(XLWorkbook wb, string name, string[] headers, double[] widths,
        Dictionary<int, IXLRange?> validations, IEnumerable<string[]>? data)
    {
        var ws = wb.AddWorksheet(name);
        for (var col = 0; col < headers.Length; col++)
        {
            ws.Cell(1, col + 1).Value = headers[col];
            ws.Column(col + 1).Width = widths[col];
        }

        StyleHeader(ws, headers.Length);
        ws.SheetView.FreezeRows(1);

        // Dropdowns down the editable range so typed-in new rows still validate.
        foreach (var (col, range) in validations)
        {
            if (range is not { } dvRange)
                continue;
            ws.Range(2, col, EditRows + 1, col).CreateDataValidation().List(dvRange, true);
        }

        if (data == null)
            return;

        var r = 2;
        foreach (var rowValues in data)
        {
            for (var col = 0; col < rowValues.Length; col++)
                ws.Cell(r, col + 1).Value = rowValues[col];
            r++;
        }
    }

    private static void AddHelpSheet(XLWorkbook wb)
    {
        var ws = wb.AddWorksheet("Help");
        ws.SheetView.FreezeRows(1);
        ws.Column(1).Width = 120;
        ws.Cell(1, 1).Value = "INSFOR Faction - how to fill this in";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 15;
        ws.Cell(1, 1).Style.Font.FontColor = HeaderColor;

        var r = 3;
        foreach (var (heading, text) in HelpContent)
        {
            if (heading != null)
            {
                ws.Cell(r, 1).Value = heading;
                ws.Cell(r, 1).Style.Font.Bold = true;
                ws.Cell(r, 1).Style.Font.FontColor = HeaderColor;
                r++;
            }

            ws.Cell(r, 1).Value = text;
            ws.Cell(r, 1).Style.Alignment.WrapText = true;
            ws.Row(r).Height = 15 * Math.Max(1, text.Length / 110 + text.Count(ch => ch == '\n') + 1);
            r += 2;
        }
    }

    private static void StyleHeader(IXLWorksheet ws, int ncols)
    {
        var range = ws.Range(1, 1, 1, ncols);
        range.Style.Fill.BackgroundColor = HeaderColor;
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = HeaderText;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    // Look up the "Name [id]" display for an id when pre-filling an existing faction; falls back to the
    // bare id so nothing is lost if the id is no longer in the catalog.
    private static string Show(Dictionary<string, string> map, string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "";
        return map.TryGetValue(id, out var disp) ? disp : id;
    }

    private static string Bool(bool value) => value ? "TRUE" : "FALSE";

    // ---------------------------------------------------------------------
    // Read
    // ---------------------------------------------------------------------
    public static FactionDefinition? Read(Stream stream)
    {
        if (!ValidateArchive(stream))
            return null;

        XLWorkbook wb;
        try
        {
            wb = new XLWorkbook(stream);
        }
        catch (Exception)
        {
            return null; // Not a readable workbook.
        }

        using (wb)
        {
            if (!ValidateWorkbookShape(wb))
                return null;

            var def = new FactionDefinition();
            var meta = def.Metadata;

            var faction = TryWs(wb, "Faction");
            if (faction != null)
            {
                meta.Title = Field(faction, "Title");
                meta.Description = Field(faction, "Description");
                meta.RoleplayText = Field(faction, "RoleplayText");
                meta.RecruitedMessage = Field(faction, "RecruitedMessage");
                meta.StatusIcon = ToIcon(ExtractId(Field(faction, "StatusIcon")));
                meta.FlagEntity = ToEnt(ExtractId(Field(faction, "FlagEntity")));
                if (float.TryParse(Field(faction, "DollarsToPointsRate"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var rate))
                    def.Economy.DollarsToPointsRate = rate;
                def.Economy.IncludeDollars = ParseBool(Field(faction, "IncludeDollars"), true);
            }

            foreach (var row in DataRows(wb, "OpposedGovfor"))
            {
                var id = ExtractId(Str(row, 1));
                if (!string.IsNullOrWhiteSpace(id))
                    meta.OpposedGovforFactions.Add(id);
            }

            foreach (var row in DataRows(wb, "JobIcons"))
            {
                var role = ExtractId(Str(row, 1));
                var icon = ExtractId(Str(row, 2));
                if (!string.IsNullOrWhiteSpace(role))
                    meta.JobStatusIcons.Add(new FactionJobIcon { Role = role, Icon = ToIcon(icon) });
            }

            foreach (var row in DataRows(wb, "PointsSubmissions"))
            {
                var ent = ExtractId(Str(row, 1));
                if (string.IsNullOrWhiteSpace(ent))
                    continue;
                def.Economy.PointsSubmissions.Add(new PointsSubmissionEntry
                {
                    Entity = new EntProtoId(ent),
                    PointsPerItemMode = ParseBool(Str(row, 2), false),
                    AmountPerPoint = ParseInt(Str(row, 3)) ?? 15,
                    PointsPerItem = ParseInt(Str(row, 4)) ?? 1,
                });
            }

            foreach (var row in DataRows(wb, "Placeables"))
            {
                var ent = ExtractId(Str(row, 1));
                if (!string.IsNullOrWhiteSpace(ent))
                    def.CellKit.PlaceableEntities.Add(new EntProtoId(ent));
            }

            ReadVendors(wb, def);
            ReadLoadouts(wb, def);

            return def;
        }
    }

    private static bool ValidateArchive(Stream stream)
    {
        if (!stream.CanSeek || stream.Length is <= 0 or > MaxWorkbookBytes)
            return false;

        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > MaxArchiveEntries)
                return false;

            long expanded = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.Length < 0 || entry.Length > MaxExpandedBytes - expanded)
                    return false;
                expanded += entry.Length;
            }

            return expanded <= MaxExpandedBytes;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static bool ValidateWorkbookShape(XLWorkbook workbook)
    {
        try
        {
            if (workbook.Worksheets.Count > MaxWorksheets)
                return false;

            var usedCells = 0;
            foreach (var worksheet in workbook.Worksheets)
            {
                var range = worksheet.RangeUsed();
                if (range == null)
                    continue;

                if (range.RangeAddress.LastAddress.RowNumber > MaxRowsPerSheet ||
                    range.RangeAddress.LastAddress.ColumnNumber > MaxColumnsPerSheet)
                    return false;

                foreach (var cell in worksheet.CellsUsed())
                {
                    if (++usedCells > MaxUsedCells || cell.GetString().Length > MaxCellTextLength)
                        return false;
                }
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void ReadVendors(XLWorkbook wb, FactionDefinition def)
    {
        // Vendors keyed by name so their sections and entries can be attached.
        var vendors = new Dictionary<string, FactionVendorDefinition>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        foreach (var row in DataRows(wb, "Vendors"))
        {
            var vname = Str(row, 1);
            if (string.IsNullOrWhiteSpace(vname) || vendors.ContainsKey(vname))
                continue;
            vendors[vname] = new FactionVendorDefinition
            {
                Name = vname,
                BaseModel = new EntProtoId(ExtractId(Str(row, 2))),
                Wrenchable = ParseBool(Str(row, 3), true),
                Invulnerable = ParseBool(Str(row, 4), false),
                UsesIntelPoints = ParseBool(Str(row, 5), true),
                UseBaseModelSections = ParseBool(Str(row, 6), false),
            };
            order.Add(vname);
        }

        // Sections keyed by "vendor section" so entries can find their section.
        var sections = new Dictionary<string, CMVendorSection>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in DataRows(wb, "VendorSections"))
        {
            var vname = Str(row, 1);
            var sname = Str(row, 2);
            if (string.IsNullOrWhiteSpace(vname) || string.IsNullOrWhiteSpace(sname) || !vendors.ContainsKey(vname))
                continue;
            var key = vname + " " + sname;
            if (sections.ContainsKey(key))
                continue;
            var perPlayer = ParseInt(Str(row, 3));
            var section = new CMVendorSection
            {
                Name = sname,
                Choices = perPlayer is { } p ? (sname, p) : null,
                SharedJOLimit = ParseInt(Str(row, 4)),
            };
            sections[key] = section;
            vendors[vname].Sections.Add(section);
        }

        foreach (var row in DataRows(wb, "VendorEntries"))
        {
            var vname = Str(row, 1);
            var sname = Str(row, 2);
            var ent = ExtractId(Str(row, 3));
            if (string.IsNullOrWhiteSpace(ent))
                continue;
            if (!sections.TryGetValue(vname + " " + sname, out var section))
                continue;
            section.Entries.Add(new CMVendorEntry
            {
                Id = new EntProtoId(ent),
                Points = ParseInt(Str(row, 4)),
                Amount = ParseInt(Str(row, 5)),
                Max = ParseInt(Str(row, 6)),
            });
        }

        foreach (var name in order)
            def.CellKit.VendorDefinitions.Add(vendors[name]);
    }

    private static void ReadLoadouts(XLWorkbook wb, FactionDefinition def)
    {
        // One row per content item; group the contents by role.
        var byRole = new Dictionary<string, FactionRoleLoadout>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        foreach (var row in DataRows(wb, "RoleLoadouts"))
        {
            var role = ExtractId(Str(row, 1));
            var content = ExtractId(Str(row, 2));
            if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(content))
                continue;
            if (!byRole.TryGetValue(role, out var loadout))
            {
                loadout = new FactionRoleLoadout { Role = role };
                byRole[role] = loadout;
                order.Add(role);
            }

            loadout.Contents.Add(new EntProtoId(content));
        }

        foreach (var role in order)
            def.RoleLoadouts.Add(byRole[role]);
    }

    // ---- read helpers ----

    private static IXLWorksheet? TryWs(XLWorkbook wb, string name) =>
        wb.Worksheets.TryGetWorksheet(name, out var ws) ? ws : null;

    private static IEnumerable<IXLRow> DataRows(XLWorkbook wb, string sheet)
    {
        var ws = TryWs(wb, sheet);
        if (ws == null)
            return Enumerable.Empty<IXLRow>();
        return ws.RowsUsed().Where(r => r.RowNumber() > 1);
    }

    private static string Str(IXLRow row, int col) => row.Cell(col).GetString().Trim();

    private static string Field(IXLWorksheet ws, string label)
    {
        foreach (var row in ws.RowsUsed())
        {
            if (string.Equals(row.Cell(1).GetString().Trim(), label, StringComparison.OrdinalIgnoreCase))
                return row.Cell(2).GetString().Trim();
        }

        return "";
    }

    // Pulls the id out of a "Friendly Name [id]" value; returns the trimmed input when there is no
    // trailing bracket (plain text, numbers, or a value typed by hand).
    private static string ExtractId(string display)
    {
        var open = display.LastIndexOf('[');
        var close = display.LastIndexOf(']');
        if (open >= 0 && close > open)
            return display.Substring(open + 1, close - open - 1).Trim();
        return display.Trim();
    }

    private static bool ParseBool(string s, bool fallback)
    {
        s = s.Trim();
        if (string.Equals(s, "TRUE", StringComparison.OrdinalIgnoreCase) || s == "1")
            return true;
        if (string.Equals(s, "FALSE", StringComparison.OrdinalIgnoreCase) || s == "0")
            return false;
        return fallback;
    }

    private static int? ParseInt(string s) =>
        int.TryParse(s.Trim(), out var i) ? i : null;

    private static ProtoId<FactionIconPrototype>? ToIcon(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : new ProtoId<FactionIconPrototype>?(new ProtoId<FactionIconPrototype>(value));

    private static EntProtoId? ToEnt(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : new EntProtoId?(new EntProtoId(value));

    // ---------------------------------------------------------------------
    // Help text mirrored from the in-game editor help window, so the sheet is self-explanatory.
    // ---------------------------------------------------------------------
    private static readonly (string? heading, string text)[] HelpContent =
    {
        (null, "An INSFOR faction is one insurgent cell the CLF leader can pick after spawning. You fill in who they are, what money buys them points, what their leader's Heavy Cell Kit can drop, and what each role gets in their \"A Package\". Fill in the sheets, then send this file back to the host to import. Every id field is a dropdown: click the cell and pick by name - never type a raw id. Add a new entry on the next empty row of a sheet. Leave a sheet empty if the faction does not use it. The server re-checks and clamps everything on import, so you cannot break the round with a bad value."),
        ("Faction sheet - Identity", "Title: the faction's name, shown in the pick list and the reveal popup. Description / RoleplayText: shown in the antag briefing and the reveal popup so members know who they are and how they are meant to play. RecruitedMessage: the briefing a freshly recruited member reads (for example via the tattoo gun); blank uses the default CLF line. StatusIcon: the faction membership icon members show to each other. FlagEntity: an optional in-world flag prop."),
        ("Faction sheet - Economy (dollars to points)", "DollarsToPointsRate: how intel dollars convert to the cell's vendor points. IncludeDollars: TRUE means cash still converts at the analyzer even if you add custom submittables on the PointsSubmissions sheet; set FALSE for a faction whose economy should ignore money entirely."),
        ("OpposedGovfor", "The GOVFOR platoons (USMC, TWE RMC, UPP, and so on) this faction is allowed to oppose. If the round's GOVFOR is in this list, the faction is offered to the leader (it gets the * mark in the editor's faction list). Add as many as you like, one platoon per row."),
        ("JobIcons", "Optional per-job status-icon overrides: members of that Role show that Icon instead of the faction icon. One row per job."),
        ("PointsSubmissions", "What the analyzer machine accepts and turns into cell points, beyond plain cash. Entity is the item (picked, never typed). PointsPerItemMode: FALSE = it takes AmountPerPoint of the item to make one point (good for cheap goods); TRUE = one item is worth PointsPerItem points (good for valuable goods). Leave the sheet empty to keep the plain-dollars behavior. The value is always at least 1 so a submission can never mint free points."),
        ("Placeables", "Any additional single entities the leader can free-place from the Heavy Cell Kit (lamps, barricades, props, and so on). One per row. The well-known CLF machines (analyzer, intel computer, objectives console, tech tree console, fax) are ticked in the in-game editor instead - their money-to-points wiring is the normal CLF behavior and needs no setup here."),
        ("Vendors", "Each vendor the leader can deploy from the kit. Name: shown on the deployed vendor and in the kit list. BaseModel: an existing vendor entity used only for its sprite/collision; its arsenal is replaced by your sections. Wrenchable: can be wrenched down and moved after placing. Invulnerable: the placed vendor will not break or change on damage. UsesIntelPoints: items are paid from the cell's shared intel points (money at the intel computer stocks it) instead of the buyer's own points. UseBaseModelSections: ignore your sections and keep the base entity's built-in stock - only for reusing a fully-made vendor (like the CLF requisitions rack); leave FALSE for a normal custom vendor."),
        ("VendorSections", "A vendor is split into sections (categories). Vendor must match a Name from the Vendors sheet. Section is the category name. PerPlayerLimit / GlobalLimit: two optional caps - how many one player may take from this category, and how many all players together may."),
        ("VendorEntries", "Items inside a section. Vendor and Section must match rows above. EntityId is the item. Points: its cost (0 or blank = free-by-stock only). Amount: how many are in stock. Max: the ceiling it restocks to."),
        ("RoleLoadouts", "Because the faction is chosen after players spawn, each role's kit is delivered afterwards as an \"A Package\" box. One row per item: pick the Role (job) and one Content entity. Repeat the same Role on several rows to give it several items."),
        ("Saving and applying (done in-game)", "After the host imports this sheet in the INSFOR editor they choose where it lives: Save (server / Default) writes it to the server database as a host faction offered to leaders whose GOVFOR matches the OpposedGovfor list; Save as local Custom writes it to their machine only, showing in the leader's Custom list; Apply for round immediately applies it to the current round's cell."),
    };
}
