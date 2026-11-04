using System.Linq;
using Content.Client.CMU14.Yautja;
using Content.Client.CMU14.Interface; // CMU14: CRT vendor palette
using Content.Client.CMU14.Vendors; // CMU14: localized inventory categories
using Content.Shared._RMC14.Holiday;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Medical.Refill;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static System.StringComparison;
using static Robust.Client.UserInterface.Controls.LineEdit;

namespace Content.Client._RMC14.Vendors;

[UsedImplicitly]
public sealed partial class CMAutomatedVendorBui : BoundUserInterface
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ILocalizationManager _localization = default!; // CMU14

    private readonly SharedJobSystem _job;
    private readonly SharedMindSystem _mind;
    private readonly SharedRankSystem _rank;
    private readonly SharedRMCHolidaySystem _rmcHoliday;

    private CMAutomatedVendorWindow? _window;

    public CMAutomatedVendorBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _job = EntMan.System<SharedJobSystem>();
        _mind = EntMan.System<SharedMindSystem>();
        _rmcHoliday = EntMan.System<SharedRMCHolidaySystem>();
        _rank = EntMan.System<SharedRankSystem>();
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMAutomatedVendorWindow>();
        var vendorName = EntMan.GetComponentOrNull<MetaDataComponent>(Owner)?.EntityName
            ?? Loc.GetString("cmu-vendor-default-name"); // CMU14: localized fallback
        _window.Title = vendorName;
        _window.SetVendorName(vendorName); // CMU14: specific inventory terminal identity
        _window.ReagentsBar.ForegroundStyleBoxOverride = new StyleBoxFlat(Color.FromHex("#AF7F38"));

        if (EntMan.TryGetComponent(Owner, out CMAutomatedVendorComponent? vendor))
        {
            ApplyWindowStyle(vendor);
            RebuildSections(vendor, EntMan.GetComponentOrNull<CMVendorUserComponent>(_player.LocalEntity));
        }

        _window.Search.OnTextChanged += OnSearchChanged;
        // CMU14: optional low-stock filter is entirely client-side.
        _window.LowStockOnly.OnToggled += _ => ApplySearchFilter(_window?.Search.Text);
        // CMU14: additional catalog controls use the same client-side filtering path.
        _window.AvailableOnly.OnToggled += _ => ApplySearchFilter(_window?.Search.Text);
        _window.OnCategoryChanged += () => ApplySearchFilter(_window?.Search.Text);
        Refresh();
    }

    private bool NeedsSectionRebuild(CMAutomatedVendorComponent vendor)
    {
        if (_window == null)
            return false;

        if (_window.Sections.ChildCount != vendor.Sections.Count)
            return true;

        for (var sectionIndex = 0; sectionIndex < vendor.Sections.Count; sectionIndex++)
        {
            if (_window.Sections.GetChild(sectionIndex) is not CMAutomatedVendorSection uiSection ||
                uiSection.Entries.ChildCount != vendor.Sections[sectionIndex].Entries.Count ||
                uiSection.Section?.Name != vendor.Sections[sectionIndex].Name) // CMU14: category identity may change
            {
                return true;
            }

            // CMU14: stock updates retain rows; replacing an item must refresh its identity and callbacks.
            for (var entryIndex = 0; entryIndex < uiSection.Entries.ChildCount; entryIndex++)
            {
                var current = vendor.Sections[sectionIndex].Entries[entryIndex];
                if (uiSection.Entries.GetChild(entryIndex) is not CMAutomatedVendorEntry row ||
                    row.Entry?.Id != current.Id || row.Entry?.Name != current.Name)
                    return true;
            }
        }

        return false;
    }

    private void RebuildSections(CMAutomatedVendorComponent vendor, CMVendorUserComponent? user)
    {
        if (_window == null)
            return;

        _window.Sections.ReleaseChildren();

        for (var sectionIndex = 0; sectionIndex < vendor.Sections.Count; sectionIndex++)
        {
            var section = vendor.Sections[sectionIndex];
            var uiSection = new CMAutomatedVendorSection { Section = section };
            uiSection.Label.SetMessage(GetSectionName(user, section));
            uiSection.Visible = IsSectionValid(section);
            // CMU14: show a per-section inventory counter.
            uiSection.UpdateCount(section.Entries.Count, section.Entries.Count);
            ApplySectionStyle(vendor, uiSection);

            for (var entryIndex = 0; entryIndex < section.Entries.Count; entryIndex++)
            {
                var entry = section.Entries[entryIndex];
                // CMU14: retain the entry contract for client-side filtering and status chips.
                var uiEntry = new CMAutomatedVendorEntry { Entry = entry };
                if (vendor.UiStyle == CMVendorUiStyle.Yautja)
                    uiEntry.UseYautjaTheme();

                if (_prototype.TryIndex(entry.Id, out var entity))
                {
                    uiEntry.Texture.Textures = EntMan.System<SpriteSystem>().GetPrototypeTextures(entity)
                        .Select(o => o.Default)
                        .ToList();
                    if (entity.TryComp<SpriteComponent>(CompName.Get<SpriteComponent>(EntMan.ComponentFactory), out var entitySprites) &&
                        entitySprites.AllLayers.FirstOrDefault() is { } firstLayer)
                    {
                        uiEntry.Texture.Modulate = firstLayer.Color;
                    }

                    // CMU14: item identity is separate from the primary dispense button.
                    uiEntry.SetTitle(CMUVendorLocalization.GetItemName(entry, entity, _localization));

                    var name = entity.Name;
                    var color = CMAutomatedVendorPanel.DefaultColor;
                    var borderColor = CMAutomatedVendorPanel.DefaultBorderColor;
                    var hoverColor = CMAutomatedVendorPanel.DefaultBorderColor;
                    if (vendor.UiStyle == CMVendorUiStyle.Yautja)
                    {
                        color = YautjaBracerUiStyle.DeepCard;
                        borderColor = YautjaBracerUiStyle.MutedBorder;
                        hoverColor = YautjaBracerUiStyle.Row;
                    }

                    if (section.TakeAll != null || section.TakeOne != null || entry.Mandatory)
                    {
                        name = Loc.GetString("cmu-vendor-mandatory-item", ("name", name)); // CMU14: locale
                        color = vendor.UiStyle == CMVendorUiStyle.Yautja ? Color.FromHex("#240909") : Color.FromHex("#251A0C");
                        borderColor = vendor.UiStyle == CMVendorUiStyle.Yautja ? YautjaBracerUiStyle.HotRed : Color.FromHex("#805300");
                        hoverColor = borderColor;
                    }
                    else if (entry.Recommended)
                    {
                        uiEntry.SetTitle(Loc.GetString("cmu-vendor-recommended-title", ("name", uiEntry.ItemName))); // CMU14
                        name = Loc.GetString("cmu-vendor-recommended-item", ("name", name)); // CMU14: locale
                        color = vendor.UiStyle == CMVendorUiStyle.Yautja ? Color.FromHex("#200606") : Color.FromHex("#102919");
                        borderColor = vendor.UiStyle == CMVendorUiStyle.Yautja ? YautjaBracerUiStyle.Red : Color.FromHex("#3A9B52");
                        hoverColor = borderColor;
                    }

                    uiEntry.Panel.Color = color;
                    uiEntry.Panel.BorderColor = borderColor;
                    uiEntry.Panel.HoveredColor = hoverColor;

                    var msg = new FormattedMessage();
                    msg.AddText(name);
                    msg.PushNewline();

                    if (!string.IsNullOrWhiteSpace(entity.Description))
                        msg.AddText(entity.Description);

                    var tooltip = new Tooltip();
                    tooltip.SetMessage(msg);

                    uiEntry.TooltipLabel.ToolTip = entity.Description;
                    uiEntry.TooltipLabel.TooltipDelay = 0;
                    uiEntry.TooltipLabel.TooltipSupplier = _ => tooltip;

                    var sectionI = sectionIndex;
                    var entryI = entryIndex;
                    var linkedEntryIndexes = new List<int>();

                    foreach (var linkedEntry in entry.LinkedEntries)
                    {
                        var linkedEntryIndex = 0;
                        foreach (var vendorEntry in section.Entries)
                        {
                            if (vendorEntry.Id == linkedEntry)
                                linkedEntryIndexes.Add(linkedEntryIndex);

                            linkedEntryIndex++;
                        }
                    }

                    uiEntry.Panel.Button.OnPressed += _ => OnButtonPressed(sectionI, entryI, linkedEntryIndexes);
                }

                uiSection.Entries.AddChild(uiEntry);
            }

            _window.Sections.AddChild(uiSection);
        }

        // CMU14: navigation mirrors the actual vendor sections.
        _window.SetCategories(vendor.Sections.Select(section =>
            CMUVendorLocalization.GetSectionName(section.Name, _localization)).ToList());
    }

    private void ApplyWindowStyle(CMAutomatedVendorComponent vendor)
    {
        if (_window == null || vendor.UiStyle != CMVendorUiStyle.Yautja)
            return;

        _window.UseYautjaTheme(); // CMU14: preserve Yautja's original red palette
        _window.ReagentsBar.ForegroundStyleBoxOverride = YautjaBracerUiStyle.Flat(YautjaBracerUiStyle.Red, YautjaBracerUiStyle.HotRed);
        _window.PointsLabel.FontColorOverride = YautjaBracerUiStyle.HotRed;
    }

    private static void ApplySectionStyle(CMAutomatedVendorComponent vendor, CMAutomatedVendorSection section)
    {
        if (vendor.UiStyle != CMVendorUiStyle.Yautja)
            return;

        section.UseYautjaTheme(); // CMU14: preserve the special vendor skin
        section.Label.Modulate = YautjaBracerUiStyle.HotRed;
    }

    private bool IsSectionValid(CMVendorSection section)
    {
        var validJob = true;
        var validRank = true;
        if (_player.LocalSession != null && _mind.TryGetMind(_player.LocalSession.UserId, out var mindId))
        {
            foreach (var job in section.Jobs)
            {
                if (!_job.MindHasJobWithId(mindId, job.Id))
                    validJob = false;
                else
                {
                    validJob = true;
                    break;
                }
            }

            if (_player.LocalEntity != null)
            {
                foreach (var rank in section.Ranks)
                {
                    var userRank = _rank.GetRank(_player.LocalEntity.Value);
                    if (userRank is null || userRank != rank)
                        validRank = false;
                    else
                    {
                        validRank = true;
                        break;
                    }
                }
            }
        }

        var validHoliday = section.Holidays.Count == 0;
        foreach (var holiday in section.Holidays)
        {
            if (_rmcHoliday.IsActiveHoliday(holiday))
                validHoliday = true;
        }

        return validJob && validHoliday && validRank;
    }

    private void OnButtonPressed(int sectionIndex, int entryIndex, List<int> linkedEntryIndexes)
    {
        // CMU14: acknowledge the request; only the server decides whether dispensing succeeds.
        if (_window?.Sections.GetChild(sectionIndex) is CMAutomatedVendorSection section &&
            section.Entries.GetChild(entryIndex) is CMAutomatedVendorEntry entry)
            _window.SetFeedback(Loc.GetString("cmu-vendor-dispense-requested", ("name", entry.ItemName)));

        var msg = new CMVendorVendBuiMsg(sectionIndex, entryIndex, linkedEntryIndexes);
        SendPredictedMessage(msg);
    }

    private void OnSearchChanged(LineEditEventArgs args)
    {
        ApplySearchFilter(args.Text);
    }

    // CMU14 method: empty stock has its own status; positive stock needs a known maximum.
    private static bool IsLowStock(CMVendorEntry entry)
    {
        return entry.Amount is { } amount
            && amount > 0 && entry.Max is > 0 && amount * 4L <= entry.Max.Value;
    }

    // CMU14 Begin: section navigation, stock-only filters, and visible result totals.
    private void ApplySearchFilter(string? text)
    {
        if (_window == null)
            return;

        var shown = 0;
        var eligible = 0;
        var selectedTotal = 0;
        var category = 0;
        var query = text?.Trim();
        if (_window.SelectedCategory is >= 0 and var selected &&
            (_window.Sections.GetChild(selected) is not CMAutomatedVendorSection selectedSection ||
                selectedSection.Section is { } selectedData && !IsSectionValid(selectedData)))
            _window.SelectAllCategories();

        foreach (var control in _window.Sections.Children)
        {
            if (control is not CMAutomatedVendorSection section)
                continue;

            var sectionAllowed = section.Section == null || IsSectionValid(section.Section);
            if (sectionAllowed)
                eligible += section.Entries.ChildCount;
            var categorySelected = _window.SelectedCategory == -1 || _window.SelectedCategory == category;
            if (sectionAllowed && categorySelected)
                selectedTotal += section.Entries.ChildCount;
            _window.UpdateCategoryCount(category, section.Entries.ChildCount, sectionAllowed);

            var count = 0;
            foreach (var itemControl in section.Entries.Children)
            {
                if (itemControl is not CMAutomatedVendorEntry entry)
                    continue;

                var matches = string.IsNullOrEmpty(query)
                    || entry.ItemName.Contains(query, CurrentCultureIgnoreCase);
                var low = entry.Entry is { } stock && IsLowStock(stock);
                var available = !entry.Panel.Button.Disabled;
                entry.Visible = sectionAllowed
                    && categorySelected
                    && matches
                    && (!_window.LowStockOnly.Pressed || low)
                    && (!_window.AvailableOnly.Pressed || available);
                if (entry.Visible)
                    count++;
            }

            section.UpdateCount(count, section.Entries.ChildCount);
            section.Visible = count > 0;
            shown += count;
            category++;
        }

        _window.UpdateCategoryCount(-1, eligible, true);
        _window.UpdateShownCount(shown, selectedTotal);
    }
    // CMU14 End

    public void Refresh()
    {
        if (_window == null)
            return;

        if (!EntMan.TryGetComponent(Owner, out CMAutomatedVendorComponent? vendor))
            return;

        var anyEntryWithPoints = false;
        var user = EntMan.GetComponentOrNull<CMVendorUserComponent>(_player.LocalEntity);

        // For objective-point vendors, the balance comes from the faction win-point pool,
        // not the individual player's points.
        int userPoints;
        string pointsLabel;
        if (vendor.UseObjectivePoints)
        {
            userPoints = vendor.CachedFactionWinPoints;
            var faction = vendor.Faction.ToLowerInvariant() switch // CMU14: localized faction labels
            {
                "govfor" => Loc.GetString("cmu-vendor-faction-govfor"),
                "opfor" => Loc.GetString("cmu-vendor-faction-opfor"),
                "clf" => Loc.GetString("cmu-vendor-faction-clf"),
                "scientist" => Loc.GetString("cmu-vendor-faction-scientist"),
                _ => vendor.Faction.ToUpperInvariant(),
            };
            pointsLabel = Loc.GetString("cmu-vendor-faction-points",
                ("faction", faction), ("count", userPoints)); // CMU14: locale
        }
        else
        {
            userPoints = vendor.PointsType == null
                ? user?.Points ?? 0
                : user?.ExtraPoints?.GetValueOrDefault(vendor.PointsType) ?? 0;
            pointsLabel = Loc.GetString("cmu-vendor-points", ("count", userPoints)); // CMU14: locale
        }

        if (NeedsSectionRebuild(vendor))
            RebuildSections(vendor, user);

        for (var sectionIndex = 0; sectionIndex < vendor.Sections.Count; sectionIndex++)
        {
            var section = vendor.Sections[sectionIndex];
            var uiSection = (CMAutomatedVendorSection) _window.Sections.GetChild(sectionIndex);
            uiSection.Section = section; // CMU14: replicated section instances can be replaced without a rebuild
            uiSection.Label.SetMessage(GetSectionName(user, section));

            var sectionDisabled = false;
            if (section.Choices is { } choices)
            {
                if (user?.Choices.GetValueOrDefault(choices.Id) >= choices.Amount ||
                    user == null && choices.Amount <= 0)
                {
                    sectionDisabled = true;
                }
            }

            for (var entryIndex = 0; entryIndex < section.Entries.Count; entryIndex++)
            {
                var entry = section.Entries[entryIndex];
                var uiEntry = (CMAutomatedVendorEntry) uiSection.Entries.GetChild(entryIndex);
                uiEntry.Entry = entry; // CMU14: filters must use the latest replicated stock
                var personalRemaining = entry.MaxPerUser is { } maxPerUser
                    ? maxPerUser - (user?.PurchaseCounts.GetValueOrDefault(entry.Id.Id) ?? 0)
                    : (int?) null;
                // CMU14: entries can carry their own choice budget
                var entryChoicesExhausted = entry.Choices is { } entryChoice
                    && (user?.Choices.GetValueOrDefault(entryChoice.Id) >= entryChoice.Amount
                    || user == null && entryChoice.Amount <= 0);
                // CMU14 Begin: explain the existing stock, choice, personal and points restrictions.
                var takeAll = user?.TakeAll;
                var takeOne = user?.TakeOne;
                var alreadyTaken = section.TakeAll is { } takeAllId && takeAll?.Contains((takeAllId, entry.Id)) == true
                    || section.TakeOne is { } takeOneId && takeOne?.Contains(takeOneId) == true;
                var notEnoughPoints = false;
                if (entry.Points != null)
                {
                    anyEntryWithPoints = true;
                    notEnoughPoints = vendor.UseObjectivePoints
                        ? userPoints < entry.Points
                        : user == null || userPoints < entry.Points;
                }

                string? reason = null;
                if (entry.Amount is <= 0)
                    reason = Loc.GetString("cmu-vendor-out");
                else if (personalRemaining is { } remaining && remaining < entry.Spawn)
                    reason = Loc.GetString(remaining <= 0 ? "cmu-vendor-reason-personal-limit" : "cmu-vendor-reason-bundle-limit");
                else if (sectionDisabled || entryChoicesExhausted)
                    reason = Loc.GetString("cmu-vendor-reason-choice-limit");
                else if (alreadyTaken)
                    reason = Loc.GetString("cmu-vendor-reason-already-taken");
                else if (notEnoughPoints)
                {
                    reason = user == null && !vendor.UseObjectivePoints
                        ? Loc.GetString("cmu-vendor-reason-no-balance")
                        : Loc.GetString("cmu-vendor-reason-points", ("count", Math.Max(0, entry.Points!.Value - userPoints)));
                }

                uiEntry.SetStock(entry.Amount, entry.Max, personalRemaining, entry.MaxPerUser, entry.Points);
                uiEntry.SetAvailability(entry.Amount is <= 0, IsLowStock(entry), reason);
                // CMU14 End
            }
        }

        // CMU14 Begin: aggregate visible sections for the overview strip.
        var inventory = vendor.Sections
            .Where(IsSectionValid)
            .SelectMany(section => section.Entries)
            .ToList();
        _window.UpdateInventorySummary(inventory.Count, inventory.Count(IsLowStock),
            inventory.Count(entry => entry.Amount is <= 0));
        // CMU14 End
        ApplySearchFilter(_window.Search.Text);
        _window.PointsLabel.Text = anyEntryWithPoints ? pointsLabel : string.Empty;

        if (!EntMan.TryGetComponent(Owner, out CMSolutionRefillerComponent? refiller))
        {
            _window.SetReagentsVisible(false); // CMU14: hide the entire CRT reagents strip
            return;
        }

        _window.SetReagentsVisible(true); // CMU14: show the entire CRT reagents strip

        var current = refiller.Current;
        var max = refiller.Max;
        _window.ReagentsBar.MinValue = 0;
        _window.ReagentsBar.MaxValue = max.Int();
        _window.ReagentsBar.SetAsRatio((refiller.Current / refiller.Max).Float());
        _window.ReagentsLabel.Text = Loc.GetString("cmu-vendor-reagent-amount",
            ("current", current.Int()), ("max", max.Int())); // CMU14: locale
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        switch (message)
        {
            case CMVendorRefreshBuiMsg:
                Refresh();
                break;
        }
    }

    // CMU14 method: localized category identity and remaining selection instructions.
    private FormattedMessage GetSectionName(CMVendorUserComponent? user, CMVendorSection section)
    {
        var sectionName = CMUVendorLocalization.GetSectionName(section.Name, _localization);

        if (section.TakeAll != null)
        {
            var takeAll = user?.TakeAll;
            foreach (var entry in section.Entries)
            {
                if (takeAll == null || !takeAll.Contains((section.TakeAll, entry.Id)))
                {
                    sectionName = Loc.GetString("cmu-vendor-section-take-all", ("name", sectionName));
                    break;
                }
            }
        }
        else if (section.TakeOne != null)
        {
            var takeOne = user?.TakeOne;
            if (takeOne == null || !takeOne.Contains(section.TakeOne))
                sectionName = Loc.GetString("cmu-vendor-section-take-one", ("name", sectionName));
        }
        else if (section.Choices is { } choices)
        {
            if (user == null)
            {
                sectionName = Loc.GetString("cmu-vendor-section-choose", ("name", sectionName), ("count", choices.Amount));
            }
            else
            {
                var left = choices.Amount - user.Choices.GetValueOrDefault(choices.Id);
                if (left > 0)
                    sectionName = Loc.GetString("cmu-vendor-section-choose", ("name", sectionName), ("count", left));
            }
        }

        var name = new FormattedMessage();
        name.AddMarkupOrThrow($"[bold]{FormattedMessage.EscapeText(sectionName)}[/bold]");
        return name;
    }
}
