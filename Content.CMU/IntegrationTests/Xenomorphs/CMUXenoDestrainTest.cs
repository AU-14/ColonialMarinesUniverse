using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Strain;
using Content.Shared.CMU14.Xenonids.Strain;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Mind;

namespace Content.IntegrationTests.CMU.Xenomorphs;

public sealed class CMUXenoDestrainTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task DestrainMessageTransfersPlayerHiveAndEvolutionPoints()
    {
        var map = await Pair.CreateTestMap();
        EntityUid gardener = default, hive = default, mind = default;
        NetEntity gardenerNet = default;
        await Server.WaitAssertion(() =>
        {
            hive = SSpawnAtPosition("CMXenoHive", map.GridCoords);
            gardener = SSpawnAtPosition("CMXenoDroneGardener", map.GridCoords);
            Server.System<SharedXenoHiveSystem>().SetHive(gardener, hive);
            var minds = Server.System<SharedMindSystem>();
            mind = minds.GetOrCreateMind(ServerSession!.UserId).Owner;
            minds.TransferTo(mind, gardener);
#pragma warning disable RA0002 // Seed a non-default balance to verify it survives the actual transfer.
            SComp<XenoEvolutionComponent>(gardener).Points = 123;
#pragma warning restore RA0002
            Server.System<XenoPlasmaSystem>().SetPlasma((gardener, SComp<XenoPlasmaComponent>(gardener)), 0);
            gardenerNet = SEntMan.GetNetEntity(gardener);
            Assert.That(Server.System<SharedUserInterfaceSystem>().TryOpenUi(
                gardener, XenoEvolutionUIKey.Key, gardener), Is.True);
        });

        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var clientGardener = CEntMan.GetEntity(gardenerNet);
            var baseCaste = CEntMan.GetComponent<XenoStrainComponent>(clientGardener).BaseCaste!.Value;
            CEntMan.System<SharedUserInterfaceSystem>().ClientSendUiMessage(
                clientGardener, XenoEvolutionUIKey.Key, new XenoStrainBuiMsg(baseCaste));
        });
        await Pair.RunUntilSynced();

        await Server.WaitAssertion(() =>
        {
            var drone = SComp<MindComponent>(mind).OwnedEntity!.Value;
            Assert.That(SEntMan.Deleted(gardener), Is.True, "Destraining must replace the specialized body.");
            Assert.That(SComp<MetaDataComponent>(drone).EntityPrototype!.ID, Is.EqualTo("CMXenoDrone"));
            Assert.That(ServerSession!.AttachedEntity, Is.EqualTo(drone), "The same player must control the new body.");
            Assert.That(SComp<HiveMemberComponent>(drone).Hive, Is.EqualTo(hive));
            var points = SComp<XenoEvolutionComponent>(drone).Points;
            Assert.That(points.Int(), Is.InRange(123, 125),
                "Destraining must retain accumulated points rather than pay an evolution cost.");
            Assert.That(SEntMan.HasComponent<XenoStrainComponent>(drone), Is.False);
            var plasma = SComp<XenoPlasmaComponent>(drone).Plasma;
            Assert.That(plasma.Int(), Is.LessThan(10),
                "Destraining must use the normal proportional plasma transfer instead of refilling it.");
        });
    }

    [Test]
    public async Task DestrainRejectsOtherCastesAndHivebrokenPlayers()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var gardener = SSpawnAtPosition("CMXenoDroneGardener", map.GridCoords);
            var minds = Server.System<SharedMindSystem>();
            var mind = minds.GetOrCreateMind(ServerSession!.UserId).Owner;
            minds.TransferTo(mind, gardener);
            var ui = Server.System<SharedUserInterfaceSystem>();
            Assert.That(ui.TryOpenUi(gardener, XenoEvolutionUIKey.Key, gardener), Is.True);
            ui.RaiseUiMessage(gardener, XenoEvolutionUIKey.Key,
                new XenoStrainBuiMsg("CMXenoPraetorian") { Actor = gardener });
            Assert.That(SComp<MindComponent>(mind).OwnedEntity, Is.EqualTo(gardener),
                "A forged destrain request must not allow switching to an unrelated caste.");

            SEntMan.AddComponent<YautjaHivebrokenXenoComponent>(gardener);
            Assert.That(ui.TryOpenUi(gardener, XenoEvolutionUIKey.Key, gardener), Is.True);
            ui.RaiseUiMessage(gardener, XenoEvolutionUIKey.Key,
                new XenoStrainBuiMsg("CMXenoDrone") { Actor = gardener });
            Assert.That(SComp<MindComponent>(mind).OwnedEntity, Is.EqualTo(gardener),
                "Destraining must obey the same hivebroken restriction as choosing a strain.");
        });
    }

    [Test]
    public async Task DestrainCooldownSurvivesBodyChangesAndExpires()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var gardener = SSpawnAtPosition("CMXenoDroneGardener", map.GridCoords);
            var minds = Server.System<SharedMindSystem>();
            var mind = minds.GetOrCreateMind(ServerSession!.UserId).Owner;
            minds.TransferTo(mind, gardener);
            var ui = Server.System<SharedUserInterfaceSystem>();

            EntityUid Choose(string caste)
            {
                var current = SComp<MindComponent>(mind).OwnedEntity!.Value;
                Assert.That(ui.TryOpenUi(current, XenoEvolutionUIKey.Key, current), Is.True);
                ui.RaiseUiMessage(current, XenoEvolutionUIKey.Key,
                    new XenoStrainBuiMsg(caste) { Actor = current });
                Assert.That(ui.IsUiOpen(current, XenoEvolutionUIKey.Key, current), Is.False,
                    $"The strain handler must receive the request for {caste} and close the menu.");
                return SComp<MindComponent>(mind).OwnedEntity!.Value;
            }

            var startedAt = SGameTiming.CurTime;
            var drone = Choose("CMXenoDrone");
            Assert.That(SComp<MetaDataComponent>(drone).EntityPrototype!.ID, Is.EqualTo("CMXenoDrone"),
                "The first destrain must be immediately available.");
            var readyAt = SComp<CMUXenoDestrainCooldownComponent>(drone).NextDestrainAt;
            Assert.That(readyAt - startedAt, Is.EqualTo(TimeSpan.FromMinutes(30)));

            var healer = Choose("CMXenoDroneHealer");
            Assert.That(SComp<MetaDataComponent>(healer).EntityPrototype!.ID, Is.EqualTo("CMXenoDroneHealer"),
                "The destrain cooldown must still allow choosing a strain.");
            Assert.That(SComp<CMUXenoDestrainCooldownComponent>(healer).NextDestrainAt, Is.EqualTo(readyAt),
                "Choosing a new strain must preserve the original deadline without restarting it.");
            Assert.That(Choose("CMXenoDrone"), Is.EqualTo(healer),
                "A new strain must not bypass the destrain cooldown.");

            Server.System<XenoEvolutionSystem>().EvolveTo(healer, "CMXenoHivelord");
            var designer = Choose("CMXenoHivelordDesigner");
            Assert.That(SComp<MetaDataComponent>(designer).EntityPrototype!.ID, Is.EqualTo("CMXenoHivelordDesigner"));
            var cooldown = SComp<CMUXenoDestrainCooldownComponent>(designer);
            Assert.That(cooldown.NextDestrainAt, Is.EqualTo(readyAt),
                "Changing caste and selecting another strain must retain the same deadline.");
            Assert.That(Choose("CMXenoHivelord"), Is.EqualTo(designer));

            // Move the deadline instead of simulating thirty minutes of unrelated gameplay.
#pragma warning disable RA0002
            cooldown.NextDestrainAt = SGameTiming.CurTime + TimeSpan.FromTicks(1);
#pragma warning restore RA0002
            Assert.That(Choose("CMXenoHivelord"), Is.EqualTo(designer),
                "Destraining must remain blocked immediately before expiry.");
#pragma warning disable RA0002
            cooldown.NextDestrainAt = SGameTiming.CurTime;
#pragma warning restore RA0002
            var hivelord = Choose("CMXenoHivelord");
            Assert.That(SEntMan.Deleted(designer), Is.True);
            Assert.That(SComp<MetaDataComponent>(hivelord).EntityPrototype!.ID, Is.EqualTo("CMXenoHivelord"),
                "Destraining must become available at the deadline.");
            Assert.That(SComp<CMUXenoDestrainCooldownComponent>(hivelord).NextDestrainAt - SGameTiming.CurTime,
                Is.EqualTo(TimeSpan.FromMinutes(30)), "A successful destrain must start a fresh cooldown.");
        });
    }
}
