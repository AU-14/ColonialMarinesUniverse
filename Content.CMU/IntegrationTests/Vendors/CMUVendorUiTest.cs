using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._RMC14.Vendors;
using Content.Client.CMU14.Vendors;
using Content.Client.CMU14.Interface;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Vendors;

[TestFixture, NonParallelizable]
public sealed class CMUVendorUiTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Dirty = true };

    [TestCase(false, "en-US", false)]
    [TestCase(true, "en-US", false)]
    [TestCase(false, "ru-RU", false)]
    [TestCase(true, "ru-RU", false)]
    [TestCase(true, "ru-RU", true)]
    public async Task EquipmentRowsRemainSeparateAfterFilteringAndResizing(bool crt, string culture, bool yautja)
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var update = ui.GetType().GetMethod("FrameUpdate")!.CreateDelegate<Action<FrameEventArgs>>(ui);
            var localization = Client.ResolveDependency<ILocalizationManager>();
            localization.SetCulture(CultureInfo.GetCultureInfo(culture));
            Client.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CrtUiEnabled, crt);
            var window = new CMAutomatedVendorWindow();
            if (yautja)
                window.UseYautjaTheme();
            var tube = Descendants(window).OfType<CrtScreenControl>().Single();
            Assert.That(tube.Roll, Is.True, "Vendor terminals retain the requested CRT roll effect");
            Assert.That(tube.Grain, Is.True, "Vendor terminals retain animated CRT grain");
            ui.WindowRoot.AddChild(window);
            try
            {
                var sections = new List<CMAutomatedVendorSection>();
                for (var sectionIndex = 0; sectionIndex < 3; sectionIndex++)
                {
                    var section = new CMAutomatedVendorSection();
                    section.Label.SetMessage($"Equipment category {sectionIndex}");
                    for (var index = 0; index < 8; index++)
                    {
                        var entry = new CMAutomatedVendorEntry();
                        if (yautja)
                            entry.UseYautjaTheme();
                        entry.SetTitle($"{index}: " + string.Concat(Enumerable.Repeat(
                            "Портфель со знаками различия старшего сержанта подразделения BEAR ", 3)));
                        entry.SetStock(12, 24, 1, 2, 25);
                        entry.SetAvailability(false, false,
                            localization.GetString("cmu-vendor-reason-points", ("count", 10)));
                        section.Entries.AddChild(entry);
                    }

                    sections.Add(section);
                    window.Sections.AddChild(section);
                }

                window.SetCategories(["First", "Second", "Third"]);
                foreach (var size in new[] { new Vector2(800, 735), new Vector2(720, 540), new Vector2(1000, 650) })
                {
                    window.SetSize = size;
                    for (var selected = -1; selected < sections.Count; selected++)
                    {
                        for (var index = 0; index < sections.Count; index++)
                        {
                            sections[index].Visible = selected == -1 || index == selected;
                            foreach (var row in sections[index].Entries.Children)
                                row.Visible = selected == -1 || row.GetPositionInParent() < 2;
                        }

                        for (var frame = 0; frame < 12; frame++)
                            update(new FrameEventArgs(1f / 60f));

                        foreach (var section in sections.Where(section => section.Visible))
                        {
                            var rows = section.Entries.Children.OfType<CMAutomatedVendorEntry>()
                                .Where(row => row.Visible).ToArray();
                            for (var index = 0; index < rows.Length; index++)
                            {
                                var row = rows[index];
                                Assert.That(row.Height, Is.GreaterThanOrEqualTo(row.Texture.MinHeight),
                                    "A visible row must not collapse to zero height");
                                Assert.That(row.Height, Is.GreaterThanOrEqualTo(row.Texture.Height),
                                    $"Row must reserve its icon height at {size}, category {selected}");
                                Assert.That(row.Panel.Button.Width, Is.GreaterThan(0), "The issue button must remain visible");
                                if (index > 0)
                                    Assert.That(row.Position.Y, Is.GreaterThanOrEqualTo(rows[index - 1].Position.Y + rows[index - 1].Height),
                                        "Equipment rows must never overlap");
                                var title = Descendants(row).OfType<RichTextLabel>().Single(label => label.Name == "ItemTitle");
                                Assert.That(title.Height, Is.GreaterThan(0), "Visible equipment must have readable text");
                                Assert.That(title.DesiredSize.X, Is.LessThanOrEqualTo(title.Width + 0.1f),
                                    "Item text must be measured at the width it is actually given");
                                Assert.That(title.GlobalRect.Right, Is.LessThanOrEqualTo(row.Panel.GlobalRect.Left),
                                    "The issue action must have its own space beside the item name");
                                Assert.That(title.GlobalPosition.Y + title.Height,
                                    Is.LessThanOrEqualTo(row.Stock.GlobalPosition.Y + 0.1f),
                                    "Wrapped names must not overlap stock information");
                            }
                        }
                    }
                }

                foreach (var section in sections)
                {
                    section.Visible = true;
                    foreach (var row in section.Entries.Children)
                        row.Visible = true;
                }

                for (var frame = 0; frame < 12; frame++)
                    update(new FrameEventArgs(1f / 60f));
                window.InventoryScroll.VScroll = 120;
                var previousScroll = window.InventoryScroll.VScroll;
                var first = (CMAutomatedVendorEntry) sections[0].Entries.GetChild(0);
                first.SetStock(11, 24, 1, 2, 25);
                for (var frame = 0; frame < 12; frame++)
                    update(new FrameEventArgs(1f / 60f));
                Assert.That(window.InventoryScroll.VScroll, Is.EqualTo(previousScroll).Within(0.1f),
                    "A stock update must preserve the reader's scroll position");
            }
            finally
            {
                ui.WindowRoot.RemoveChild(window);
            }
        });
    }

    [TestCase("en-US")]
    [TestCase("ru-RU")]
    public async Task EveryVendorCategoryResolvesInTheSelectedLanguage(string culture)
    {
        await Client.WaitAssertion(() =>
        {
            var localization = Client.ResolveDependency<ILocalizationManager>();
            localization.SetCulture(CultureInfo.GetCultureInfo(culture));
            var prototypes = Client.ResolveDependency<IPrototypeManager>();
            var factory = Client.ResolveDependency<IComponentFactory>();
            var names = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(prototype => !prototype.Abstract && !Pair.IsTestPrototype(prototype))
                .Select(prototype => prototype.TryComp<CMAutomatedVendorComponent>(out var vendor, factory) ? vendor : null)
                .Where(vendor => vendor != null)
                .SelectMany(vendor => vendor!.Sections.Select(section => section.Name))
                .ToHashSet();
            Assert.That(names, Is.Not.Empty);
            Assert.Multiple(() =>
            {
                foreach (var name in names.Order())
                {
                    var text = CMUVendorLocalization.GetSectionName(name, localization);
                    Assert.That(text, Is.Not.Empty, name);
                    Assert.That(text, Does.Not.Contain("vendor-section-"), name);
                    if (culture == "ru-RU")
                        Assert.That(text, Does.Match(@"\p{IsCyrillic}"), $"Untranslated category: {name}");
                }
            });
        });
    }

    [TestCase("en-US")]
    [TestCase("ru-RU")]
    public async Task EveryExplicitItemLabelUsesTheSelectedLanguage(string culture)
    {
        await Client.WaitAssertion(() =>
        {
            var localization = Client.ResolveDependency<ILocalizationManager>();
            localization.SetCulture(CultureInfo.GetCultureInfo(culture));
            var prototypes = Client.ResolveDependency<IPrototypeManager>();
            var factory = Client.ResolveDependency<IComponentFactory>();
            var entries = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(prototype => !prototype.Abstract && !Pair.IsTestPrototype(prototype))
                .Select(prototype => prototype.TryComp<CMAutomatedVendorComponent>(out var vendor, factory) ? vendor : null)
                .Where(vendor => vendor != null)
                .SelectMany(vendor => vendor!.Sections.SelectMany(section => section.Entries))
                .Where(entry => entry.Name != null)
                .GroupBy(entry => entry.Name).Select(group => group.First()).ToArray();
            Assert.That(entries, Is.Not.Empty);
            Assert.Multiple(() =>
            {
                foreach (var entry in entries)
                {
                    if (!prototypes.TryIndex(entry.Id, out var prototype))
                        continue;

                    var text = CMUVendorLocalization.GetItemName(entry, prototype, localization);
                    Assert.That(text, Is.Not.Empty, entry.Name);
                    Assert.That(text, Does.Not.Contain("cmu-vendor-item-"), entry.Name);
                    if (culture == "ru-RU")
                        Assert.That(text, Does.Match(@"\p{IsCyrillic}"), $"Untranslated item override: {entry.Name}");
                }
            });
        });
    }

    [TestCase("en-US")]
    [TestCase("ru-RU")]
    public async Task StockLimitsPricesAndUnavailableReasonsRemainDistinct(string culture)
    {
        await Client.WaitAssertion(() =>
        {
            var localization = Client.ResolveDependency<ILocalizationManager>();
            localization.SetCulture(CultureInfo.GetCultureInfo(culture));
            var entry = new CMAutomatedVendorEntry();
            entry.SetStock(null, null, 1, 2, 25);
            Assert.That(entry.Stock.GetMessage(), Does.Contain("∞"), "A personal limit must not turn infinite shared stock into finite stock");
            Assert.That(entry.Stock.GetMessage(), Does.Contain("1 / 2"));
            Assert.That(entry.Stock.GetMessage(), Does.Contain("25"));

            var reason = localization.GetString("cmu-vendor-reason-personal-limit");
            entry.SetStock(0, 10, 0, 2, null);
            entry.SetAvailability(true, false, reason);
            Assert.That(entry.Stock.GetMessage(), Does.Contain("0 / 10"));
            Assert.That(entry.Stock.GetMessage(), Does.Contain("0 / 2"));
            Assert.That(entry.Stock.GetMessage(), Does.Not.Contain("∞"));
            Assert.That(entry.Panel.Button.Disabled, Is.True);
            Assert.That(Descendants(entry).OfType<Label>().Single(label => label.Name == "Availability").Text,
                Does.Contain(reason));

            entry.SetAvailability(false, false, null);
            Assert.That(entry.Panel.Button.Disabled, Is.False);
            Assert.That(Descendants(entry).OfType<Label>().Single(control => control.Name == "Availability").Text, Is.Empty,
                "Available equipment keeps an empty status slot to preserve row positions");
        });
    }

    [Test]
    public async Task ReplicatedStockAndPointsUpdateFiltersWithoutReplacingRows()
    {
        await Client.WaitAssertion(() =>
        {
            var localization = Client.ResolveDependency<ILocalizationManager>();
            localization.SetCulture(CultureInfo.GetCultureInfo("en-US"));
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var owner = CEntMan.Spawn(null, MapCoordinates.Nullspace);
            var vendor = CEntMan.EnsureComponent<CMAutomatedVendorComponent>(owner);
            var original = new CMVendorEntry { Id = "CMTricordrazineAutoInjector", Amount = 8, Max = 8 };
            vendor.Sections = [new CMVendorSection { Name = "Equipment", Entries = [original] }];
            var window = new CMAutomatedVendorWindow();
            ui.WindowRoot.AddChild(window);
            using var bui = new CMAutomatedVendorBui(owner, CMAutomatedVendorUI.Key);
            typeof(CMAutomatedVendorBui).GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(bui, window);
            try
            {
                bui.Refresh();
                var section = (CMAutomatedVendorSection) window.Sections.GetChild(0);
                var row = (CMAutomatedVendorEntry) section.Entries.GetChild(0);
                window.LowStockOnly.Pressed = true;
                bui.Refresh();
                Assert.That(row.Visible, Is.False, "Full stock must not match the low-stock filter");

                // Network state replaces section/entry objects even when the catalog shape is unchanged.
                vendor.Sections = [new CMVendorSection { Name = "Equipment", Entries = [original with { Amount = 2 }] }];
                bui.Refresh();
                Assert.That(section.Entries.GetChild(0), Is.SameAs(row), "Stock changes must preserve rows and scroll state");
                Assert.That(row.Entry!.Amount, Is.EqualTo(2));
                Assert.That(row.Visible, Is.True, "The filter must read the replacement stock entry");

                vendor.Sections[0].Entries[0] = original with { Amount = 0 };
                bui.Refresh();
                Assert.That(row.Visible, Is.False, "Empty stock has a separate state from low stock");
                window.LowStockOnly.Pressed = false;
                bui.Refresh();
                Assert.That(row.Panel.Button.Disabled, Is.True);

                vendor.UseObjectivePoints = true;
                vendor.CachedFactionWinPoints = 3;
                vendor.Sections[0].Entries[0] = original with { Amount = 4, Points = 5 };
                bui.Refresh();
                Assert.That(row.Panel.Button.Disabled, Is.True);
                var status = Descendants(row).OfType<Label>().Single(label => label.Name == "Availability");
                Assert.That(status.Text, Does.Contain(localization.GetString("cmu-vendor-reason-points", ("count", 2))));
                vendor.CachedFactionWinPoints = 5;
                bui.Refresh();
                Assert.That(row.Panel.Button.Disabled, Is.False, "Faction points do not require a personal vendor account");
            }
            finally
            {
                ui.WindowRoot.RemoveChild(window);
                CEntMan.DeleteEntity(owner);
            }
        });
    }

    [TestCase("en-US")]
    [TestCase("ru-RU")]
    public async Task CategoryNamesFitAndStatusChangesKeepTheInventoryStill(string culture)
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var update = ui.GetType().GetMethod("FrameUpdate")!.CreateDelegate<Action<FrameEventArgs>>(ui);
            var localization = Client.ResolveDependency<ILocalizationManager>();
            localization.SetCulture(CultureInfo.GetCultureInfo(culture));
            var window = new CMAutomatedVendorWindow();
            var section = new CMAutomatedVendorSection();
            var first = new CMAutomatedVendorEntry();
            var second = new CMAutomatedVendorEntry();
            first.SetTitle("Equipment one");
            second.SetTitle("Equipment two");
            first.SetStock(5, 10, 1, 1, null);
            second.SetStock(5, 10, 1, 1, null);
            first.SetAvailability(false, false, null);
            section.Entries.AddChild(first);
            section.Entries.AddChild(second);
            window.Sections.AddChild(section);
            window.SetCategories(culture == "ru-RU"
                ? ["Запасное снаряжение", "Обязательный комплект огневой поддержки"]
                : ["Spare Equipment", "Mandatory fire support equipment"]);
            window.UpdateCategoryCount(-1, 28, true);
            window.UpdateCategoryCount(0, 11, true);
            window.UpdateCategoryCount(1, 3, true);
            ui.WindowRoot.AddChild(window);
            try
            {
                void Settle()
                {
                    for (var frame = 0; frame < 12; frame++)
                        update(new FrameEventArgs(1f / 60f));
                }

                foreach (var width in new[] { 720, 920 })
                {
                    window.SetSize = new Vector2(width, 650);
                    Settle();
                    foreach (var name in Descendants(window).OfType<RichTextLabel>().Where(label => label.Name == "CategoryName"))
                    {
                        var counter = name.Parent!.Children.OfType<Label>().Single(label => label.Name == "CategoryCount");
                        Assert.That(name.Width, Is.GreaterThan(0));
                        Assert.That(name.DesiredSize.X - name.Margin.SumHorizontal,
                            Is.LessThanOrEqualTo(name.Width + 0.1f), "Category text must wrap at its actual width");
                        Assert.That(name.GlobalRect.Right, Is.LessThanOrEqualTo(counter.GlobalRect.Left), "Names and counts have separate columns");
                        Assert.That(name.GlobalRect.Left, Is.GreaterThanOrEqualTo(name.Parent.GlobalRect.Left));
                    }

                    var rowSize = first.Size;
                    var nextPosition = second.GlobalPosition;
                    var viewport = window.InventoryScroll.Size;
                    foreach (var reason in new[] { "Out of stock", string.Concat(Enumerable.Repeat("Личный лимит выдачи исчерпан. ", 8)), null })
                    {
                        first.SetAvailability(reason != null, false, reason);
                        window.SetFeedback(reason ?? localization.GetString("cmu-vendor-footer"));
                        Settle();
                        Assert.That(first.Size, Is.EqualTo(rowSize), "A status must not resize the equipment row");
                        Assert.That(second.GlobalPosition, Is.EqualTo(nextPosition), "A status must not move the next item");
                        Assert.That(window.InventoryScroll.Size, Is.EqualTo(viewport), "Feedback must not shrink the inventory viewport");
                        var status = Descendants(first).OfType<Label>().Single(label => label.Name == "Availability");
                        Assert.That(status.ToolTip, Is.EqualTo(reason ?? string.Empty), "Full reasons remain available when the single line is clipped");
                    }
                }
            }
            finally
            {
                ui.WindowRoot.RemoveChild(window);
            }
        });
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
