using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Marines;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Dropship.Cas;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Radio;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

#pragma warning disable RA0002 // Arrange CAS mounts, thermal limits and explosion policies.

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class CasCorrectionsTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task CasExplosionsDoNotGibEvenAfterTheSourceIsDeleted(bool projectileImpact)
    {
        var map = await Pair.CreateTestMap();
        EntityUid victim = default;
        await Server.WaitAssertion(() =>
        {
            victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var gib = SEntMan.EnsureComponent<MobGibbedByExplosionTypeComponent>(victim);
            gib.Explosions = ["RMC", "RMCLTBCannon"];
            gib.Threshold = 0;
            var source = SEntMan.SpawnEntity(projectileImpact ? "CMUProjectileLTBCannonHEAT" : null, map.GridCoords);
            SEntMan.AddComponent<NonGibbingDamageComponent>(source);
            if (!projectileImpact)
            {
                Server.System<ExplosionSystem>().QueueExplosion(
                    Server.System<SharedTransformSystem>().GetMapCoordinates(victim), "RMC", 20, 1, 5, source,
                    canCreateVacuum: false);
            }
            SEntMan.DeleteEntity(source);
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(victim), Is.False);
            Assert.That(SEntMan.GetComponent<DamageableComponent>(victim).TotalDamage.Float(), Is.GreaterThan(0));
            Assert.That(Server.System<GibbingSystem>().GibbingSuppressed, Is.False,
                "CAS must not leave permanent gib protection on the body or the system.");
            Server.System<SharedBodySystem>().GibBody(victim);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(victim), Is.True,
            "Unrelated gibbing must retain its normal behaviour."));
    }
}

[RegisterComponent]
public sealed partial class CasRadioProbeComponent : Component
{
    public readonly List<string> Messages = new();
}

public sealed class CasRadioProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CasRadioProbeComponent, RadioSendAttemptEvent>(OnTransmission);
    }

    private static void OnTransmission(Entity<CasRadioProbeComponent> ent, ref RadioSendAttemptEvent args)
    {
        if (args.Channel.ID == "radioGovforJTAC")
            ent.Comp.Messages.Add(args.Message);
    }
}
