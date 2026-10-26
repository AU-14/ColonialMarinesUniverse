using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Slow;
using Content.Shared.Atmos;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Item.Stain;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Temperature.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaBugReportsTest
{
    [Test]
    public async Task YautjaKeepsBodyTemperatureInRoomAir()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var air = new GasMixture(Atmospherics.CellVolume) { Temperature = Atmospherics.T20C };
            air.AdjustMoles(Gas.Oxygen, Atmospherics.OxygenMolesStandard);
            air.AdjustMoles(Gas.Nitrogen, Atmospherics.NitrogenMolesStandard);
            entMan.System<AtmosphereSystem>().SetMapAtmosphere(map.MapUid, false, air);

            hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
        });

        // without insulation the body settled at room temp within a couple of minutes
        await pair.RunTicksSync(pair.SecondsToTicks(120));

        await server.WaitAssertion(() =>
        {
            var temperature = server.EntMan.GetComponent<TemperatureComponent>(hunter).Temperature;
            Assert.That(temperature, Is.GreaterThan(Atmospherics.T0C + 35f));
        });

        await pair.CleanReturnAsync();
    }

    private static void RaiseStep(IEntityManager entMan, string stepId, EntityUid patient, EntityUid part)
    {
        var step = entMan.System<SharedCMSurgerySystem>().GetSingleton(stepId);
        Assert.That(step, Is.Not.Null);
        var ev = new CMSurgeryStepEvent(patient, patient, part, new List<EntityUid>());
        entMan.EventBus.RaiseLocalEvent(step!.Value, ref ev);
    }

    private static void PickUpOnly(IEntityManager entMan, EntityUid user, EntityUid item)
    {
        var hands = entMan.System<SharedHandsSystem>();
        foreach (var held in hands.EnumerateHeld(user).ToList())
            hands.TryDrop(user, held);
        Assert.That(hands.TryPickupAnyHand(user, item), Is.True);
    }
}
