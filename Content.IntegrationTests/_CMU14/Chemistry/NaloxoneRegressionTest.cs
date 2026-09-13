using Content.Shared._RMC14.Body;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Chemistry;

[TestFixture]
public sealed class NaloxoneRegressionTest
{
    private static readonly ProtoId<ReagentPrototype> Tramadol = "CMUTramadol";
    private static readonly ProtoId<ReagentPrototype> Naloxone = "AU14Naloxone";

    [Test]
    public async Task NaloxoneClearsToxinDamageDuringAnOverdose()
    {
        await using var pair = await PoolManager.GetServerClient();
        var human = EntityUid.Invalid;

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            human = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            var bloodstream = entities.System<SharedRMCBloodstreamSystem>();
            Assert.That(bloodstream.TryGetChemicalSolution(human, out var solution, out _), Is.True);
            solution.Comp.Solution.AddReagent(Tramadol, 31);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.2f));

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var damageable = entities.System<DamageableSystem>();
            var component = entities.GetComponent<DamageableComponent>(human);
            var damage = damageable.GetAllDamage((human, component));
            Assert.That(damage.DamageDict["Poison"], Is.GreaterThan(FixedPoint2.Zero));

            var bloodstream = entities.System<SharedRMCBloodstreamSystem>();
            Assert.That(bloodstream.TryGetChemicalSolution(human, out var solution, out _), Is.True);
            solution.Comp.Solution.AddReagent(Naloxone, 3);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.2f));

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var damageable = entities.System<DamageableSystem>();
            var component = entities.GetComponent<DamageableComponent>(human);
            var damage = damageable.GetAllDamage((human, component));
            Assert.Multiple(() =>
            {
                Assert.That(damage.DamageDict["Poison"], Is.EqualTo(FixedPoint2.Zero));
            });

            pair.Server.EntMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }
}
