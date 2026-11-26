using Content.Shared.Preferences; // CMU14
using System.Globalization;
using Content.Server.CMU14.Chat; // CMU14
using System.Linq;
using Content.Server._RMC14.Marines;
using Content.Shared._RMC14.Marines;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Content.Shared._RMC14.ARES;
using Content.Shared.Radio;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Components;
using Content.Shared.StationRecords.Systems;
using Content.Shared.CMU14.Yautja;
using Content.Server.CMU14.Marines.Roles.Chevrons;

namespace Content.Server._RMC14.Announce
{
    public sealed partial class MarinePresenceAnnounceSystem : EntitySystem
    {
        [Dependency] private CMUApolloAnnouncementSystem _apollo = default!; // CMU14
        [Dependency] private ARESCoreSystem _aresCore = default!;
        [Dependency] private MarineAnnounceSystem _marineAnnounce = default!;
        [Dependency] private SquadSystem _squad = default!;
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private StationRecordsSystem _stationRecords = default!;
        [Dependency] private ChevronSystem _chevron = default!;

        public static readonly ProtoId<RadioChannelPrototype> CommonChannel = "MarineCommon";

        public void AnnounceLateJoin(bool lateJoin, bool silent, EntityUid mob, string jobId, string jobName, JobPrototype jobPrototype, HumanoidCharacterProfile? profile = null) // CMU14: profile
        {
            // CMU14: yautja join through the standard spawn path but must never be announced to marines.
            if (HasComp<YautjaComponent>(mob))
                return;

            if (!_aresCore.TryGetMarineARES(out var ares) || ares == null)
                return;

            if (IsCMULateJoinAnnouncementExcluded(mob)) // CMU14
                return;

            var aresUid = ares.Value.Owner;
            // CMU14: without the profile the player's chosen rank was ignored and the first valid one announced.
            var fullRankName = _chevron.GetAnnouncementFullName(mob, jobId, profile);
            var rankName = _chevron.GetAnnouncementShortName(mob, jobId, profile);

            if (lateJoin && !silent)
            {
                if (jobPrototype.JoinNotifyCrew)
                {
                    string? announceToFaction = TryComp<MarineComponent>(mob, out var marineComp)
                        ? marineComp.Faction
                        : null;

                    _marineAnnounce.AnnounceARESStaging(aresUid,
                        Loc.GetString("rmc-latejoin-arrival-announcement-special",
                        ("character", fullRankName)),
                        jobPrototype.LatejoinArrivalSound,
                        null,
                        announceToFaction);
                }
                else
                {
                    var faction = _apollo.PresenceFaction(mob, jobPrototype); // CMU14
                    var departmentPrototypes = _prototypeManager.EnumeratePrototypes<DepartmentPrototype>().ToList();
                    var processedChannels = new HashSet<ProtoId<RadioChannelPrototype>>();
                    bool departmentChannelFound = false;
                    bool isHead = false;

                    foreach (var department in departmentPrototypes)
                    {
                        if (!department.Roles.Contains(jobId))
                            continue;

                        if (department.HeadOfDepartment == jobId)
                            isHead = true;

                        var channelId = department.DepartmentRadio;

                        if (channelId == null && _squad.TryGetMemberSquad(mob, out var squad) && squad.Comp.Radio != null)
                            channelId = squad.Comp.Radio;

                        if (channelId == null || !processedChannels.Add(channelId.Value))
                            continue;

                        departmentChannelFound = true;

                        _marineAnnounce.AnnounceApolloRadio(aresUid, // CMU14
                            Loc.GetString("rmc-latejoin-arrival-announcement",
                            ("character", rankName),
                            ("entity", mob),
                            ("job", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobName))),
                            channelId.Value, faction);
                    }

                    if (!departmentChannelFound || isHead)
                    {
                        _marineAnnounce.AnnounceApolloRadio(aresUid, // CMU14
                            Loc.GetString("rmc-latejoin-arrival-announcement",
                            ("character", rankName),
                            ("entity", mob),
                            ("job", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobName))),
                            CommonChannel, faction);
                    }
                }
            }
        }

        public void AnnounceEarlyLeave(Entity<CryostorageContainedComponent> ent, uint? recordId, EntityUid? station, string jobName)
        {
            if (!_aresCore.TryGetMarineARES(out var ares) || ares == null)
                return;

            var aresUid = ares.Value.Owner;
            JobPrototype? jobProto = null;

            if (TryComp<StationRecordsComponent>(station, out var stationRecords) && recordId != null && station != null)
            {
                var key = new StationRecordKey(recordId.Value, station.Value);
                if (_stationRecords.TryGetRecord<GeneralStationRecord>(key, out var entry, stationRecords) && !string.IsNullOrWhiteSpace(entry.JobPrototype))
                    _prototypeManager.TryIndex(entry.JobPrototype, out jobProto);
            }

            var rankName = _chevron.GetAnnouncementShortName(ent.Owner, jobProto?.ID);
            var faction = _apollo.PresenceFaction(ent.Owner, jobProto); // CMU14

            var departmentPrototypes = _prototypeManager.EnumeratePrototypes<DepartmentPrototype>().ToList();
            var processedChannels = new HashSet<ProtoId<RadioChannelPrototype>>();
            bool departmentChannelFound = false;
            bool isHead = false;

            if (jobProto != null)
            {
                foreach (var department in departmentPrototypes)
                {
                    if (!department.Roles.Contains(jobProto.ID))
                        continue;

                    if (department.HeadOfDepartment == jobProto.ID)
                        isHead = true;

                    var channelId = department.DepartmentRadio;

                    if (channelId == null && _squad.TryGetMemberSquad(ent.Owner, out var squad) && squad.Comp.Radio != null)
                        channelId = squad.Comp.Radio;

                    if (channelId == null || !processedChannels.Add(channelId.Value))
                        continue;

                    departmentChannelFound = true;

                    _marineAnnounce.AnnounceApolloRadio(aresUid, // CMU14
                        Loc.GetString("rmc-earlyleave-cryo-announcement",
                        ("character", rankName),
                        ("entity", ent.Owner),
                        ("job", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobName))),
                        channelId.Value, faction);
                }

                if (!departmentChannelFound || isHead)
                {
                    _marineAnnounce.AnnounceApolloRadio(aresUid, // CMU14
                        Loc.GetString("rmc-earlyleave-cryo-announcement",
                        ("character", rankName),
                        ("entity", ent.Owner),
                        ("job", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobName))),
                        CommonChannel, faction);
                }
            }
            else
            {
                _marineAnnounce.AnnounceApolloRadio(aresUid, // CMU14
                    Loc.GetString("rmc-earlyleave-cryo-announcement",
                    ("character", rankName),
                    ("entity", ent.Owner),
                    ("job", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobName))),
                    CommonChannel, faction);
            }
        }
    }
}
