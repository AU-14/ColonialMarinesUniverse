using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Scoping;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Weapons;

[TestFixture]
public sealed class CMUNightVisionScopeTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: CMMobHuman
  id: CMUNightVisionScopeTestUser
  components:
  - type: NightVision
    innate: true
    state: Half
    green: false
";

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task NightVisionOnlyAppliesWhileScoped(bool innate, bool unwield)
    {
        EntityUid user = default;
        EntityUid gun = default;
        EntityUid scope = default;

        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity(innate ? "CMUNightVisionScopeTestUser" : "CMMobHuman", MapCoordinates.Nullspace);
            gun = SEntMan.SpawnEntity("RMCWeaponRifleM54C", MapCoordinates.Nullspace);
            scope = SEntMan.SpawnEntity("CMUAttachmentNightVisionScope", MapCoordinates.Nullspace);
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(gun);
            Assert.That(Server.System<AttachableHolderSystem>().Attach((gun, holder), scope, user), Is.True);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, gun), Is.True);
            Assert.That(Server.System<SharedWieldableSystem>().TryWield((gun, SEntMan.GetComponent<WieldableComponent>(gun)), user), Is.True);

            var scopeComp = SEntMan.GetComponent<ScopeComponent>(scope);
            Assert.That(scopeComp.ZoomLevels, Has.Count.EqualTo(1));
            Assert.That(scopeComp.ZoomLevels[0].Zoom, Is.EqualTo(1.5f));
            Assert.That(SEntMan.GetComponent<NightVisionItemComponent>(scope).User, Is.Null);
            Assert.That(Server.System<SharedScopeSystem>().StartScoping((scope, scopeComp), user), Is.Not.Null);
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<ScopingComponent>(user), Is.True);
            var vision = SEntMan.GetComponent<NightVisionComponent>(user);
            Assert.That(vision.State, Is.EqualTo(NightVisionState.Full));
            Assert.That(vision.Green, Is.True);
            Assert.That(vision.BlockScopes, Is.False);
            Assert.That(SEntMan.GetComponent<ContentEyeComponent>(user).TargetZoom, Is.EqualTo(new Vector2(1.5f)));

            if (unwield)
                Assert.That(Server.System<SharedWieldableSystem>().TryUnwield((gun, SEntMan.GetComponent<WieldableComponent>(gun)), user), Is.True);
            else
                Assert.That(Server.System<SharedScopeSystem>().Unscope((scope, SEntMan.GetComponent<ScopeComponent>(scope))), Is.True);
        });

        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<ScopingComponent>(user), Is.False);
            Assert.That(SEntMan.GetComponent<ContentEyeComponent>(user).TargetZoom, Is.EqualTo(Vector2.One));
            Assert.That(SEntMan.GetComponent<NightVisionItemComponent>(scope).User, Is.Null);
            if (innate)
            {
                var vision = SEntMan.GetComponent<NightVisionComponent>(user);
                Assert.That(vision.Innate, Is.True);
                Assert.That(vision.State, Is.EqualTo(NightVisionState.Half));
                Assert.That(vision.Green, Is.False);
            }
            else
            {
                Assert.That(SEntMan.HasComponent<NightVisionComponent>(user), Is.False);
            }

            SEntMan.DeleteEntity(gun);
            SEntMan.DeleteEntity(user);
        });
    }
}
