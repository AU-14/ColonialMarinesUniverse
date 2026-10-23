using System.Linq;
using System.Reflection;
using Content.Client._RMC14.Requisitions;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Requisitions;
using Content.Shared.Access.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Requisitions;

// BUG-948: every rebuild of the item browser spawned a client dummy entity per item just to
// read its icon, which froze the client for seconds and could kill the list mid-build
public sealed class ASRSClientBrowserTest : InteractionTest
{
    [Test]
    public async Task ItemBrowserFillsIconsOnceAndRebuildsWithoutSpawningEntities()
    {
        await SpawnTarget("CMASRSConsoleColony");
        await Server.WaitPost(() =>
        {
            var console = SEntMan.GetEntity(Target!.Value);
            SEntMan.RemoveComponent<AccessReaderComponent>(console);
            SEntMan.RemoveComponent<ActivatableUIRequiresAccessComponent>(console);
        });
        await RunTicks(5);

        await Activate();
        await RunTicks(120);
        Assert.That(IsUiOpen(RequisitionsUIKey.Key));

        var rows = GetWindow<RequisitionsWindow>().ItemizedView.ItemsContainer.Children.OfType<RequisitionsItemRow>().ToList();
        Assert.That(rows, Has.Count.GreaterThan(100));
        Assert.That(rows.All(row => row.ItemIcon.Textures.Count > 0), Is.True, "Some rows never got their icon.");

        await Client.WaitPost(() =>
        {
            var ui = CEntMan.System<SharedUserInterfaceSystem>();
            Assert.That(ui.TryGetOpenUi(CEntMan.GetEntity(Target!.Value), RequisitionsUIKey.Key, out var bui));
            var rebuild = bui!.GetType().GetMethod("RebuildItemizedBrowser", BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

            var spawned = 0;
            void Count(Entity<MetaDataComponent> _) => spawned++;
            CEntMan.EntityAdded += Count;
            try
            {
                rebuild.Invoke(bui, null);
            }
            finally
            {
                CEntMan.EntityAdded -= Count;
            }

            Assert.That(spawned, Is.Zero, "Rebuilding the browser spawned client entities for its icons again.");
        });
    }
}
