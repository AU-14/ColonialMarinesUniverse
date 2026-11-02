using Content.IntegrationTests.Tests.Movement;
using Content.Server.CMU14.Fob;
using Content.Server.GameTicking.Events;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Fob;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Fob;

[TestFixture]
public sealed class CMUFobFenceMovementTest : MovementTest
{
    protected override string PlayerPrototype => "CMMobHuman";
    protected override int Tiles => 5;

    [Test]
    public async Task StationaryDeployedFenceBlocksWalkingUntilRecovered()
    {
        await SpawnTarget("CMUFobFence");
        var fence = STarget!.Value;
        var equipment = Server.System<CMUFobEquipmentSystem>();
        var wrench = ToServer(await PlaceInHands("CMWrench"));
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUFobReserveEnabled, true);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundStartingEvent(1));
            SEntMan.EnsureComponent<MarineComponent>(SPlayer).Faction = "govfor";
            Server.System<SkillsSystem>().SetSkill(SPlayer, "RMCSkillConstruction", 3);
            Server.System<TagSystem>().AddTag(SPlayer, "InstantDoAfters");
            Assert.That(Server.System<CMUFobReserveSystem>().TryCreateNetwork(fence, "govfor", out _), Is.True);
            Assert.That(equipment.TryStartWork((fence, SEntMan.GetComponent<CMUFobEquipmentComponent>(fence)),
                SPlayer, wrench), Is.True);
        });
        // Let the inherited item physics settle before approaching the fence.
        await RunSeconds(2);
        await Move(DirectionFlag.East, 1);
        await Server.WaitAssertion(() => Assert.That(Delta(), Is.GreaterThan(0.4f),
            "A deployed fence must physically stop a walking marine even after it becomes stationary."));

        var cutters = ToServer(await PlaceInHands("CMWirecutter"));
        await Server.WaitAssertion(() => Assert.That(equipment.TryStartWork(
            (fence, SEntMan.GetComponent<CMUFobEquipmentComponent>(fence)), SPlayer, cutters), Is.True));
        await Move(DirectionFlag.East, 1);
        await Server.WaitAssertion(() => Assert.That(Delta(), Is.LessThan(-0.5f),
            "Recovering the fence must clear the physical barrier."));
    }
}
