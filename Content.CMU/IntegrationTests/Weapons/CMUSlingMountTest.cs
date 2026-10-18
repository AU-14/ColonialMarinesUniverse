using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared.CMU14.Attachable;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Weapons;

[TestFixture]
public sealed class CMUSlingMountTest : InteractionTest
{
    private const string Rail = "rmc-aslot-rail";
    private const string Underbarrel = "rmc-aslot-underbarrel";

    protected override string PlayerPrototype => "MobHuman";

    [TestCase("AU14AttachmentTwoPointSling")]
    [TestCase("RMCAttachmentTwoPointSling")]
    public async Task AltClickTogglesMountAndReplicates(string prototype)
    {
        var sling = await PlaceInHands(prototype);
        await Interact(sling, PlayerCoords, altInteract: true);
        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CMUSlingComponent>(ToServer(sling)).Underbarrel, Is.True));
        await Client.WaitAssertion(() =>
            Assert.That(CEntMan.GetComponent<CMUSlingComponent>(ToClient(sling)).Underbarrel, Is.True));

        await Interact(sling, PlayerCoords, altInteract: true);
        await Pair.RunUntilSynced();
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CMUSlingComponent>(ToServer(sling)).Underbarrel, Is.False));
        await Client.WaitAssertion(() =>
            Assert.That(CEntMan.GetComponent<CMUSlingComponent>(ToClient(sling)).Underbarrel, Is.False));
    }

    [Test]
#pragma warning disable RA0002 // Regression coverage reads slot dictionaries owned by AttachableHolderSystem.
    public async Task EverySlingCompatibleGunSupportsUnlockedUnderbarrelMounting()
    {
        await Server.WaitAssertion(() =>
        {
            var attachments = Server.System<AttachableHolderSystem>();
            var slings = Server.System<CMUSlingSystem>();
            var whitelist = Server.System<EntityWhitelistSystem>();
            var factory = Server.ResolveDependency<IComponentFactory>();
            var sling = SEntMan.SpawnEntity("AU14AttachmentTwoPointSling", MapData.GridCoords);
            var slingComp = SEntMan.GetComponent<CMUSlingComponent>(sling);
            var tested = 0;
            var locked = 0;

            foreach (var prototype in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (prototype.Abstract ||
                    !prototype.TryComp<GunComponent>(out _, factory) ||
                    !prototype.TryComp<AttachableHolderComponent>(out var definition, factory) ||
                    !definition.Slots.TryGetValue(Rail, out var rail) ||
                    !whitelist.IsWhitelistPass(rail.Whitelist, sling))
                    continue;

                var gun = SEntMan.SpawnEntity(prototype.ID, MapData.GridCoords);
                var holder = SEntMan.GetComponent<AttachableHolderComponent>(gun);
                Assert.That(attachments.Attach((gun, holder), sling, SPlayer, Rail), Is.True, prototype.ID);
                Assert.That(attachments.Detach((gun, holder), sling, SPlayer, Rail), Is.True, prototype.ID);

                slings.ToggleMount((sling, slingComp), SPlayer);
                Assert.That(slingComp.Underbarrel, Is.True);
                Assert.That(attachments.Attach((gun, holder), sling, SPlayer, Rail), Is.False, prototype.ID);
                Assert.That(holder.Slots.TryGetValue(Underbarrel, out var slot), Is.True, prototype.ID);
                attachments.TryGetAttachable((gun, holder), Underbarrel, out var original);

                if (slot.Locked || rail.Locked)
                {
                    Assert.That(attachments.Attach((gun, holder), sling, SPlayer, Underbarrel), Is.False, prototype.ID);
                    attachments.TryGetAttachable((gun, holder), Underbarrel, out var unchanged);
                    Assert.That(unchanged.Owner, Is.EqualTo(original.Owner), prototype.ID);
                    locked++;
                }
#pragma warning restore RA0002
                else
                {
                    Assert.That(attachments.Attach((gun, holder), sling, SPlayer), Is.True, prototype.ID);
                    Assert.That(attachments.TryGetAttachable((gun, holder), Underbarrel, out var mounted), Is.True);
                    Assert.That(mounted.Owner, Is.EqualTo(sling), prototype.ID);
                    slings.ToggleMount((sling, slingComp), SPlayer);
                    Assert.That(slingComp.Underbarrel, Is.True, "An installed sling must be detached before switching.");
                    Assert.That(attachments.Detach((gun, holder), sling, SPlayer, Underbarrel), Is.True);
                    tested++;
                }

                slings.ToggleMount((sling, slingComp), SPlayer);
                Assert.That(slingComp.Underbarrel, Is.False);
                SEntMan.DeleteEntity(gun);
            }

            SEntMan.DeleteEntity(sling);
            Assert.That(tested, Is.GreaterThan(0));
            Assert.That(locked, Is.GreaterThan(0));
            TestContext.Out.WriteLine($"Verified {tested} unlocked and {locked} locked sling-compatible gun prototypes.");
        });
    }

    [Test]
    public async Task UnderbarrelSlingLeavesRailAvailableForScope()
    {
        var sling = await PlaceInHands("AU14AttachmentTwoPointSling");
        await Interact(sling, PlayerCoords, altInteract: true);
        var gun = await Spawn("RMCWeaponRifleM54C");
        var scope = await Spawn("RMCAttachmentS42xTelescopicMiniscope");
        await Server.WaitAssertion(() =>
        {
            var attachments = Server.System<AttachableHolderSystem>();
            var uid = ToServer(gun);
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(uid);
            Assert.That(attachments.Attach((uid, holder), ToServer(sling), SPlayer), Is.True);
            Assert.That(attachments.Attach((uid, holder), ToServer(scope), SPlayer, Rail), Is.True);
            Assert.That(attachments.TryGetAttachable((uid, holder), Underbarrel, out var mountedSling), Is.True);
            Assert.That(mountedSling.Owner, Is.EqualTo(ToServer(sling)));
            Assert.That(attachments.TryGetAttachable((uid, holder), Rail, out var mountedScope), Is.True);
            Assert.That(mountedScope.Owner, Is.EqualTo(ToServer(scope)));
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var attachments = Client.System<AttachableHolderSystem>();
            var uid = ToClient(gun);
            var holder = CEntMan.GetComponent<AttachableHolderComponent>(uid);
            Assert.That(attachments.TryGetAttachable((uid, holder), Underbarrel, out var mountedSling), Is.True);
            Assert.That(mountedSling.Owner, Is.EqualTo(ToClient(sling)));
            Assert.That(attachments.TryGetAttachable((uid, holder), Rail, out var mountedScope), Is.True);
            Assert.That(mountedScope.Owner, Is.EqualTo(ToClient(scope)));
        });
    }
}
