using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;

namespace Content.IntegrationTests.CMU14.Weapons;

[TestFixture]
public sealed class CMUGovforRifleScopeTest : GameTest
{
    private static readonly string[] Rifles =
    [
        "RMCWeaponRifleM54CMK1",
        "RMCWeaponRifleM54CMK1Unloaded",
        "RMCWeaponRifleM54CMK1AP",
        "CMU14WeaponRifleM41AMK1PMC",
        "RMCWeaponRifleSSG45NoLockStripped",
        "RMCWeaponRifleM54C",
        "AU14WeaponRifleKramerAR",
        "CMUWeaponRifleKramerKarbine",
        "AU14WeaponRifleAG80",
        "AU14WeaponRifleprodigyrifleAR",
        "AUWeaponRifleM20A",
        "AU14WeaponRifleM16A1",
        "AU14WeaponRifleM16A2",
        "WeaponRifleMAR40",
        "RMCWeaponRifleMAR30",
        "RMCWeaponRifleABR40Tactical",
        "AU14WeaponRifleMAR30Tactical",
        "AU14WeaponRifleABR40TacticalVAI",
        "AUWeaponRifleXM20A2HAZOPS",
        "RMCWeaponRifleL24",
        "AU14WeaponRifleL90",
        "RMCWeaponRifleL88A1",
        "RMCWeaponRifleL88A2",
        "RMCWeaponRifleL88A1Filled",
        "RMCWeaponRifleL88A2Filled",
        "RMCWeaponRifleL90A1",
        "RMCWeaponRifleL90A2",
        "RMCWeaponRifleL90A1Filled",
        "RMCWeaponRifleL90A1FilledSemiStripped",
        "WeaponRifleM4SPR",
        "WeaponRifleM4SPRCustom",
    ];

    [TestCase("RMCAttachmentS42xTelescopicMiniscope")]
    [TestCase("CMUAttachmentNightVisionScope")]
    public async Task StandardGovforAndScoutRiflesAcceptScope(string scopePrototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var attachments = Server.System<AttachableHolderSystem>();
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            foreach (var riflePrototype in Rifles)
            {
                var rifle = SEntMan.SpawnEntity(riflePrototype, map.GridCoords);
                var scope = SEntMan.SpawnEntity(scopePrototype, map.GridCoords);
                var holder = SEntMan.GetComponent<AttachableHolderComponent>(rifle);
                Assert.That(holder.Slots["rmc-aslot-rail"].Locked, Is.False, riflePrototype);
                Assert.That(attachments.Attach((rifle, holder), scope, user, "rmc-aslot-rail"),
                    Is.True, $"{riflePrototype} must accept {scopePrototype}");
                Assert.That(attachments.TryGetAttachable((rifle, holder), "rmc-aslot-rail", out var attached), Is.True);
                Assert.That(attached.Owner, Is.EqualTo(scope), riflePrototype);
                Assert.That(attachments.Detach((rifle, holder), scope, user, "rmc-aslot-rail"),
                    Is.True, $"{riflePrototype} scope must remain removable");
                SEntMan.DeleteEntity(scope);
                SEntMan.DeleteEntity(rifle);
            }
        });
    }
}
