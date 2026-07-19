#nullable enable

using System.Collections.Generic;
using Content.Shared.Physics;
using Content.Shared.Weapons.Misc;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Physics;

[TestFixture]
[TestOf(typeof(SharedGrapplingGunSystem))]
public sealed class JointVisualsTargetTest
{
    [Test]
    public async Task GrapplingVisualTargetUsesLocalEntityAndNetworks()
    {
        Assert.That(
            typeof(JointVisualsComponent).GetField(nameof(JointVisualsComponent.Target))?.FieldType,
            Is.EqualTo(typeof(EntityUid?)));

        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var sEntMan = server.EntMan;
        var cEntMan = client.EntMan;
        var map = await pair.CreateTestMap();

        EntityUid sGun = default;
        EntityUid sHook = default;

        await server.WaitAssertion(() =>
        {
            sGun = sEntMan.SpawnEntity("WeaponGrapplingGun", map.GridCoords);
            sHook = sEntMan.SpawnEntity("GrapplingHook", map.GridCoords);
            var shootable = sEntMan.EnsureComponent<AmmoComponent>(sHook);
            var shot = new GunShotEvent(
                sGun,
                new List<(EntityUid?, IShootable)> { (sHook, shootable) },
                map.GridCoords,
                map.GridCoords);

            sEntMan.EventBus.RaiseLocalEvent(sGun, ref shot);

            var visuals = sEntMan.GetComponent<JointVisualsComponent>(sHook);
            Assert.That(visuals.Target, Is.EqualTo(sGun));
        });

        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var cGun = cEntMan.GetEntity(sEntMan.GetNetEntity(sGun));
            var cHook = cEntMan.GetEntity(sEntMan.GetNetEntity(sHook));
            var visuals = cEntMan.GetComponent<JointVisualsComponent>(cHook);

            Assert.That(visuals.Target, Is.EqualTo(cGun));
        });

        await pair.CleanReturnAsync();
    }
}
