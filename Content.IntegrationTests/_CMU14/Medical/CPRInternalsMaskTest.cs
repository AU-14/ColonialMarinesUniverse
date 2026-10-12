using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Medical.CPR;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Medical;

[TestFixture]
public sealed class CPRInternalsMaskTest : GameTest
{
    [Test]
    public async Task MasksDoNotBlockCPROnPatientsBreathingFromInternals()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var performer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var mask = SEntMan.SpawnEntity("ClothingMaskBreath", map.GridCoords);
            var tank = SEntMan.SpawnEntity("OxygenTankFilled", map.GridCoords);
            Assert.That(Server.System<InventorySystem>().TryEquip(patient, mask, "mask", silent: true, force: true), Is.True);

            bool Blocked()
            {
                var ev = new ReceiveCPRAttemptEvent(performer, patient, true);
                SEntMan.EventBus.RaiseLocalEvent(patient, ref ev);
                return ev.Cancelled;
            }

            Assert.That(Blocked(), Is.True, "a mask over the mouth should block CPR");

            var internalsSystem = Server.System<SharedInternalsSystem>();
            var internals = SEntMan.EnsureComponent<InternalsComponent>(patient);
            internalsSystem.ConnectBreathTool((patient, internals), mask);
            Assert.That(internalsSystem.TryConnectTank((patient, internals), tank), Is.True);

            Assert.That(Blocked(), Is.False, "a patient breathing from internals can be given CPR through the mask");
        });
    }
}
