using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Threats.Mobs.Wendigo.Riding;
using Content.Shared._RMC14.Buckle;
using Content.Shared._RMC14.Explosion;
using Content.Shared.Body.Part;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Riding;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.IntegrationTests.CMU14.Threats.Wendigo;

[TestFixture]
public sealed class CMUWendigoRidingTest
{
    private const string Mount = "CMUWendigoLab";
    private const string Human = "CMMobHuman";
    private const string Xeno = "CMXenoDrone";
    private const string Bullet = "BulletRifle10x24mm";

    /// <summary>
    /// Climbing on needs the Wendigo's consent: stock buckling is refused, the accept path fills the left then the right
    /// shoulder, and a third human, a xeno and a one-legged human are all turned away. A rider's own shots pass through.
    /// </summary>
    [Test]
    public async Task ConsentFillsShouldersAndRefusesTheIneligible()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var buckle = em.System<SharedBuckleSystem>();
            var mounts = em.System<CMUMountSystem>();
            var consent = em.System<CMUMountConsentSystem>();
            var rmcBuckle = em.System<RMCBuckleSystem>();

            var wendigo = em.SpawnEntity(Mount, map.GridCoords);
            var first = em.SpawnEntity(Human, map.GridCoords);
            var second = em.SpawnEntity(Human, map.GridCoords);
            var third = em.SpawnEntity(Human, map.GridCoords);
            var xeno = em.SpawnEntity(Xeno, map.GridCoords);
            var legless = em.SpawnEntity(Human, map.GridCoords);
            var mount = em.GetComponent<CMUMountComponent>(wendigo);

            Assert.That(buckle.TryBuckle(first, first, wendigo, popup: false), Is.False,
                "Stock buckling must not bypass the Wendigo's consent.");
            Assert.That(buckle.IsBuckled(first), Is.False);

            // Left shoulder through the accept path.
            Assert.That(mounts.TryMount(wendigo, first, popup: false), Is.True);
            Assert.That(mount.LeftRider, Is.EqualTo(first));
            AssertSeated(em, rmcBuckle, wendigo, first, mount.LeftSeat);

            // Right shoulder through the dialog's accept answer.
            Assert.That(consent.RequestMount(wendigo, second), Is.True);
            Assert.That(buckle.IsBuckled(second), Is.False, "Asking alone must not mount.");
            em.EventBus.RaiseLocalEvent(wendigo,
                new CMUMountConsentEvent(em.GetNetEntity(wendigo), em.GetNetEntity(second), true));
            Assert.That(mount.RightRider, Is.EqualTo(second));
            AssertSeated(em, rmcBuckle, wendigo, second, mount.RightSeat);

            Assert.That(mounts.TryMount(wendigo, third, popup: false), Is.False, "Only two shoulders.");
            Assert.That(buckle.TryBuckle(third, third, wendigo, popup: false), Is.False);

            // A dismount frees the seat, so the refusals below are about the rider, not about room.
            mounts.Dismount(second, knockdown: false);
            Assert.That(mount.RightRider, Is.Null);
            Assert.That(mounts.TryMount(wendigo, xeno, popup: false), Is.False, "Xenos cannot ride.");

            SeverLeg(em, legless);
            Assert.That(mounts.TryMount(wendigo, legless, popup: false), Is.False, "Riders need both legs.");
            Assert.That(buckle.IsBuckled(legless), Is.False);

            // A rider's bullet passes through the mount under them.
            var bullet = em.SpawnEntity(Bullet, map.GridCoords);
            em.GetComponent<ProjectileComponent>(bullet).Shooter = first;
            var collide = new PreventCollideEvent(wendigo,
                bullet,
                em.GetComponent<PhysicsComponent>(wendigo),
                em.GetComponent<PhysicsComponent>(bullet),
                em.GetComponent<FixturesComponent>(wendigo).Fixtures.Values.First(),
                em.GetComponent<FixturesComponent>(bullet).Fixtures.Values.First());
            em.EventBus.RaiseLocalEvent(wendigo, ref collide);
            Assert.That(collide.Cancelled, Is.True, "A rider's own shots must not hit the mount.");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Explosions and losing a leg throw riders off and restore their own buckle offset.
    /// </summary>
    [Test]
    public async Task ExplosionsAndLegLossDismount()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var buckle = em.System<SharedBuckleSystem>();
            var mounts = em.System<CMUMountSystem>();

            var wendigo = em.SpawnEntity(Mount, map.GridCoords);
            var left = em.SpawnEntity(Human, map.GridCoords);
            var right = em.SpawnEntity(Human, map.GridCoords);
            var hadOffset = em.HasComponent<RMCBuckleOffsetComponent>(left);
            var mount = em.GetComponent<CMUMountComponent>(wendigo);

            Assert.That(mounts.TryMount(wendigo, left, popup: false), Is.True);
            Assert.That(mounts.TryMount(wendigo, right, popup: false), Is.True);

            var explosion = new ExplosionReceivedEvent("RMC", MapCoordinates.Nullspace, new DamageSpecifier());
            em.EventBus.RaiseLocalEvent(wendigo, ref explosion);

            foreach (var rider in new[] { left, right })
            {
                Assert.That(buckle.IsBuckled(rider), Is.False, "An explosion on the mount throws its riders off.");
                Assert.That(em.HasComponent<CMURiderComponent>(rider), Is.False);
            }

            Assert.That(mount.LeftRider, Is.Null);
            Assert.That(mount.RightRider, Is.Null);
            Assert.That(em.HasComponent<RMCBuckleOffsetComponent>(left), Is.EqualTo(hadOffset),
                "The rider's own buckle offset must be restored.");

            var rider2 = em.SpawnEntity(Human, map.GridCoords);
            Assert.That(mounts.TryMount(wendigo, rider2, popup: false), Is.True);
            SeverLeg(em, rider2);
            Assert.That(buckle.IsBuckled(rider2), Is.False, "Losing a leg throws the rider off.");
            Assert.That(em.HasComponent<CMURiderComponent>(rider2), Is.False);
            Assert.That(mount.LeftRider, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertSeated(IEntityManager em, RMCBuckleSystem rmcBuckle, EntityUid wendigo, EntityUid rider, Vector2 seat)
    {
        var buckle = em.GetComponent<BuckleComponent>(rider);
        Assert.That(buckle.BuckledTo, Is.EqualTo(wendigo));
        Assert.That(em.GetComponent<CMURiderComponent>(rider).Mount, Is.EqualTo(wendigo));
        Assert.That((rmcBuckle.GetOffset(rider) - seat).Length(), Is.LessThan(0.001f));

        var strap = em.GetComponent<StrapComponent>(wendigo);
        var xform = em.GetComponent<TransformComponent>(rider);
        Assert.That(xform.ParentUid, Is.EqualTo(wendigo));
        Assert.That((xform.LocalPosition - strap.BuckleOffset - seat).Length(), Is.LessThan(0.001f));
    }

    private static void SeverLeg(IEntityManager em, EntityUid body)
    {
        var index = em.System<CMUMedicalBodyIndexSystem>();
        Assert.That(index.TryGetBodyPart(body, new(BodyPartType.Leg, BodyPartSymmetry.Left), out var leg), Is.True);
        var attempt = new BodyPartSeverAttemptEvent(body, leg, BodyPartType.Leg, Surgical: true);
        em.EventBus.RaiseLocalEvent(leg, ref attempt);
        Assert.That(attempt.Succeeded, Is.True);
    }
}
