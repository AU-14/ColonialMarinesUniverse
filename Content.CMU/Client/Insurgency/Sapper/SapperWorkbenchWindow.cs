using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Insurgency.Sapper;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Insurgency.Sapper;

public sealed class SapperWorkbenchWindow : DefaultWindow
{
    private readonly IPrototypeManager _prototype;
    private readonly Button _gunsmithingButton;
    private readonly Button _craftingButton;
    private readonly BoxContainer _body;
    private SapperWorkbenchBuiState? _state;
    private WorkbenchTab _tab = WorkbenchTab.Gunsmithing;

    public event Action<string>? OnAddAttachment;
    public event Action<string>? OnRemoveAttachment;
    public event Action? OnTakeWeapon;
    public event Action<int>? OnCraft;
    public event Action<string>? OnEjectMaterial;

    public SapperWorkbenchWindow()
    {
        _prototype = IoCManager.Resolve<IPrototypeManager>();

        Title = Loc.GetString("insfor-sapper-workbench-window-title");
        MinSize = new Vector2(720, 520);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var tabs = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 8),
        };

        _gunsmithingButton = new Button
        {
            Text = Loc.GetString("insfor-sapper-workbench-tab-gunsmithing"),
            HorizontalExpand = true,
        };
        _gunsmithingButton.OnPressed += _ =>
        {
            _tab = WorkbenchTab.Gunsmithing;
            Rebuild();
        };
        tabs.AddChild(_gunsmithingButton);

        // "Fabrication", not "Trap Crafting": the tab also builds the spy-camera net, the siphon
        // rig, and the Switch chip.
        _craftingButton = new Button
        {
            Text = Loc.GetString("insfor-sapper-workbench-tab-fabrication"),
            HorizontalExpand = true,
        };
        _craftingButton.OnPressed += _ =>
        {
            _tab = WorkbenchTab.Crafting;
            Rebuild();
        };
        tabs.AddChild(_craftingButton);

        root.AddChild(tabs);

        _body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(_body);

        Contents.AddChild(root);
    }

    public void SetState(SapperWorkbenchBuiState state)
    {
        _state = state;
        Rebuild();
    }

    private void Rebuild()
    {
        _body.RemoveAllChildren();
        _gunsmithingButton.Pressed = _tab == WorkbenchTab.Gunsmithing;
        _craftingButton.Pressed = _tab == WorkbenchTab.Crafting;

        if (_state == null)
            return;

        if (_tab == WorkbenchTab.Gunsmithing)
            BuildGunsmithing(_state);
        else
            BuildCrafting(_state);

        // Matches the improved construction menu; runs per rebuild, already-styled controls skip.
        InsforUiStyle.Apply(this);
    }

    private void BuildGunsmithing(SapperWorkbenchBuiState state)
    {
        // The weapon on the bench, drawn big and centered above its slots.
        if (state.WeaponPrototype != null && _prototype.HasIndex<EntityPrototype>(state.WeaponPrototype))
        {
            var view = new EntityPrototypeView
            {
                MinSize = new Vector2(160, 96),
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4),
                Scale = new Vector2(3, 3),
            };
            view.SetPrototype(new EntProtoId(state.WeaponPrototype));
            _body.AddChild(view);
        }

        var weaponRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 8),
        };

        weaponRow.AddChild(new Label
        {
            Text = state.WeaponName ?? Loc.GetString("insfor-sapper-workbench-no-weapon-loaded"),
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        });

        var take = new Button
        {
            Text = Loc.GetString("insfor-sapper-workbench-take-weapon-button"),
            Disabled = state.WeaponName == null,
        };
        take.OnPressed += _ => OnTakeWeapon?.Invoke();
        weaponRow.AddChild(take);
        _body.AddChild(weaponRow);

        var columns = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        _body.AddChild(columns);

        var slotsPanel = CreatePanel(Loc.GetString("insfor-sapper-workbench-attachment-slots"));
        slotsPanel.HorizontalExpand = true;
        columns.AddChild(slotsPanel);

        foreach (var slot in state.Slots)
            AddSlotRow(slotsPanel, slot);

        if (state.Slots.Count == 0)
        {
            slotsPanel.AddChild(new Label
            {
                Text = Loc.GetString("insfor-sapper-workbench-load-weapon-slots"),
                Margin = new Thickness(4),
            });
        }

        var statsPanel = CreatePanel(Loc.GetString("insfor-sapper-workbench-buffs-debuffs"));
        statsPanel.HorizontalExpand = true;
        statsPanel.Margin = new Thickness(8, 0, 0, 0);
        columns.AddChild(statsPanel);

        if (state.Stats.Count == 0)
        {
            statsPanel.AddChild(new Label
            {
                Text = Loc.GetString("insfor-sapper-workbench-no-modifiers"),
                Margin = new Thickness(4),
            });
            return;
        }

        foreach (var stat in state.Stats)
        {
            statsPanel.AddChild(new Label
            {
                Text = stat.Text,
                Modulate = stat.Buff ? Color.Green : Color.Red,
                Margin = new Thickness(4, 2),
            });
        }
    }

    private void AddSlotRow(BoxContainer parent, SapperWorkbenchSlotState slot)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(4, 2),
        };

        var attachment = slot.AttachmentName ?? Loc.GetString("insfor-sapper-workbench-empty");
        row.AddChild(new Label
        {
            Text = Loc.GetString(
                "insfor-sapper-workbench-slot",
                ("slot", slot.SlotName),
                ("attachment", attachment)),
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        });

        var add = new Button
        {
            Text = Loc.GetString("insfor-sapper-workbench-add"),
            Disabled = !slot.CanAdd,
            MinWidth = 70,
        };
        add.OnPressed += _ => OnAddAttachment?.Invoke(slot.SlotId);
        row.AddChild(add);

        var remove = new Button
        {
            Text = Loc.GetString("insfor-sapper-workbench-remove"),
            Disabled = !slot.CanRemove,
            MinWidth = 82,
        };
        remove.OnPressed += _ => OnRemoveAttachment?.Invoke(slot.SlotId);
        row.AddChild(remove);

        parent.AddChild(row);
    }

    private void BuildCrafting(SapperWorkbenchBuiState state)
    {
        var materialsPanel = CreatePanel(Loc.GetString("insfor-sapper-workbench-materials"));
        materialsPanel.Margin = new Thickness(0, 0, 0, 8);
        _body.AddChild(materialsPanel);

        if (state.Materials.Count == 0)
        {
            materialsPanel.AddChild(new Label
            {
                Text = Loc.GetString("insfor-sapper-workbench-no-materials-loaded"),
                Margin = new Thickness(4),
            });
        }

        foreach (var material in state.Materials)
        {
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(4, 1),
            };
            row.AddChild(new Label
            {
                Text = Loc.GetString(
                    "insfor-sapper-workbench-material",
                    ("name", material.Name),
                    ("count", material.Count)),
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
            });

            var eject = new Button
            {
                Text = Loc.GetString("insfor-sapper-workbench-eject"),
                MinWidth = 60,
            };
            var id = material.Id;
            eject.OnPressed += _ => OnEjectMaterial?.Invoke(id);
            row.AddChild(eject);
            materialsPanel.AddChild(row);
        }

        BuildIngredientsHelp(state);

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
            HScrollEnabled = false,
        };
        var recipes = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };
        scroll.AddChild(recipes);
        _body.AddChild(scroll);

        foreach (var recipe in state.Recipes)
            AddRecipeRow(recipes, recipe);
    }

    // Small "Loose Ingredients Help": every distinct loose ingredient the recipes use, with its
    // sprite, so players know what to scavenge and lay on the bench.
    private void BuildIngredientsHelp(SapperWorkbenchBuiState state)
    {
        var seen = new HashSet<string>();
        var rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
        foreach (var recipe in state.Recipes)
        {
            foreach (var ing in recipe.Ingredients)
            {
                if (!seen.Add(ing.Name))
                    continue;

                var entry = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 0, 10, 0) };
                if (ing.Icon != null && _prototype.HasIndex<EntityPrototype>(ing.Icon))
                {
                    var view = new EntityPrototypeView { MinSize = new Vector2(24, 24) };
                    view.SetPrototype(new EntProtoId(ing.Icon));
                    entry.AddChild(view);
                }
                entry.AddChild(new Label { Text = ing.Name, Modulate = Color.Gray, VerticalAlignment = VAlignment.Center, Margin = new Thickness(2, 0, 0, 0) });
                rows.AddChild(entry);
            }
        }

        if (seen.Count == 0)
            return;

        var help = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(4, 0, 4, 6) };
        help.AddChild(new Label
        {
            Text = Loc.GetString("insfor-sapper-workbench-loose-help"),
            StyleClasses = { "LabelKeyText" },
        });
        help.AddChild(new Label
        {
            Text = Loc.GetString("insfor-sapper-workbench-loose-help-description"),
            Modulate = Color.Gray,
        });
        help.AddChild(rows);
        _body.AddChild(help);
    }

    private void AddRecipeRow(BoxContainer parent, SapperWorkbenchRecipeState recipe)
    {
        var panel = new PanelContainer { Margin = new Thickness(0, 0, 0, 4) };
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(6),
        };

        if (_prototype.HasIndex<EntityPrototype>(recipe.Prototype))
        {
            var view = new EntityPrototypeView { MinSize = new Vector2(48, 48) };
            view.SetPrototype(new EntProtoId(recipe.Prototype));
            row.AddChild(view);
        }

        var nameAndCost = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(8, 0),
        };
        // ClipText keeps long cost lines from forcing the row wider than the window, which used to
        // push the Craft button out of view.
        nameAndCost.AddChild(new Label { Text = recipe.Name, ClipText = true });
        nameAndCost.AddChild(new Label
        {
            Text = FormatMaterials(recipe.Materials),
            Modulate = Color.Gray,
            ClipText = true,
        });

        // Loose-item ingredients get their sprites drawn inline.
        if (recipe.Ingredients.Count > 0)
        {
            var ingredients = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            foreach (var ing in recipe.Ingredients)
            {
                if (ing.Icon != null && _prototype.HasIndex<EntityPrototype>(ing.Icon))
                {
                    var view = new EntityPrototypeView { MinSize = new Vector2(20, 20) };
                    view.SetPrototype(new EntProtoId(ing.Icon));
                    ingredients.AddChild(view);
                }
                ingredients.AddChild(new Label
                {
                    Text = Loc.GetString("insfor-sapper-workbench-ingredient", ("count", ing.Count), ("name", ing.Name)),
                    Modulate = Color.Gray,
                    VerticalAlignment = VAlignment.Center,
                    Margin = new Thickness(2, 0, 8, 0),
                });
            }
            nameAndCost.AddChild(ingredients);
        }

        row.AddChild(nameAndCost);

        var craft = new Button
        {
            Text = Loc.GetString("insfor-sapper-workbench-craft"),
            Disabled = !recipe.CanBuild,
            VerticalAlignment = VAlignment.Center,
            MinWidth = 60,
        };
        craft.OnPressed += _ => OnCraft?.Invoke(recipe.Index);
        row.AddChild(craft);

        panel.AddChild(row);
        parent.AddChild(panel);
    }

    private static BoxContainer CreatePanel(string title)
    {
        var panel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
        };

        panel.AddChild(new Label
        {
            Text = title,
            Margin = new Thickness(4, 0, 4, 4),
        });

        return panel;
    }

    private static string FormatMaterials(Dictionary<string, int> materials)
    {
        return materials.Count == 0
            ? Loc.GetString("insfor-sapper-workbench-no-materials")
            : string.Join(", ", materials.Select(kvp => Loc.GetString(
                "insfor-sapper-workbench-material-cost",
                ("name", kvp.Key),
                ("count", kvp.Value))));
    }

    private enum WorkbenchTab : byte
    {
        Gunsmithing,
        Crafting,
    }
}
