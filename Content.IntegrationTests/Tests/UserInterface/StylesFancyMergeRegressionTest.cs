using System.Numerics;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.Tests.UserInterface;

[TestFixture]
public sealed class StylesFancyMergeRegressionTest : GameTest
{
    [Test]
    public async Task NamedSheetsAndCrtPreviewResetKeepGlobalNanoSheetCoherent()
    {
        await Client.WaitAssertion(() =>
        {
            var manager = Client.ResolveDependency<IStylesheetManager>();
            var ui = Client.ResolveDependency<IUserInterfaceManager>();

            Assert.Multiple(() =>
            {
                Assert.That(manager.TryGetStylesheet("Nanotrasen", out var nanotrasen), Is.True);
                Assert.That(nanotrasen, Is.SameAs(manager.SheetNanotrasen));
                Assert.That(manager.TryGetStylesheet("System", out var system), Is.True);
                Assert.That(system, Is.SameAs(manager.SheetSystem));
                Assert.That(manager.TryGetStylesheet("missing", out _), Is.False);
#pragma warning disable CS0618
                Assert.That(ui.Stylesheet, Is.SameAs(manager.SheetNano),
                    "the global legacy UI must continue to use the configured CRT-aware nano sheet");
#pragma warning restore CS0618
            });

            try
            {
#pragma warning disable CS0618
                var initial = manager.SheetNano;
                manager.PreviewCrtUi(enabled: false, CCVars.CrtUiColorRed);
                var disabled = manager.SheetNano;
                Assert.Multiple(() =>
                {
                    Assert.That(StyleNano.CrtUiEnabled, Is.False);
                    Assert.That(disabled, Is.Not.SameAs(initial));
                    Assert.That(ui.Stylesheet, Is.SameAs(disabled));
                    Assert.That(StyleNano.CrtGreen, Is.EqualTo(StyleNano.NanoGold));
                });

                manager.PreviewCrtUi(enabled: true, CCVars.CrtUiColorBlue);
                var blue = manager.SheetNano;
                Assert.Multiple(() =>
                {
                    Assert.That(StyleNano.CrtUiEnabled, Is.True);
                    Assert.That(blue, Is.Not.SameAs(disabled));
                    Assert.That(ui.Stylesheet, Is.SameAs(blue));
                    Assert.That(StyleNano.CrtGreen, Is.Not.EqualTo(StyleNano.NanoGold));
                });

                manager.ResetCrtUiPreview();
                Assert.Multiple(() =>
                {
                    Assert.That(StyleNano.CrtUiEnabled, Is.EqualTo(Client.CfgMan.GetCVar(CCVars.CrtUiEnabled)));
                    Assert.That(manager.SheetNano, Is.Not.SameAs(blue));
                    Assert.That(ui.Stylesheet, Is.SameAs(manager.SheetNano));
                });
#pragma warning restore CS0618
            }
            finally
            {
                manager.ResetCrtUiPreview();
            }
        });
    }

    [Test]
    public async Task FancyWindowResolvesNamedSheetAndRetainsCmuCloseAppearance()
    {
        await Client.WaitAssertion(() =>
        {
            var manager = Client.ResolveDependency<IStylesheetManager>();
            var window = new FancyWindow
            {
                Stylesheet = "System",
                AllowDraggingOutsideParentBounds = true
            };

            try
            {
                var control = (Control) window;
                Assert.Multiple(() =>
                {
                    Assert.That(control.Stylesheet, Is.SameAs(manager.SheetSystem));
                    Assert.That(window.Stylesheet, Is.EqualTo("System"));
                    Assert.That(window.AllowDraggingOutsideParentBounds, Is.True);
                });

                var size = new Vector2(31, 17);
                window.SetCloseButtonAppearance(Color.Purple, size);
                var closeButton = window.FindControl<TextureButton>("CloseButton");
                Assert.Multiple(() =>
                {
                    Assert.That(closeButton.Visible, Is.True);
                    Assert.That(closeButton.Disabled, Is.False);
                    Assert.That(closeButton.MinSize, Is.EqualTo(size));
                    Assert.That(closeButton.SetSize, Is.EqualTo(size));
                    Assert.That(closeButton.ModulateSelfOverride, Is.EqualTo(Color.Purple));
                });
            }
            finally
            {
                window.Dispose();
            }
        });
    }
}
