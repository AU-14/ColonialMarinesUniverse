using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Content.Client._RMC14.Attachable.Components;
using Content.Client._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Attachable.Components;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Attachable;

/// <summary>
///     Lines up a weapon's attachment overlays by eye: pick a slot, nudge its offset a pixel at a time while watching an
///     enlarged view of the weapon, preview any attachment in it, then copy the finished offsets as YAML. Changes are
///     only drawn on this client, until the weapon's prototype is updated with the copied YAML.
/// </summary>
public sealed class CMUAttachmentOffsetWindow : DefaultWindow
{
    private const float PixelsPerTile = 32f;

    [Dependency] private IClipboardManager _clipboard = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly AttachableHolderVisualsSystem _visuals;
    private readonly EntityUid _holder;

    private readonly OptionButton _slotSelect = new();
    private readonly List<string> _slots = new();
    private readonly LineEdit _xEdit = new() { MinWidth = 70 };
    private readonly LineEdit _yEdit = new() { MinWidth = 70 };
    private readonly Label _tiles = new();
    private readonly Label _status = new() { FontColorOverride = Color.Gray };
    private readonly OptionButton _stepSelect = new();
    private readonly float[] _steps = [1f, 0.5f, 4f];
    private readonly LineEdit _filter = new() { PlaceHolder = "Filter attachments", HorizontalExpand = true };
    private readonly OptionButton _previewSelect = new() { HorizontalExpand = true };
    private readonly List<EntProtoId> _previewIds = new();
    private readonly List<EntProtoId> _allAttachments;
    private readonly Label _yaml = new() { FontColorOverride = Color.LightGray };

    private EntityUid? _preview;
    private string? _previewSlot;

