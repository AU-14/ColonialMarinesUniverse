using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.CMU14.CriminalRecords;
using Content.Shared.CMU14.Religion;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Systems;
using Content.Shared.CMU14.CriminalRecords;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Religion;

/// <summary>
///     Puts a character's religion on their records and, unless they are an atheist, lets them pray:
///     a right-click verb that sends what they type to admin chat, once every 15 minutes.
/// </summary>
public sealed class CMUReligionSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private QuickDialogSystem _quickDialog = default!;
    [Dependency] private StationRecordsSystem _records = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUUniversalRecordsSystem _universalRecords = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete,
            after: [typeof(StationRecordsSystem), typeof(CMUColonyRecordsSystem)]);
        SubscribeLocalEvent<CMUReligiousComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        var religion = args.Profile.Religion;

        if (religion != CMUReligionPrototype.Atheist)
            EnsureComp<CMUReligiousComponent>(args.Mob).Religion = religion;

        if (TryComp<CMUChaplainComponent>(args.Mob, out var chaplain))
            GiveHolyBook((args.Mob, chaplain), religion);

        // Hide Meta Information keeps it off the records consoles too.
        if (args.Profile.HideMetaInformation)
            return;

        SetRecordReligion(args.Station, args.Profile.Name, religion);
        if (_universalRecords.GetRecords() is { } universal && universal != args.Station)
            SetRecordReligion(universal, args.Profile.Name, religion);
    }

    private void SetRecordReligion(EntityUid station, string name, string religion)
    {
        if (!Exists(station) || _records.GetRecordByName(station, name) is not { } id)
            return;

        var key = new StationRecordKey(id, station);
        if (!_records.TryGetRecord<GeneralStationRecord>(key, out var record))
            return;

        record.Religion = religion;
        _records.Synchronize(key);
    }

    /// <summary>
    ///     Gives a chaplain their religion's holy book, in the kit they're holding if they have one.
    /// </summary>
    public void GiveHolyBook(Entity<CMUChaplainComponent> chaplain, ProtoId<CMUReligionPrototype> religion)
    {
        if (!_prototypes.TryIndex(religion, out var faith))
            return;

        var book = Spawn(faith.HolyBook, Transform(chaplain).Coordinates);
        foreach (var held in _hands.EnumerateHeld(chaplain.Owner))
        {
            if (MetaData(held).EntityPrototype?.ID is { } heldId && heldId == chaplain.Comp.Kit.Id &&
                TryComp<StorageComponent>(held, out var kit) &&
                _storage.Insert(held, book, out _, storageComp: kit, playSound: false))
            {
                return;
            }
        }

        _hands.PickupOrDrop(chaplain, book);
    }

    private void OnGetVerbs(Entity<CMUReligiousComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (args.User != args.Target ||
            !TryComp(args.User, out ActorComponent? actor) ||
            _mobState.IsDead(args.User))
        {
            return;
        }

        var session = actor.PlayerSession;
        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-pray-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/pray.svg.png")),
            Act = () => OpenPrayer(user, session),
            Impact = LogImpact.Low,
        });
    }

    private void OpenPrayer(EntityUid user, ICommonSession session)
    {
        if (!TryComp(user, out CMUReligiousComponent? religious))
            return;

        if (_timing.CurTime < religious.NextPrayer)
        {
            var minutes = (int) Math.Ceiling((religious.NextPrayer - _timing.CurTime).TotalMinutes);
            _popup.PopupEntity(Loc.GetString("cmu-pray-cooldown", ("minutes", minutes)), user, session, PopupType.Medium);
            return;
        }

        _quickDialog.OpenDialog(session,
            Loc.GetString("cmu-pray-title"),
            Loc.GetString("cmu-pray-prompt"),
            (LongString prayer) => Pray(user, session, prayer));
    }

    private void Pray(EntityUid user, ICommonSession session, string prayer)
    {
        if (!TryComp(user, out CMUReligiousComponent? religious) ||
            session.AttachedEntity != user ||
            _timing.CurTime < religious.NextPrayer)
        {
            return;
        }

        prayer = prayer.Trim();
        if (prayer.Length == 0)
            return;

        if (prayer.Length > religious.MaxLength)
            prayer = prayer[..religious.MaxLength];

        religious.NextPrayer = _timing.CurTime + religious.Cooldown;

        _chat.SendAdminAnnouncement(Loc.GetString("cmu-pray-admin-message",
            ("name", Name(user)),
            ("prayer", prayer)));
        _popup.PopupEntity(Loc.GetString("cmu-pray-sent"), user, session, PopupType.Medium);
        _adminLog.Add(LogType.AdminMessage, LogImpact.Low,
            $"{ToPrettyString(user):player} prayed: {prayer}");
    }
}
