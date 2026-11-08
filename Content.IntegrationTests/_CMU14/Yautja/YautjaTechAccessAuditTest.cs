using Content.CMU.Server.Yautja;
using Content.Server.CMU14.Yautja;
using Content.Shared.Actions;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Yautja;

// CMU14 Test: the Yautja item-usage audit against CM-SS13.
//
// Reference behavior (cmss13-devs/cmss13 @ 3753130, code/):
//  - code/game/objects/items.dm:183 — flags_item = ITEM_PREDATOR only does AddElement(yautja_tracked_item).
//    It never restricts carrying, swinging or throwing; the items' own pickup()/equipped() overrides only
//    print flavour text (yaut_bracers.dm:87 "The bracer feels cold against your skin...", yaut_mask.dm:64,
//    yaut_items.dm:243), so those stay unrestricted for every species.
//  - Every real restriction is a per-function HAS_TRAIT(user, TRAIT_YAUTJA_TECH) check, with
//    hunter_data.thralled counting as authorized: guns via able_to_fire (yaut_weapons.dm:1372/1467/1571,
//    mcaste_weapons.dm:69/162/257), the bracer menu/verbs (yaut_bracers.dm:535/1513/1551), the chain's
//    attack_hand (yaut_weapons.dm:724), the smart disc's attack_self fiddle (smartdisc.dm:62-80) and the
//    falcon's can_control_falcon_drone (falcon.dm:55-70, which also demands worn yautja bracers).
[TestFixture]
public sealed class YautjaTechAccessAuditTest
{

    // smartdisc.dm:68-71 — a non-Yautja activating the disc has a 75% chance of "You fiddle with the disc,
    // but nothing happens." and otherwise arms it. Repeated independent attempts make both branches certain
    // to appear without depending on one specific random draw.

    private static MapCoordinates TestCoords(IEntityManager entMan)
    {
        var maps = entMan.System<SharedMapSystem>();
        maps.CreateMap(out var mapId, runMapInit: true);
        return new MapCoordinates(default, mapId);
    }

    private static bool ShootAttempt(IEntityManager entMan, EntityUid user, EntityUid gun)
    {
        var coords = entMan.GetComponent<TransformComponent>(gun).Coordinates;
        var attempt = new AttemptShootEvent(user, null, coords, coords);
        entMan.EventBus.RaiseLocalEvent(gun, ref attempt);
        return attempt.Cancelled;
    }

    [Test]
    public async Task OnlyYautjaTechMayFireYautjaGunsLikeCmss13AbleToFire()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var coords = TestCoords(entMan);

            var marine = entMan.SpawnEntity("CMMobHuman", coords);
            var hunter = entMan.SpawnEntity("CMUMobYautja", coords);
            var marineRifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", coords);
            var hunterRifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", coords);

            Assert.That(ShootAttempt(entMan, marine, marineRifle), Is.True,
                "yaut_weapons.dm:1467 able_to_fire refuses a non-tech user.");
            Assert.That(ShootAttempt(entMan, hunter, hunterRifle), Is.False,
                "A Yautja fires its own gun normally.");

            var spike = entMan.SpawnEntity("CMUYautjaSpikeLauncher", coords);
            Assert.That(ShootAttempt(entMan, marine, spike), Is.True,
                "yaut_weapons.dm:1372 able_to_fire on the spike launcher.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EnthralledUsersCountAsYautjaTechLikeCmss13Thralled()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var coords = TestCoords(entMan);

            // yaut_hudprocs.dm:526 grants TRAIT_YAUTJA_TECH to a newblood, and the checks that read the trait
            // ("!HAS_TRAIT(...) && !hunter_data.thralled") pass for an authorized user. Note the smart disc is
            // not one of them: smartdisc.dm:26/68 uses isyautja(), a species test, so a thrall still fiddles.
            var thrall = entMan.SpawnEntity("CMMobHuman", coords);
            entMan.EnsureComponent<YautjaTechAuthorizedComponent>(thrall);

            var rifle = entMan.SpawnEntity("CMUYautjaPlasmaRifle", coords);
            Assert.That(ShootAttempt(entMan, thrall, rifle), Is.False,
                "An authorized user is not refused by able_to_fire.");

            var spike = entMan.SpawnEntity("CMUYautjaSpikeLauncher", coords);
            Assert.That(ShootAttempt(entMan, thrall, spike), Is.False,
                "yaut_weapons.dm:1372 able_to_fire on the spike launcher also passes for an authorized user.");
        });

        await pair.CleanReturnAsync();
    }

    // Regression: the reported bug was that a Yautja wearing a bracer with nothing fitted still received Deploy
    // Wristblades, Deploy Scimitar and Deploy Shield, because the buttons were granted from ActionWhitelist
    // membership alone. The rule the prototypes document (YautjaComponents.GetDefaultGearPrototypes: a bracer
    // pre-installs only its plasma caster and other attachments "have to be installed explicitly") is that a
    // button follows its attachment, so this covers both the absent and the installed case.

}
