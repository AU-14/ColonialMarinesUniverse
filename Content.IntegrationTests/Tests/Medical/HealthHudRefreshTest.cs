using System.Reflection;
using Content.Client.Overlays;
using Content.Shared.Inventory.Events;
using Content.Shared.Overlays;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
public sealed class HealthHudRefreshTest
{
    [Test]
    public async Task RefreshReplacesCachedConfiguration()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var healthBars = client.System<ShowHealthBarsSystem>();
        var healthIcons = client.System<ShowHealthIconsSystem>();
        var overlayManager = client.ResolveDependency<IOverlayManager>();

        await client.WaitAssertion(() =>
        {
            try
            {
                UpdateHud(healthBars, new ShowHealthBarsComponent
                {
                    DamageContainers = ["Biological"],
                    HealthStatusIcon = "HealthIconFine",
                });
                UpdateHud(healthIcons, new ShowHealthIconsComponent
                {
                    DamageContainers = ["Biological"],
                });

                var overlay = overlayManager.GetOverlay<EntityHealthBarOverlay>();
                Assert.Multiple(() =>
                {
                    Assert.That(overlay.DamageContainers, Is.EquivalentTo(new[] { "Biological" }));
                    Assert.That(overlay.StatusIcon, Is.Not.Null);
                    Assert.That(healthIcons.DamageContainers, Is.EquivalentTo(new[] { "Biological" }));
                });

                // Refresh while still active. Deactivation already clears these collections,
                // so this is the transition that regressed.
                UpdateHud(healthBars, new ShowHealthBarsComponent
                {
                    DamageContainers = ["Inorganic"],
                    HealthStatusIcon = "HealthIconFine",
                });
                UpdateHud(healthIcons, new ShowHealthIconsComponent
                {
                    DamageContainers = ["Inorganic"],
                });

                Assert.Multiple(() =>
                {
                    Assert.That(overlay.DamageContainers, Is.EquivalentTo(new[] { "Inorganic" }));
                    Assert.That(healthIcons.DamageContainers, Is.EquivalentTo(new[] { "Inorganic" }));
                });

                // An empty active refresh verifies that all cached presentation,
                // including the optional status icon, is reset before rebuilding.
                UpdateHud<ShowHealthBarsComponent>(healthBars);
                UpdateHud<ShowHealthIconsComponent>(healthIcons);

                Assert.Multiple(() =>
                {
                    Assert.That(overlay.DamageContainers, Is.Empty);
                    Assert.That(overlay.StatusIcon, Is.Null);
                    Assert.That(healthIcons.DamageContainers, Is.Empty);
                });
            }
            finally
            {
                healthBars.Deactivate();
                healthIcons.Deactivate();
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void UpdateHud<T>(EquipmentHudSystem<T> system, params T[] components)
        where T : IComponent
    {
        var refresh = new RefreshEquipmentHudEvent<T>(default)
        {
            Active = true,
        };
        refresh.Components.AddRange(components);

        // Exercise the singleton system's real active-update path so Deactivate
        // can perform its normal overlay and pooled-test cleanup afterward.
        var update = typeof(EquipmentHudSystem<T>).GetMethod(
            "Update",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Equipment HUD update method was not found.");

        update.Invoke(system, [refresh]);
    }
}
