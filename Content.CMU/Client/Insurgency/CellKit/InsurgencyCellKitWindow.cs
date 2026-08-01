using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.Insurgency.CellKit;

/// <summary>
///     Lists the cell kit's remaining deployables, each with its sprite, name, and a Deploy button.
///     Deploying starts a do-after server-side; the list refreshes when the entry is consumed.
/// </summary>
public sealed class InsurgencyCellKitWindow : DefaultWindow
{
    private readonly IPrototypeManager _prototype;
    private readonly BoxContainer _rows;

    public event Action<int>? OnDeploy;

    public InsurgencyCellKitWindow()
    {
        _prototype = IoCManager.Resolve<IPrototypeManager>();

        Title = Loc.GetString("insfor-cell-kit-title");
        MinSize = new Vector2(420, 460);

        _rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_rows);
        Contents.AddChild(scroll);
    }

    public void SetEntries(List<string> entries, List<string> names)
    {
        _rows.RemoveAllChildren();

        if (entries.Count == 0)
        {
            _rows.AddChild(new Label { Text = Loc.GetString("insfor-cell-kit-empty"), Margin = new Thickness(8) });
            return;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var proto = entries[i];
            var index = i;

            var panel = new PanelContainer { Margin = new Thickness(0, 0, 0, 4) };
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(6) };

            if (_prototype.HasIndex<EntityPrototype>(proto))
            {
                var view = new EntityPrototypeView { MinSize = new Vector2(48, 48) };
                view.SetPrototype(new EntProtoId(proto));
                row.AddChild(view);
            }

            // Prefer the faction-authored display name (e.g. the vendor's name); fall back to the proto's.
            var name = i < names.Count && !string.IsNullOrWhiteSpace(names[i])
                ? names[i]
                : _prototype.TryIndex<EntityPrototype>(proto, out var p) ? p.Name : proto;
            row.AddChild(new Label { Text = name, HorizontalExpand = true, Margin = new Thickness(8, 0), VerticalAlignment = VAlignment.Center });

            var deploy = new Button { Text = Loc.GetString("insfor-cell-kit-deploy"), VerticalAlignment = VAlignment.Center };
            deploy.OnPressed += _ => OnDeploy?.Invoke(index);
            row.AddChild(deploy);

            panel.AddChild(row);
            _rows.AddChild(panel);
        }
    }
}
