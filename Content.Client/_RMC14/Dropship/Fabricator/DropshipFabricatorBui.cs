using Content.Client.Message;
// CMU14 Start: CRT theme, weapon filter and search
using System.Linq;
using Content.Client.CMU14.Dropship.Fabricator;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Shared._RMC14.Dropship.Weapon;
// CMU14 End
using Content.Shared._RMC14.Dropship.Fabricator;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using static Content.Shared._RMC14.Dropship.Fabricator.DropshipFabricatorPrintableComponent;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._RMC14.Dropship.Fabricator;

[UsedImplicitly]
public sealed partial class DropshipFabricatorBui : BoundUserInterface
{
    private const int QueueRowHeight = 28;

    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    [ViewVariables]
    private DropshipFabricatorWindow? _window;

    private readonly DropshipFabricatorSystem _system;

    // CMU14 Start: weapon filter and search state
    private readonly List<CMUDropshipFabricatorEntry> _entries = new();
    private readonly List<(CMUDropshipFabricatorEntry Entry, PanelContainer Row)> _rows = new();
    private readonly List<(CMUDropshipFabricatorFilter Filter, Button Button)> _filterButtons = new();
    private CMUDropshipFabricatorFilter _filter = CMUDropshipFabricatorFilter.All;
    private string _search = string.Empty;
    // CMU14 End

    public DropshipFabricatorBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        _system = EntMan.System<DropshipFabricatorSystem>();
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DropshipFabricatorWindow>();

        // CMU14 Start: catalog grouped by weapon, filter sidebar, search bar, CRT theme
        _entries.Clear();
        _rows.Clear();
        _filterButtons.Clear();
        _filter = CMUDropshipFabricatorFilter.All;
        _search = string.Empty;

        foreach (var id in _system.Printables)
        {
            if (!_prototypes.TryIndex(id, out var printableProto) ||
                !id.TryGet(out var printable, _prototypes, _compFactory))
            {
                continue;
            }

            _entries.Add(new CMUDropshipFabricatorEntry(
                id,
                printableProto.Name,
                printable.Cost,
                printable.Category,
                GetWeapon(printableProto)));
        }

        foreach (var entry in _entries)
        {
            var row = CreateRow(entry);
            _rows.Add((entry, row));

            if (entry.Category == CategoryType.Equipment)
                _window.EquipmentContainer.AddChild(row);
            else
                _window.AmmoContainer.AddChild(row);
        }

        CreateFilterButtons();

        _window.SearchBar.OnTextChanged += args =>
        {
            _search = args.Text;
            ApplyFilters();
        };

