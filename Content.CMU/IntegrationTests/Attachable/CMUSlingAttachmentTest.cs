using Content.Client._RMC14.Attachable.Components;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared.CMU14.Attachable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Attachable;

[TestFixture]
public sealed class CMUSlingAttachmentTest : GameTest
{
    private const string Rail = "rmc-aslot-rail";
    private const string Underbarrel = "rmc-aslot-underbarrel";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
    - type: entity
      parent: RMCWeaponRifleM54C
      id: CMUTestSlingRifle
      components:
      - type: AttachableHolder
        slots:
          rmc-aslot-rail:
            whitelist:
              tags:
              - RMCAttachmentMagneticHarness
              - RMCAttachmentRailFlashlight
          rmc-aslot-underbarrel:
            whitelist:
              tags:
              - RMCAttachmentBipod
      - type: AttachableHolderVisuals
        offsets:
          rmc-aslot-rail: 0.25, 0.125
          rmc-aslot-underbarrel: -0.25, -0.125

    - type: entity
      parent: CMUTestSlingRifle
      id: CMUTestIncompatibleSlingRifle
      components:
      - type: AttachableHolder
        slots:
          rmc-aslot-rail:
            whitelist:
              tags:
              - RMCAttachmentRailFlashlight
    """;

    [TestCase("AU14AttachmentTwoPointSling")]
    [TestCase("RMCAttachmentTwoPointSling")]
    public async Task DefaultSlingCoexistsWithRailAndUnderbarrelAttachments(string slingPrototype)
    {
        var map = await Pair.CreateTestMap();
        NetEntity rifleNet = default;
        await Server.WaitAssertion(() =>
        {
            var rifle = SEntMan.SpawnEntity("CMUTestSlingRifle", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(rifle);
            var system = SEntMan.System<AttachableHolderSystem>();
            var rail = SEntMan.SpawnEntity("RMCAttachmentRailFlashlight", map.GridCoords);
            var underbarrel = SEntMan.SpawnEntity("RMCAttachmentBipod", map.GridCoords);
            var sling = SEntMan.SpawnEntity(slingPrototype, map.GridCoords);
            Assert.That(system.Attach((rifle, holder), rail, user, Rail), Is.True);
            Assert.That(system.Attach((rifle, holder), underbarrel, user, Underbarrel), Is.True);
            Assert.That(system.Attach((rifle, holder), sling, user), Is.True);
            Assert.That(system.TryGetAttachable(rifle, AttachableHolderSystem.CMUSlingSlot, out var mounted), Is.True);
            Assert.That(mounted.Owner, Is.EqualTo(sling));
            Assert.That(system.TryGetAttachable(rifle, Rail, out var mountedRail), Is.True);
            Assert.That(mountedRail.Owner, Is.EqualTo(rail));
            Assert.That(system.TryGetAttachable(rifle, Underbarrel, out var mountedUnderbarrel), Is.True);
            Assert.That(mountedUnderbarrel.Owner, Is.EqualTo(underbarrel));
            Assert.That(system.Attach((rifle, holder), rail, user, AttachableHolderSystem.CMUSlingSlot), Is.False);
            rifleNet = SEntMan.GetNetEntity(rifle);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var rifle = CEntMan.GetEntity(rifleNet);
            var sprites = CEntMan.System<SpriteSystem>();
            var sprite = CEntMan.GetComponent<SpriteComponent>(rifle);
            var offsets = CEntMan.GetComponent<AttachableHolderVisualsComponent>(rifle).Offsets;
            Assert.That(sprites.LayerMapTryGet((rifle, sprite), AttachableHolderSystem.CMUSlingSlot, out var layer, false), Is.True);
            Assert.That(sprites.TryGetLayer((rifle, sprite), layer, out var slingLayer, false), Is.True);
            Assert.That(slingLayer!.Offset, Is.EqualTo(offsets[Rail]));
        });
    }

    [Test]
    public async Task AlternateMountsUseRailOffsetAndMountedSlingsCannotChangeMode()
    {
        var map = await Pair.CreateTestMap();
        foreach (var slot in new[] { Rail, Underbarrel })
        {
            NetEntity rifleNet = default;
            await Server.WaitAssertion(() =>
            {
                var rifle = SEntMan.SpawnEntity("CMUTestSlingRifle", map.GridCoords);
                var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
                var sling = SEntMan.SpawnEntity("AU14AttachmentTwoPointSling", map.GridCoords);
                var component = SEntMan.GetComponent<CMUSlingComponent>(sling);
                var slings = SEntMan.System<CMUSlingSystem>();
                slings.ToggleMount((sling, component), user);
                if (slot == Underbarrel)
                    slings.ToggleMount((sling, component), user);
                var mount = component.Mount;
                var attachments = SEntMan.System<AttachableHolderSystem>();
                Assert.That(attachments.Attach((rifle, SEntMan.GetComponent<AttachableHolderComponent>(rifle)), sling, user), Is.True);
                Assert.That(attachments.TryGetAttachable(rifle, slot, out var mounted), Is.True);
                Assert.That(mounted.Owner, Is.EqualTo(sling));
                slings.ToggleMount((sling, component), user);
                Assert.That(component.Mount, Is.EqualTo(mount));
                rifleNet = SEntMan.GetNetEntity(rifle);
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var rifle = CEntMan.GetEntity(rifleNet);
                var sprites = CEntMan.System<SpriteSystem>();
                var sprite = CEntMan.GetComponent<SpriteComponent>(rifle);
                var offsets = CEntMan.GetComponent<AttachableHolderVisualsComponent>(rifle).Offsets;
                Assert.That(sprites.LayerMapTryGet((rifle, sprite), slot, out var layer, false), Is.True);
                Assert.That(sprites.TryGetLayer((rifle, sprite), layer, out var slingLayer, false), Is.True);
                Assert.That(slingLayer!.Offset, Is.EqualTo(offsets[Rail]));
            });
        }
    }

    [Test]
    public async Task InteractiveAttachmentDefaultsToTheSlingSlot()
    {
        var map = await Pair.CreateTestMap();
        EntityUid rifle = default;
        EntityUid sling = default;
        var ticks = 0;
        await Server.WaitAssertion(() =>
        {
            rifle = SEntMan.SpawnEntity("CMUTestSlingRifle", map.GridCoords);
            sling = SEntMan.SpawnEntity("AU14AttachmentTwoPointSling", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var hands = SEntMan.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(user, rifle), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, sling), Is.True);
            ticks = (int) Math.Ceiling(SEntMan.GetComponent<AttachableComponent>(sling).AttachDoAfter * SGameTiming.TickRate) + 2;
            SEntMan.System<AttachableHolderSystem>().StartAttach(
                (rifle, SEntMan.GetComponent<AttachableHolderComponent>(rifle)), sling, user);
        });
        await Pair.RunTicksSync(ticks);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<AttachableHolderSystem>()
                .TryGetAttachable(rifle, AttachableHolderSystem.CMUSlingSlot, out var mounted), Is.True);
            Assert.That(mounted.Owner, Is.EqualTo(sling));
        });
    }

    [Test]
    public async Task StartingSlingsLeaveTheRailFreeAndIncompatibleWeaponsRejectSlings()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var system = SEntMan.System<AttachableHolderSystem>();
            var rifle = SEntMan.SpawnEntity("RMCWeaponLMGQYJ72", map.GridCoords);
            Assert.That(system.TryGetAttachable(rifle, AttachableHolderSystem.CMUSlingSlot, out var mounted), Is.True);
            Assert.That(SEntMan.HasComponent<CMUSlingComponent>(mounted), Is.True);
            Assert.That(system.TryGetAttachable(rifle, Rail, out _), Is.False);

            var incompatible = SEntMan.SpawnEntity("CMUTestIncompatibleSlingRifle", map.GridCoords);
            var holder = SEntMan.GetComponent<AttachableHolderComponent>(incompatible);
            Assert.That(holder.Slots, Does.Not.ContainKey(AttachableHolderSystem.CMUSlingSlot));
            var sling = SEntMan.SpawnEntity("AU14AttachmentTwoPointSling", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Assert.That(system.Attach((incompatible, holder), sling, user), Is.False);
        });
    }

    [Test]
    public async Task EverySlingCompatibleWeaponGetsADedicatedSlot()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var factory = Server.ResolveDependency<IComponentFactory>();
            var whitelist = SEntMan.System<EntityWhitelistSystem>();
            var sling = SEntMan.SpawnEntity("AU14AttachmentTwoPointSling", map.GridCoords);
            var count = 0;
            foreach (var prototype in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (prototype.Abstract ||
                    !prototype.TryComp<AttachableHolderComponent>(out var holder, factory))
                {
                    continue;
                }

                var slots = holder.Slots;
                if (!slots.TryGetValue(Rail, out var rail) ||
                    !whitelist.IsWhitelistPass(rail.Whitelist, sling))
                    continue;

                var weapon = SEntMan.SpawnEntity(prototype.ID, map.GridCoords);
                Assert.That(SEntMan.GetComponent<AttachableHolderComponent>(weapon).Slots,
                    Does.ContainKey(AttachableHolderSystem.CMUSlingSlot), prototype.ID);
                SEntMan.DeleteEntity(weapon);
                count++;
            }
            Assert.That(count, Is.GreaterThan(0));
        });
    }
}
