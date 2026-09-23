using Content.Server.Destructible;
using Content.Server.Power.Components;
using Content.Shared._RMC14.Power;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CMU14.Atmos;
using Content.Shared.Construction.Components;
using Content.Shared.Damage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Atmos;

[TestFixture]
public sealed class GasMinerPrototypeTest
{
    private static readonly (EntProtoId Id, Gas Gas)[] SingleGasMiners =
    {
        ("CMUGasMinerOxygenPortable", Gas.Oxygen),
        ("CMUGasMinerNitrogenPortable", Gas.Nitrogen),
        ("CMUGasMinerPlasmaPortableAdmin", Gas.Plasma),
        ("CMUGasMinerPhoronPortableAdmin", Gas.Phoron),
        ("CMUGasMinerCarbonDioxidePortableAdmin", Gas.CarbonDioxide),
        ("CMUGasMinerNitrousOxidePortableAdmin", Gas.NitrousOxide),
        ("CMUGasMinerTritiumPortableAdmin", Gas.Tritium),
    };

    [Test]
    public async Task PortableGasMinersHavePermanentFortyMoleContracts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            foreach (var (id, gas) in SingleGasMiners)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                AssertPermanentMiner(prototype, factory, id);

                Assert.That(prototype.TryComp<GasMinerComponent>(out var miner, factory), Is.True, id.ToString());
                Assert.That(miner!.SpawnGas, Is.EqualTo(gas), id.ToString());
                Assert.That(miner.SpawnAmount, Is.EqualTo(40f), id.ToString());
                Assert.That(miner.MaxExternalPressure, Is.EqualTo(150f), id.ToString());
                Assert.That(prototype.TryComp<CMUGasMinerMixtureComponent>(out _, factory), Is.False, id.ToString());
            }

            const string airId = "CMUGasMinerAirPortableAdmin";
            var air = prototypes.Index<EntityPrototype>(airId);
            AssertPermanentMiner(air, factory, airId);
            Assert.That(air.TryComp<GasMinerComponent>(out var airMiner, factory), Is.True);
            Assert.That(airMiner!.SpawnAmount, Is.EqualTo(40f));
            Assert.That(airMiner.MaxExternalPressure, Is.EqualTo(150f));
            Assert.That(air.TryComp<CMUGasMinerMixtureComponent>(out var mixture, factory), Is.True);
            Assert.That(mixture!.Gases, Has.Count.EqualTo(2));
            Assert.That(mixture.Gases[Gas.Oxygen], Is.EqualTo(0.21f));
            Assert.That(mixture.Gases[Gas.Nitrogen], Is.EqualTo(0.79f));
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertPermanentMiner(
        EntityPrototype prototype,
        IComponentFactory factory,
        EntProtoId id)
    {
        Assert.That(prototype.TryComp<TransformComponent>(out var transform, factory), Is.True, id.ToString());
        Assert.That(transform!.Anchored, Is.True, id.ToString());

        Assert.That(prototype.TryComp<PhysicsComponent>(out var physics, factory), Is.True, id.ToString());
        Assert.That(physics!.BodyType, Is.EqualTo(BodyType.Static), id.ToString());

        Assert.That(prototype.TryComp<AnchorableComponent>(out var anchorable, factory), Is.True, id.ToString());
        Assert.That(anchorable!.Flags, Is.EqualTo(AnchorableFlags.None), id.ToString());

        Assert.That(prototype.TryComp<ApcPowerReceiverComponent>(out var power, factory), Is.True, id.ToString());
        Assert.That(power!.Load, Is.Zero, id.ToString());
        Assert.That(prototype.TryComp<ExtensionCableReceiverComponent>(out _, factory), Is.False, id.ToString());

        Assert.That(prototype.TryComp<RMCPowerReceiverComponent>(out var rmcPower, factory), Is.True, id.ToString());
        Assert.That(rmcPower!.IdleLoad, Is.EqualTo(1500), id.ToString());
        Assert.That(rmcPower.ActiveLoad, Is.EqualTo(1500), id.ToString());
        Assert.That(rmcPower.Channel, Is.EqualTo(RMCPowerChannel.Environment), id.ToString());

        Assert.That(prototype.TryComp<DamageableComponent>(out _, factory), Is.False, id.ToString());
        Assert.That(prototype.TryComp<DestructibleComponent>(out _, factory), Is.False, id.ToString());
    }
}
