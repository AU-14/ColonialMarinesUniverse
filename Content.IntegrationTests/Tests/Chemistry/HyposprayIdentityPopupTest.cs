#nullable enable

using System.Linq;
using Content.Client.Popups;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components.Localization;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
[TestOf(typeof(HypospraySystem))]
[TestOf(typeof(RMCSharedHypospraySystem))]
public sealed class HyposprayIdentityPopupTest
{
    private const string ExpectedMessage = "You inject the masked patient.";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: HyposprayIdentityTestTarget
          name: true patient
          components:
          - type: Identity
          - type: Grammar
            attributes:
              proper: false
          - type: SolutionContainerManager
            solutions:
              inject:
                maxVol: 20
          - type: InjectableSolution
            solution: inject

        - type: entity
          id: HyposprayIdentityTestStandard
          components:
          - type: SolutionContainerManager
            solutions:
              hypospray:
                maxVol: 10
                reagents:
                - ReagentId: Water
                  Quantity: 10
          - type: Hypospray
            transferAmount: 5
            onlyAffectsMobs: false

        - type: entity
          id: HyposprayIdentityTestVial
          components:
          - type: SolutionContainerManager
            solutions:
              beaker:
                maxVol: 10
                reagents:
                - ReagentId: Water
                  Quantity: 10

        - type: entity
          id: HyposprayIdentityTestRMC
          components:
          - type: ContainerContainer
            containers:
              vial: !type:ContainerSlot
          - type: ContainerFill
            containers:
              vial:
              - HyposprayIdentityTestVial
          - type: RMCHypospray
            slotId: vial
            vialName: beaker
            transferAmount: 5
            onlyAffectsMobs: false
            tacticalSkills:
              all:
                RMCSkillMedical: 1
        """;

    [Test]
    public async Task StandardAndRmcHypospraysUsePresentedIdentity()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var sEntMan = server.EntMan;
        var cEntMan = client.EntMan;
        var playerManager = server.ResolveDependency<IPlayerManager>();
        var serverSession = playerManager.Sessions.Single();
        var map = await pair.CreateTestMap();

        EntityUid sUser = default;
        EntityUid sTarget = default;
        EntityUid sStandardHypospray = default;
        EntityUid sRmcHypospray = default;

        await server.WaitAssertion(() =>
        {
            sUser = sEntMan.SpawnEntity(null, map.GridCoords);
            sTarget = sEntMan.SpawnEntity("HyposprayIdentityTestTarget", map.GridCoords);
            sStandardHypospray = sEntMan.SpawnEntity("HyposprayIdentityTestStandard", map.GridCoords);
            sRmcHypospray = sEntMan.SpawnEntity("HyposprayIdentityTestRMC", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(serverSession, sUser), Is.True);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var identity = sEntMan.GetComponent<IdentityComponent>(sTarget);
            var identitySlot = identity.IdentityEntitySlot ??
                throw new AssertionException("The target must have an identity slot.");
            var identityEntity = identitySlot.ContainedEntity ??
                throw new AssertionException("The target must have an identity entity.");
            sEntMan.System<MetaDataSystem>().SetEntityName(identityEntity, "masked patient");

            var grammar = sEntMan.EnsureComponent<GrammarComponent>(identityEntity);
            sEntMan.System<GrammarSystem>().SetProperNoun((identityEntity, grammar), false);
        });
        await pair.RunTicksSync(5);

        var cUser = cEntMan.GetEntity(sEntMan.GetNetEntity(sUser));
        var cTarget = cEntMan.GetEntity(sEntMan.GetNetEntity(sTarget));
        var cStandardHypospray = cEntMan.GetEntity(sEntMan.GetNetEntity(sStandardHypospray));
        var cRmcHypospray = cEntMan.GetEntity(sEntMan.GetNetEntity(sRmcHypospray));
        var popups = client.System<PopupSystem>();
        var solutions = client.System<SharedSolutionContainerSystem>();

        await client.WaitAssertion(() =>
        {
            Assert.That(client.Session?.AttachedEntity, Is.EqualTo(cUser));
            Assert.That(cEntMan.GetComponent<MetaDataComponent>(cTarget).EntityName, Is.EqualTo("true patient"));

            var identityEntity = Identity.Entity(cTarget, cEntMan);
            Assert.That(identityEntity, Is.Not.EqualTo(cTarget));
            Assert.That(cEntMan.GetComponent<MetaDataComponent>(identityEntity).EntityName, Is.EqualTo("masked patient"));

            ClearPopups(popups);
            var component = cEntMan.GetComponent<HyposprayComponent>(cStandardHypospray);
            Assert.That(
                client.System<HypospraySystem>().TryDoInject(
                    (cStandardHypospray, component),
                    cTarget,
                    cUser,
                    doAfter: false),
                Is.True);

            AssertPopup(popups);
            Assert.That(solutions.TryGetSolution(cStandardHypospray, "hypospray", out _, out var standardSolution), Is.True);
            Assert.That(standardSolution!.Volume.Float(), Is.EqualTo(5f));
            Assert.That(solutions.TryGetSolution(cTarget, "inject", out _, out var targetSolution), Is.True);
            Assert.That(targetSolution!.Volume.Float(), Is.EqualTo(5f));

            ClearPopups(popups);
            var doAfterEvent = new HyposprayDoAfterEvent();
            var doAfterArgs = new DoAfterArgs(
                cEntMan,
                cUser,
                TimeSpan.Zero,
                doAfterEvent,
                cRmcHypospray,
                cTarget,
                cRmcHypospray);
            doAfterEvent.DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero);

            cEntMan.EventBus.RaiseLocalEvent(cRmcHypospray, doAfterEvent);

            Assert.That(doAfterEvent.Handled, Is.True);
            AssertPopup(popups);
            Assert.That(solutions.TryGetSolution(cTarget, "inject", out _, out targetSolution), Is.True);
            Assert.That(targetSolution!.Volume.Float(), Is.EqualTo(10f));

            var containers = client.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainer(cRmcHypospray, "vial", out var vialContainer), Is.True);
            var vial = vialContainer!.ContainedEntities.Single();
            Assert.That(solutions.TryGetSolution(vial, "beaker", out _, out var vialSolution), Is.True);
            Assert.That(vialSolution!.Volume.Float(), Is.EqualTo(5f));
        });

        await pair.CleanReturnAsync();
    }

    private static void ClearPopups(PopupSystem popups)
    {
        popups.SetPopupsSuppressed(true);
        popups.SetPopupsSuppressed(false);
    }

    private static void AssertPopup(PopupSystem popups)
    {
        Assert.That(popups.WorldLabels, Has.Count.EqualTo(1));
        var popup = popups.WorldLabels.Single();
        Assert.Multiple(() =>
        {
            Assert.That(popup.Text, Is.EqualTo(ExpectedMessage));
            Assert.That(popup.Text, Does.Not.Contain("true patient"));
            Assert.That(popups.CursorLabels, Is.Empty);
        });
    }
}
