using System.Linq;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Effects;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared._RMC14.Medical.Surgery.Tools;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class CMUSurgicalToolSupplyTest
{
    private static readonly EntProtoId SurgicalTray = "RMCSurgicalTray";
    private static readonly EntProtoId FixOVein = "CMUFixOVein";
    private static readonly EntProtoId OrganClamp = "CMUOrganClampItem";
    private static readonly EntProtoId FixInternalBleedingStep = "CMUSurgeryStepCauterizeBleed";
    private static readonly EntProtoId PackOrganBleedStep = "CMUSurgeryStepPackOrganBleed";

    [Test]
    public async Task FilledTrayContainsFunctionalSpecializedTools()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            var tray = prototypes.Index<EntityPrototype>(SurgicalTray);
            Assert.That(tray.TryGetComponent<StorageFillComponent>(out var fill, factory), Is.True);
            var contents = fill!.Contents
                .Where(entry => entry.PrototypeId is not null)
                .Select(entry => entry.PrototypeId!.Value)
                .ToArray();

            var fixOVein = prototypes.Index<EntityPrototype>(FixOVein);
            var organClamp = prototypes.Index<EntityPrototype>(OrganClamp);

            Assert.Multiple(() =>
            {
                Assert.That(contents, Does.Contain(FixOVein));
                Assert.That(contents, Does.Contain(OrganClamp));

                Assert.That(fixOVein.TryGetComponent<CMUFixOVeinComponent>(out _, factory), Is.True);
                Assert.That(fixOVein.TryGetComponent<CMSurgeryToolComponent>(out _, factory), Is.True);
                Assert.That(organClamp.TryGetComponent<CMUOrganClampComponent>(out _, factory), Is.True);
                Assert.That(organClamp.TryGetComponent<CMSurgeryToolComponent>(out _, factory), Is.True);

                Assert.That(StepUsesTool<CMUFixOVeinComponent>(prototypes, factory, FixInternalBleedingStep), Is.True);
                Assert.That(StepRemoves<InternalBleedingComponent>(prototypes, factory, FixInternalBleedingStep), Is.True);
                Assert.That(StepUsesTool<CMUOrganClampComponent>(prototypes, factory, PackOrganBleedStep), Is.True);
                Assert.That(
                    StepResolvesTrait(prototypes, factory, PackOrganBleedStep, CMUSurgicalTrait.OrganHemorrhage),
                    Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    private static bool StepUsesTool<T>(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId stepId)
        where T : Component
    {
        var stepPrototype = prototypes.Index<EntityPrototype>(stepId);
        return stepPrototype.TryGetComponent<CMSurgeryStepComponent>(out var step, factory)
            && step!.Tool?.Values.Any(entry => entry.Component is T) == true;
    }

    private static bool StepRemoves<T>(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId stepId)
        where T : Component
    {
        var stepPrototype = prototypes.Index<EntityPrototype>(stepId);
        return stepPrototype.TryGetComponent<CMSurgeryStepComponent>(out var step, factory)
            && step!.Remove?.Values.Any(entry => entry.Component is T) == true;
    }

    private static bool StepResolvesTrait(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId stepId,
        CMUSurgicalTrait trait)
    {
        var stepPrototype = prototypes.Index<EntityPrototype>(stepId);
        return stepPrototype.TryGetComponent<CMUSurgeryStepResolveTraitEffectComponent>(out var effect, factory)
            && effect!.Trait == trait;
    }
}