        CrtLobbyTheme.ApplyWindow(_window, useCrtTypography: true);
        ApplyFilters();
        Refresh();
        // CMU14 End
    }

    // CMU14 Start: filter, search and row helpers
    private string? GetWeapon(EntityPrototype proto)
    {
        if (proto.TryGetComponent(out DropshipWeaponComponent? _, _compFactory))
            return proto.Name;

        if (proto.TryGetComponent(out DropshipAmmoComponent? ammo, _compFactory) &&
            !string.IsNullOrEmpty(ammo.Weapon.Id) &&
            _prototypes.TryIndex(ammo.Weapon, out var weapon))
        {
            return weapon.Name;
        }

        return null;
    }

    private void CreateFilterButtons()
    {
        if (_window == null)
            return;

        var group = new ButtonGroup();
        var filters = new List<CMUDropshipFabricatorFilter> { CMUDropshipFabricatorFilter.All };
        filters.AddRange(CMUDropshipFabricatorCatalog.GetWeapons(_entries).Select(CMUDropshipFabricatorFilter.ForWeapon));

        if (_entries.Any(e => e.Weapon == null))
            filters.Add(CMUDropshipFabricatorFilter.Support);

        foreach (var filter in filters)
        {
            var button = new Button
            {
                Group = group,
                ToggleMode = true,
                Pressed = filter == _filter,
                HorizontalExpand = true,
                ClipText = true,
                ToolTip = filter.Weapon,
            };
            button.OnPressed += _ =>
            {
                _filter = filter;
                ApplyFilters();
            };

            _filterButtons.Add((filter, button));
            _window.FilterContainer.AddChild(button);
        }
    }

    private PanelContainer CreateRow(CMUDropshipFabricatorEntry entry)
    {
        var info = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
            Children =
            {
                new Label { Text = entry.Name, ClipText = true, ToolTip = entry.Name },
            },
        };

        if (entry.Category == CategoryType.Ammo && entry.Weapon != null)
        {
            info.AddChild(new Label
            {
                Text = Loc.GetString("cmu-dropship-fabricator-item-weapon", ("weapon", entry.Weapon)),
                StyleClasses = { StyleNano.StyleClassCrtDimText },
                ClipText = true,
            });
        }

        var button = new Button
        {
            Text = Loc.GetString("rmc-dropship-fabricator-fabricate", ("cost", entry.Cost)),
            MinWidth = 120,
            VerticalAlignment = Control.VAlignment.Center,
        };
        button.OnPressed += _ => SendPredictedMessage(new DropshipFabricatorPrintMsg(entry.Id));

        return new PanelContainer
        {
            HorizontalExpand = true,
            StyleClasses = { StyleNano.StyleClassCrtTableCell },
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Margin = new Thickness(4, 2),
                    SeparationOverride = 6,
                    Children = { info, button },
                },
            },
        };
    }

    private void ApplyFilters()
    {
        if (_window is not { Disposed: false })
            return;

        var equipment = 0;
        var ammo = 0;
        foreach (var (entry, row) in _rows)
        {
            row.Visible = CMUDropshipFabricatorCatalog.IsVisible(entry, _filter, _search);
            if (!row.Visible)
                continue;

            var alt = entry.Category == CategoryType.Equipment ? equipment++ % 2 == 1 : ammo++ % 2 == 1;
            row.RemoveStyleClass(alt ? StyleNano.StyleClassCrtTableCell : StyleNano.StyleClassCrtTableCellAlt);
            row.AddStyleClass(alt ? StyleNano.StyleClassCrtTableCellAlt : StyleNano.StyleClassCrtTableCell);
        }

        _window.EquipmentLabel.Text = Loc.GetString("cmu-dropship-fabricator-equipment", ("count", equipment));
        _window.AmmoLabel.Text = Loc.GetString("cmu-dropship-fabricator-ammo", ("count", ammo));
        _window.EquipmentEmptyLabel.Visible = equipment == 0;
        _window.AmmoEmptyLabel.Visible = ammo == 0;

        foreach (var (filter, button) in _filterButtons)
        {
            var count = CMUDropshipFabricatorCatalog.Count(_entries, filter, _search);
            button.Text = filter.Kind switch
            {
                CMUDropshipFabricatorFilterKind.Weapon => Loc.GetString("cmu-dropship-fabricator-filter-weapon",
                    ("weapon", filter.Weapon ?? string.Empty),
                    ("count", count)),
                CMUDropshipFabricatorFilterKind.Support => Loc.GetString("cmu-dropship-fabricator-filter-support",
                    ("count", count)),
                _ => Loc.GetString("cmu-dropship-fabricator-filter-all", ("count", count)),
            };
        }
    }
    // CMU14 End

    public void Refresh()
    {
        if (_window is not { Disposed: false })
            return;

        if (!EntMan.TryGetComponent(Owner, out DropshipFabricatorComponent? fabricator))
            return;

        _window.PointsLabel.Text = Loc.GetString("rmc-dropship-fabricator-points",
            ("points", fabricator.Points));

        if (fabricator.Printing is { } printing)
        {
            _window.CurrentLabel.SetMarkupPermissive(Loc.GetString("rmc-dropship-fabricator-current",
                ("item", GetPrintableName(printing))));
        }
        else
        {
            _window.CurrentLabel.SetMarkupPermissive(Loc.GetString("rmc-dropship-fabricator-idle"));
        }

        _window.QueueLabel.SetMarkupPermissive(Loc.GetString("rmc-dropship-fabricator-queue",
            ("count", fabricator.Queue.Count),
            ("max", fabricator.MaxQueue)));

        _window.QueueContainer.ReleaseChildren();
        if (fabricator.Queue.Count == 0)
        {
            var empty = new Label
            {
                Text = Loc.GetString("rmc-dropship-fabricator-queue-empty"),
                Margin = new Thickness(4, 2),
                HorizontalExpand = true,
                SetHeight = QueueRowHeight,
                StyleClasses = { StyleNano.StyleClassCrtDimText }, // CMU14: CRT theme
            };
            _window.QueueContainer.AddChild(empty);
            CrtLobbyTheme.Apply(_window.QueueContainer); // CMU14: CRT theme
            return;
        }

        for (var i = 0; i < fabricator.Queue.Count; i++)
        {
            var entry = fabricator.Queue[i];
            var index = i;

            var label = new Label
            {
                Text = Loc.GetString("rmc-dropship-fabricator-queue-entry",
                    ("position", i + 1),
                    ("item", GetPrintableName(entry.Id)),
                    ("cost", entry.Cost)),
                Margin = new Thickness(4, 2),
                HorizontalExpand = false,
                VerticalAlignment = Control.VAlignment.Center,
            };

            var cancel = new Button
            {
                Text = Loc.GetString("rmc-dropship-fabricator-cancel"),
                // CMU14: OpenBoth removed, it ties with the CRT button style
                MinWidth = 90,
                SetHeight = 24,
            };
            cancel.OnPressed += _ => SendPredictedMessage(new DropshipFabricatorCancelQueueMsg(index));

            var container = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Margin = new Thickness(0, 2),
                Children =
                {
                    label,
                    new Control { HorizontalExpand = true },
                    cancel
                },
                HorizontalExpand = true,
                SetHeight = QueueRowHeight,
            };
            _window.QueueContainer.AddChild(container);
        }

        CrtLobbyTheme.Apply(_window.QueueContainer); // CMU14: CRT theme
    }

    private string GetPrintableName(EntProtoId<DropshipFabricatorPrintableComponent> id)
    {
        return _prototypes.TryIndex(id, out var proto)
            ? proto.Name
            : id.ToString();
    }
}