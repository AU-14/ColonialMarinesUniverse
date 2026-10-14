using Content.Server.Atmos.EntitySystems;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Timing;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Components;
using Content.Shared._RMC14.Stamina;
using Content.Shared._RMC14.StatusEffect;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaResistanceParityTest
{
    private static readonly Robust.Shared.Prototypes.ProtoId<DamageModifierSetPrototype> CMUYautjaPrototype = "CMUYautja";

    [Test]
    public async Task YautjaDamageModifierSetMatchesCmss13SpeciesValues()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);

        try
        {
            await server.WaitAssertion(() =>
            {
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var modifiers = prototypes.Index<DamageModifierSetPrototype>(CMUYautjaPrototype).Coefficients;

                Assert.That(modifiers, Has.Count.EqualTo(5));
                Assert.That(modifiers["Blunt"], Is.EqualTo(0.28f));
                Assert.That(modifiers["Slash"], Is.EqualTo(0.28f));
                Assert.That(modifiers["Piercing"], Is.EqualTo(0.28f));
                Assert.That(modifiers["Heat"], Is.EqualTo(0.65f));
                Assert.That(modifiers["Poison"], Is.EqualTo(0f));
                Assert.That(modifiers.ContainsKey("Shock"), Is.False);
                Assert.That(modifiers.ContainsKey("Cold"), Is.False);
                Assert.That(modifiers.ContainsKey("Caustic"), Is.False);
                Assert.That(modifiers.ContainsKey("Radiation"), Is.False);
                Assert.That(modifiers.ContainsKey("Bloodloss"), Is.False);
                Assert.That(modifiers.ContainsKey("Asphyxiation"), Is.False);
            });
        }
        finally
        {
            server.Dispose();
        }

    }

    [Test]
    public async Task FireContactAndBurnTicksKeepYautjaHeatResistanceAndStackDecay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var hunter = server.EntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var fire = server.System<SharedRMCFlammableSystem>();
            var damageable = server.System<DamageableSystem>();
            fire.DamageFromFire(hunter, new DamageSpecifier { DamageDict = { ["Heat"] = 30 } });
            Assert.That(damageable.GetTotalDamage(hunter).Float(), Is.EqualTo(19.5f).Within(0.01));
            damageable.ClearAllDamage(hunter);

            Assert.That(fire.Ignite(hunter, 30, 20, null), Is.True);
            var flammable = server.EntMan.GetComponent<FlammableComponent>(hunter);
            var initialStacks = flammable.FireStacks;
            flammable.NextUpdate = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<FlammableSystem>().RefreshUpdateSnapshot();
            server.System<FlammableSystem>().Update(0);
            Assert.Multiple(() =>
            {
                Assert.That(damageable.GetTotalDamage(hunter).Float(), Is.EqualTo(flammable.Damage.GetTotal().Float() * 6 * 0.65f).Within(0.02),
                    "Ongoing fire must retain the same species resistance as contact damage.");
                Assert.That(flammable.FireStacks, Is.EqualTo(initialStacks - 2),
                    "Hunters retain their two-stack passive fire decay.");
            });
            server.EntMan.DeleteEntity(hunter);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task YautjaHasNoStaminaAndUsesCmss13StatusDurations()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        EntityUid yautja = default;

        try
        {
            await server.WaitPost(() =>
            {
                yautja = server.EntMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            });

            await server.WaitAssertion(() =>
            {
                Assert.That(server.EntMan.HasComponent<StaminaComponent>(yautja), Is.False);
                Assert.That(server.EntMan.HasComponent<RMCStaminaComponent>(yautja), Is.False);

                var stun = new RMCStatusEffectTimeEvent("Stun", TimeSpan.FromSeconds(3));
                server.EntMan.EventBus.RaiseLocalEvent(yautja, ref stun);
                Assert.That(stun.Duration, Is.EqualTo(TimeSpan.FromSeconds(2)));

                var unconscious = new RMCStatusEffectTimeEvent("Unconscious", TimeSpan.FromSeconds(3));
                server.EntMan.EventBus.RaiseLocalEvent(yautja, ref unconscious);
                Assert.That(unconscious.Duration, Is.EqualTo(TimeSpan.Zero),
                    "Regular Yautja retain CMU's unconsciousness immunity.");
            });
        }
        finally
        {
            server.Dispose();
        }
    }
}
