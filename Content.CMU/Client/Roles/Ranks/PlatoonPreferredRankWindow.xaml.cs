using System.Linq;
using System.Numerics;
using Content.Shared._CMU14.Roles.Ranks;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Client.Lobby.UI;

namespace Content.Client._CMU14.Roles.Ranks;

public sealed class PlatoonRankPreferenceWindow : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private readonly TabContainer _tabs;
    private readonly Dictionary<string, string?> _selections = new();

    public event Action<Dictionary<string, string?>>? OnSave;

    public PlatoonRankPreferenceWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("cmu14-rank-preference-window-title");
        MinSize = new Vector2(920, 660);
        SetSize = new Vector2(920, 660);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _tabs = new TabContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(_tabs);

        var saveButton = new Button
        {
            Text = Loc.GetString("cmu14-rank-preference-save"),
            HorizontalAlignment = HAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0)
        };
        saveButton.OnPressed += _ => Save();
        root.AddChild(saveButton);

        Contents.AddChild(root);
        CrtLobbyTheme.ApplyWindow(this);
    }

    /// <summary>
    /// Populates the window with one tab per platoon that has resolvable ranks for this job.
    /// currentPreferences maps platoonId -> currently selected rankId (null = Auto).
    /// </summary>
    public void PopulateSingleJob(PlatoonRankPreferenceJobEntry job, Dictionary<string, string?> currentPreferences)
    {
        _tabs.RemoveAllChildren();
        _selections.Clear();
        Title = job.JobName;

        if (job.Platoons.Count == 0)
        {
            var empty = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            empty.AddChild(new Label
            {
                Text = Loc.GetString("cmu14-rank-preference-no-ranks"),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            });
            _tabs.AddChild(empty);
            _tabs.SetTabTitle(0, job.JobName);
            return;
        }

        foreach (var platoonOptions in job.Platoons)
        {
            currentPreferences.TryGetValue(platoonOptions.PlatoonId, out var currentPreference);
            AddPlatoonTab(platoonOptions, currentPreference);
        }
    }

    private void AddPlatoonTab(PlatoonRankOptions platoonOptions, string? currentPreference)
    {
        var scroll = new ScrollContainer
        {
            HScrollEnabled = false,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(4),
            HorizontalExpand = true,
        };

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 4),
            HorizontalExpand = true,
        };

        if (platoonOptions.PatchPath is { } patchPath)
        {
            try
            {
                var tex = _resourceCache.GetResource<TextureResource>(patchPath);
                header.AddChild(new TextureRect
                {
                    Texture = tex.Texture,
                    Stretch = TextureRect.StretchMode.KeepAspectCentered,
                    TextureScale = new Vector2(2f, 2f),
                    VerticalAlignment = VAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                });
            }
            catch
            {
                // Patch texture missing - degrade gracefully, name label still renders.
            }
        }

        header.AddChild(new Label
        {
            Text = platoonOptions.PlatoonName,
            VerticalAlignment = VAlignment.Center,
            StyleClasses = { "LabelHeading" },
        });

        vbox.AddChild(header);

        vbox.AddChild(new PanelContainer
        {
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 6),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#444444"),
                ContentMarginTopOverride = 1,
                ContentMarginBottomOverride = 1,
            },
        });

        var toggles = new List<(string? RankId, Button Button)>();

        void SelectRank(string? rankId)
        {
            _selections[platoonOptions.PlatoonId] = rankId;
            foreach (var (id, button) in toggles)
                button.Pressed = id == rankId;
        }

        var autoButton = MakeSelectButton();
        autoButton.OnPressed += _ => SelectRank(null);
        toggles.Add((null, autoButton));
        vbox.AddChild(BuildRow(null, Loc.GetString("cmu14-rank-preference-auto"), null, true, null, autoButton));

        foreach (var rank in platoonOptions.Ranks)
        {
            var button = MakeSelectButton();
            if (!rank.Unlocked)
            {
                button.Disabled = true;
                button.Modulate = Color.FromHex("#888888");
            }
            else
            {
                button.OnPressed += _ => SelectRank(rank.RankId);
            }
            toggles.Add((rank.RankId, button));

            vbox.AddChild(BuildRow(rank.ChevronEntity, rank.RankName, rank.Paygrade, rank.Unlocked, rank.RequirementsText, button));
        }

        var validPreference = toggles
            .FirstOrDefault(t => t.RankId == currentPreference && (t.RankId == null || platoonOptions.Ranks.FirstOrDefault(r => r.RankId == currentPreference)?.Unlocked == true));

        SelectRank(validPreference != default ? currentPreference : null);

        scroll.AddChild(vbox);
        _tabs.AddChild(scroll);

        // Keep text title as tooltip/accessibility fallback; the header inside the tab is the real label.
        _tabs.SetTabTitle(_tabs.ChildCount - 1, platoonOptions.PlatoonName);
    }

    private BoxContainer BuildRow(
        EntProtoId? chevron,
        string rankName,
        string? paygrade,
        bool unlocked,
        string? requirementsText,
        Button selectButton)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 2),
            ToolTip = requirementsText,
        };

        var icon = GetChevronIcon(chevron);
        if (icon != null)
        {
            row.AddChild(new TextureRect
            {
                Texture = icon,
                TextureScale = new Vector2(2, 2),
                VerticalAlignment = VAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            });
        }

        row.AddChild(new Label
        {
            Text = rankName,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            FontColorOverride = unlocked ? null : Color.Gray
        });

        row.AddChild(new Label
        {
            Text = paygrade ?? string.Empty,
            MinWidth = 60,
            VerticalAlignment = VAlignment.Center,
            FontColorOverride = unlocked ? null : Color.Gray
        });

        row.AddChild(selectButton);

        return row;
    }

    private Button MakeSelectButton()
    {
        return new Button
        {
            Text = Loc.GetString("cmu14-rank-preference-select"),
            ToggleMode = true,
            MinWidth = 70,
            VerticalAlignment = VAlignment.Center
        };
    }

    private Texture? GetChevronIcon(EntProtoId? entProtoId)
    {
        if (entProtoId == null)
            return null;

        if (!_prototypeManager.TryIndex((string)entProtoId, out EntityPrototype? proto))
            return null;

        var textures = SpriteComponent.GetPrototypeTextures(proto, _resourceCache, out _).ToList();
        return textures.Count > 0 ? textures[0].Default : null;
    }

    private void Save()
    {
        OnSave?.Invoke(new Dictionary<string, string?>(_selections));
    }
}