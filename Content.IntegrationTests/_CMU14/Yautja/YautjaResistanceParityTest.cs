using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Components;
using Content.Shared._RMC14.Stamina;
using Content.Shared._RMC14.StatusEffect;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaResistanceParityTest
{
    [Test]
    public async Task YautjaDamageModifierSetMatchesCmss13SpeciesValues()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);

        try
        {
            await server.WaitAssertion(() =>
            {
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var modifiers = prototypes.Index<DamageModifierSetPrototype>("CMUYautja").Coefficients;

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
                Assert.That(unconscious.Duration, Is.EqualTo(TimeSpan.FromSeconds(3)));
            });
        }
        finally
        {
            server.Dispose();
        }
    }
}
