using System.Collections;
using System.Reflection;
using Content.Client.Info;
using Content.Client.Options.UI.Tabs;
using Content.Client.Stylesheets;
using Content.IntegrationTests.Fixtures;
using Content.Shared._CMU14.Input;
using Content.Shared._RMC14.Input;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;

namespace Content.IntegrationTests.Tests.UserInterface;

[TestFixture]
public sealed class RulesKeyRebindMergeRegressionTest : GameTest
{
    [Test]
    public async Task RulesWindowComposesCrtPanelTabsTutorialAndCvarLifecycle()
    {
        await Client.WaitAssertion(() =>
        {
            var originalEnabled = Client.CfgMan.GetCVar(CCVars.CrtUiEnabled);
            var originalColor = Client.CfgMan.GetCVar(CCVars.CrtUiColor);
            var styles = Client.ResolveDependency<IStylesheetManager>();
            var window = new RulesAndInfoWindow();

            try
            {
                var contents = window.FindControl<Control>("ContentsContainer");
                var panel = contents.Children.OfType<PanelContainer>().Single();
                var tabs = panel.Children.OfType<TabContainer>().Single();
                var rules = tabs.Children.OfType<RulesControl>().Single();
                var tutorial = tabs.Children.OfType<Info>().Single();
                var controls = tutorial.InfoContainer.Children.OfType<InfoControlsSection>().Single();
                var sections = tutorial.InfoContainer.Children.OfType<InfoSection>().ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(rules, Is.Not.Null);
                    Assert.That(controls, Is.Not.Null);
                    Assert.That(sections, Has.Length.EqualTo(2),
                        "the active tutorial must contain Gameplay and Sandbox without the retired Intro section");
                    Assert.That(sections.Select(section => Descendants(section).OfType<Label>().Single().Text),
                        Is.EquivalentTo(new[]
                    {
                        Loc.GetString("ui-info-header-gameplay"),
                        Loc.GetString("ui-info-header-sandbox")
                    }));
                    Assert.That(window.Stylesheet, Is.SameAs(styles.SheetNano));
                });

                Client.CfgMan.SetCVar(CCVars.CrtUiColor,
                    originalColor == CCVars.CrtUiColorBlue ? CCVars.CrtUiColorRed : CCVars.CrtUiColorBlue);
                Client.CfgMan.SetCVar(CCVars.CrtUiEnabled, !originalEnabled);
                Assert.That(window.Stylesheet, Is.SameAs(styles.SheetNano),
                    "both CRT callbacks must reapply the current rebuilt nano sheet to the live window");
            }
            finally
            {
                window.Dispose();
                Client.CfgMan.SetCVar(CCVars.CrtUiColor, originalColor);
                Client.CfgMan.SetCVar(CCVars.CrtUiEnabled, originalEnabled);
            }
        });
    }

    [Test]
    public async Task KeyRebindIncludesForkKeysAndEightFilteredHumanEmotePickersWithoutLeakingDummy()
    {
        await Client.WaitAssertion(() =>
        {
            var ui = Client.ResolveDependency<IUserInterfaceManager>();
            var humanCount = CountPrototype(CEntMan, "MobHuman");
            var tab = new KeyRebindTab();
            ui.StateRoot.AddChild(tab);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tab.IsInsideTree, Is.True,
                        "the options tab must exercise its normal entered-tree lifecycle");
                    Assert.That(CountPrototype(CEntMan, "MobHuman"), Is.EqualTo(humanCount),
                        "constructing key options must delete its real-human emote filter dummy");
                });

                var keyControls = (IDictionary) typeof(KeyRebindTab)
                    .GetField("_keyControls", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(tab)!;
                BoundKeyFunction[] requiredForkKeys =
                [
                    CMKeyFunctions.RMCActivateAttachableBarrel,
                    CMKeyFunctions.RMCActivateAttachableRail,
                    CMKeyFunctions.RMCActivateAttachableStock,
                    CMKeyFunctions.RMCActivateAttachableUnderbarrel,
                    CMKeyFunctions.RMCFieldStripHeldItem,
                    CMKeyFunctions.RMCCycleFireMode,
                    CMKeyFunctions.CMUniqueAction,
                    CMKeyFunctions.CMHolsterPrimary,
                    CMKeyFunctions.CMHolsterSecondary,
                    CMKeyFunctions.CMHolsterTertiary,
                    CMKeyFunctions.CMHolsterQuaternary,
                    CMKeyFunctions.RMCPickUpDroppedItems,
                    CMKeyFunctions.RMCInteractWithOtherHand,
                    CMKeyFunctions.RMCRest,
                    CMUKeyFunctions.CMUCycleBodyZoneTarget,
                    CMUKeyFunctions.CMUCycleBodyZoneTargetReverse,
                    CMUKeyFunctions.CMUTargetBodyZoneHead,
                    CMUKeyFunctions.CMUTargetBodyZoneTorso,
                    CMUKeyFunctions.CMUTargetBodyZoneLeftArm,
                    CMUKeyFunctions.CMUTargetBodyZoneRightArm,
                    CMUKeyFunctions.CMUTargetBodyZoneLeftLeg,
                    CMUKeyFunctions.CMUTargetBodyZoneRightLeg,
                    CMUKeyFunctions.CMUOpenMedicalCraftingMenu,
                    CMUKeyFunctions.CMUToggleShootDownZLevel,
                    CMKeyFunctions.CMXenoWideSwing,
                    CMKeyFunctions.RMCXenoRest,
                    CMUKeyFunctions.CMUEmoteSlot1,
                    CMUKeyFunctions.CMUEmoteSlot2,
                    CMUKeyFunctions.CMUEmoteSlot3,
                    CMUKeyFunctions.CMUEmoteSlot4,
                    CMUKeyFunctions.CMUEmoteSlot5,
                    CMUKeyFunctions.CMUEmoteSlot6,
                    CMUKeyFunctions.CMUEmoteSlot7,
                    CMUKeyFunctions.CMUEmoteSlot8
                ];
                Assert.Multiple(() =>
                {
                    Assert.That(requiredForkKeys.Distinct().Count(), Is.EqualTo(requiredForkKeys.Length));
                    foreach (var key in requiredForkKeys)
                        Assert.That(keyControls.Contains(key), Is.True, $"missing fork key {key}");
                });

                var emotePickers = Descendants(tab).OfType<OptionButton>().ToArray();
                Assert.That(emotePickers, Has.Length.EqualTo(8));
                var firstChoices = PickerChoices(emotePickers[0]);
                var emoteChoices = firstChoices.Skip(1).ToArray();
                Assert.That(firstChoices, Is.Not.Empty);
                Assert.Multiple(() =>
                {
                    Assert.That(firstChoices[0], Is.EqualTo(Loc.GetString("cmu-ui-options-emote-slot-none")));
                    Assert.That(emoteChoices, Is.Not.Empty);
                    Assert.That(emoteChoices, Is.Ordered.Using<string>(StringComparer.Ordinal));
                    Assert.That(emotePickers, Has.All.Matches<OptionButton>(picker =>
                        picker.ItemCount == firstChoices.Length && PickerChoices(picker).SequenceEqual(firstChoices)),
                        "each persistent emote slot must expose the same filtered, sorted choices plus None");
                });
            }
            finally
            {
                tab.Dispose();
            }

            Assert.Multiple(() =>
            {
                Assert.That(tab.IsInsideTree, Is.False);
                Assert.That(CountPrototype(CEntMan, "MobHuman"), Is.EqualTo(humanCount),
                    "disposing the live options tab must not leave its emote filter dummy behind");
            });
        });
    }

    private static string[] PickerChoices(OptionButton picker)
    {
        return Descendants(picker.OptionsScroll)
            .OfType<Button>()
            .Where(button => button.ToggleMode)
            .Select(button => button.Text)
            .ToArray();
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static int CountPrototype(IEntityManager entities, string prototypeId)
    {
        var count = 0;
        var query = entities.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var metadata))
        {
            if (metadata.EntityPrototype?.ID == prototypeId)
                count++;
        }

        return count;
    }
}