    public CMUAttachmentOffsetWindow(EntityUid holder)
    {
        IoCManager.InjectDependencies(this);
        _visuals = _entities.System<AttachableHolderVisualsSystem>();
        _holder = holder;

        var meta = _entities.GetComponent<MetaDataComponent>(holder);
        Title = $"Attachment offsets: {meta.EntityName} ({meta.EntityPrototype?.ID ?? "?"})";
        MinSize = new Vector2(560, 640);

        _allAttachments = _prototypes.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && p.TryGetComponent<AttachableVisualsComponent>(out _, _entities.ComponentFactory))
            .OrderBy(p => p.Name)
            .Select(p => new EntProtoId(p.ID))
            .ToList();

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };

        // The weapon itself, blown up so single pixels are easy to see.
        var view = new SpriteView
        {
            Scale = new Vector2(5, 5),
            OverrideDirection = Direction.South,
            SetSize = new Vector2(360, 200),
            HorizontalAlignment = HAlignment.Center,
        };
        view.SetEntity(holder);
        root.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#202428") },
            HorizontalExpand = true,
            Children = { view },
        });

        root.AddChild(Row(new Label { Text = "Slot:" }, _slotSelect));
        root.AddChild(_status);

        root.AddChild(Row(
            new Label { Text = "X px:" }, _xEdit,
            new Label { Text = "Y px:" }, _yEdit,
            _tiles));

        foreach (var step in _steps)
            _stepSelect.AddItem($"{step.ToString(CultureInfo.InvariantCulture)} px");

        root.AddChild(Row(
            new Label { Text = "Nudge:" },
            Button("←", () => Nudge(-1, 0)),
            Button("→", () => Nudge(1, 0)),
            Button("↑", () => Nudge(0, 1)),
            Button("↓", () => Nudge(0, -1)),
            new Label { Text = "Step:" },
            _stepSelect,
            Button("Reset", ResetSlot)));

        root.AddChild(new Label { Text = "Preview an attachment in this slot (drawn on your screen only):" });
        root.AddChild(Row(_filter));
        root.AddChild(Row(_previewSelect, Button("Preview", ShowPreview), Button("Clear", ClearPreview)));

        root.AddChild(Row(new Label { Text = "YAML for the weapon's prototype:" }, Button("Copy YAML", CopyYaml)));
        root.AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#15181b") },
            Children = { _yaml },
        });

        Contents.AddChild(new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, Children = { root } });

        _slotSelect.OnItemSelected += args =>
        {
            _slotSelect.SelectId(args.Id);
            ShowSlot();
        };
        _stepSelect.OnItemSelected += args => _stepSelect.SelectId(args.Id);
        _previewSelect.OnItemSelected += args => _previewSelect.SelectId(args.Id);
        _filter.OnTextChanged += _ => FillPreviewList();
        _xEdit.OnTextEntered += _ => ApplyTyped();
        _yEdit.OnTextEntered += _ => ApplyTyped();

        FillSlots();
        FillPreviewList();
        ShowSlot();
    }

    private static BoxContainer Row(params Control[] children)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        foreach (var child in children)
            row.AddChild(child);
        return row;
    }

    private static Button Button(string text, Action pressed)
    {
        var button = new Button { Text = text };
        button.OnPressed += _ => pressed();
        return button;
    }

    private bool TryGetVisuals(out Entity<AttachableHolderVisualsComponent> holder)
    {
        holder = default;
        if (!_entities.TryGetComponent(_holder, out AttachableHolderVisualsComponent? visuals))
            return false;

        holder = (_holder, visuals);
        return true;
    }

    private string? SelectedSlot => _slotSelect.SelectedId >= 0 && _slotSelect.SelectedId < _slots.Count
        ? _slots[_slotSelect.SelectedId]
        : null;

    private void FillSlots()
    {
        _slots.Clear();
        var slots = new SortedSet<string>(StringComparer.Ordinal);
        if (_entities.TryGetComponent(_holder, out AttachableHolderComponent? holder))
            slots.UnionWith(holder.Slots.Keys);

        if (TryGetVisuals(out var visuals))
            slots.UnionWith(_visuals.CMUGetOffsets(visuals).Keys);

        _slotSelect.Clear();
        foreach (var slot in slots)
        {
            _slotSelect.AddItem(slot, _slots.Count);
            _slots.Add(slot);
        }
    }

    private void FillPreviewList()
    {
        var filter = _filter.Text.Trim();
        _previewIds.Clear();
        _previewSelect.Clear();
        foreach (var id in _allAttachments)
        {
            var proto = _prototypes.Index(id);
            if (filter.Length > 0 &&
                !proto.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !proto.ID.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _previewSelect.AddItem($"{proto.Name} ({proto.ID})", _previewIds.Count);
            _previewIds.Add(id);
        }
    }

    private Vector2 CurrentOffset()
    {
        return SelectedSlot is { } slot && TryGetVisuals(out var visuals) &&
               _visuals.CMUGetOffsets(visuals).TryGetValue(slot, out var offset)
            ? offset
            : Vector2.Zero;
    }

    private void ShowSlot()
    {
        var offset = CurrentOffset();
        _xEdit.Text = Format(offset.X * PixelsPerTile);
        _yEdit.Text = Format(offset.Y * PixelsPerTile);
        _tiles.Text = $"= {Format(offset.X)}, {Format(offset.Y)} tiles";

        if (!TryGetVisuals(out var visuals))
            _status.Text = "This weapon has no AttachableHolderVisuals, so nothing on it is drawn.";
        else if (SelectedSlot is { } slot && !_visuals.CMUGetOffsets(visuals).ContainsKey(slot))
            _status.Text = "This slot has no offset yet, so its attachments aren't drawn. Nudging it adds one.";
        else
            _status.Text = "Right and up are positive. 1 px is 1/32 of a tile.";

        UpdateYaml();
    }

    private void SetOffset(Vector2 offset)
    {
        if (SelectedSlot is not { } slot || !TryGetVisuals(out var visuals))
            return;

        _visuals.CMUSetOffset(visuals, slot, offset);

        // Moving the slot redraws the real attachment over a preview in the same slot; put the preview back on top.
        if (_preview is { } preview && _previewSlot == slot)
            _visuals.CMUPreview(visuals, slot, preview);

        ShowSlot();
    }

    private void Nudge(int x, int y)
    {
        var step = _steps[Math.Clamp(_stepSelect.SelectedId, 0, _steps.Length - 1)] / PixelsPerTile;
        SetOffset(CurrentOffset() + new Vector2(x * step, y * step));
    }

    private void ApplyTyped()
    {
        if (!float.TryParse(_xEdit.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(_yEdit.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            ShowSlot();
            return;
        }

        SetOffset(new Vector2(x, y) / PixelsPerTile);
    }

    private void ResetSlot()
    {
        SetOffset(Vector2.Zero);
    }

    private void ShowPreview()
    {
        if (SelectedSlot is not { } slot ||
            _previewSelect.SelectedId < 0 || _previewSelect.SelectedId >= _previewIds.Count ||
            !TryGetVisuals(out var visuals))
        {
            return;
        }

        ClearPreview();
        var preview = _entities.SpawnEntity(_previewIds[_previewSelect.SelectedId], MapCoordinates.Nullspace);
        if (!_visuals.CMUPreview(visuals, slot, preview))
        {
            _entities.DeleteEntity(preview);
            return;
        }

        _preview = preview;
        _previewSlot = slot;
        ShowSlot();
    }

    private void ClearPreview()
    {
        if (_preview is { } preview)
        {
            if (_previewSlot is { } slot && TryGetVisuals(out var visuals))
                _visuals.CMUClearPreview(visuals, slot);

            _entities.DeleteEntity(preview);
        }

        _preview = null;
        _previewSlot = null;
    }

    private string BuildYaml()
    {
        var yaml = new StringBuilder();
        yaml.AppendLine("  - type: AttachableHolderVisuals");
        yaml.AppendLine("    offsets:");
        if (TryGetVisuals(out var visuals))
        {
            foreach (var (slot, offset) in _visuals.CMUGetOffsets(visuals).OrderBy(pair => pair.Key, StringComparer.Ordinal))
                yaml.AppendLine($"      {slot}: {Format(offset.X)}, {Format(offset.Y)}");
        }

        return yaml.ToString().TrimEnd();
    }

    private void UpdateYaml()
    {
        _yaml.Text = BuildYaml();
    }

    private void CopyYaml()
    {
        _clipboard.SetText(BuildYaml());
        _status.Text = "Copied. Paste it over the weapon's AttachableHolderVisuals component.";
    }

    private static string Format(float value)
    {
        return value.ToString("0.#####", CultureInfo.InvariantCulture);
    }

    public override void Close()
    {
        ClearPreview();
        base.Close();
    }
}
