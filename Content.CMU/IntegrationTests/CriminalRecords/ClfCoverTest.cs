using Content.IntegrationTests.Fixtures;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.CriminalRecords;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Systems;

namespace Content.IntegrationTests.CMU14.CriminalRecords;

[TestFixture]
public sealed class ClfCoverTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task SapperArrivalRecordsTheWornCivilianCover()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            const string job = "AU14JobCLFSapper";
            var profile = new HumanoidCharacterProfile();
            var mob = Server.System<StationSpawningSystem>().SpawnPlayerMob(map.GridCoords, job, profile, station: null);
            var ids = Server.System<SharedIdCardSystem>();
            Assert.That(ids.TryFindIdCard(mob, out var worn), Is.True);
            var engineer = Server.ProtoMan.Index<JobPrototype>("AU14JobCivilianEngineer").LocalizedName;
            Assert.That(worn.Comp.LocalizedJobTitle, Is.EqualTo(engineer));

            var hands = Server.System<SharedHandsSystem>();
            hands.TryDrop(mob);
            var spare = SEntMan.SpawnEntity("AU14IDCardColonyWasteManagementSpecialist", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(mob, spare), Is.True);
            var ev = new PlayerSpawnCompleteEvent(mob, ServerSession!, job, false, true, 0, EntityUid.Invalid, profile);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ev);

            var holder = Server.System<CMUUniversalRecordsSystem>().GetRecords()!.Value;
            var records = Server.System<StationRecordsSystem>();
            var id = records.GetRecordByName(holder, profile.Name);
            Assert.That(id, Is.Not.Null);
            Assert.That(records.TryGetRecord<GeneralStationRecord>(new StationRecordKey(id!.Value, holder), out var record), Is.True);
            Assert.That(record!.JobTitle, Is.EqualTo(engineer));
            Assert.That(record.JobIcon, Is.EqualTo(worn.Comp.JobIcon));
        });
    }
}
