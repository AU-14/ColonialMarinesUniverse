using Content.Server._RMC14.Requisitions;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.CMU14.Requisitions;
using Content.Server.Storage.EntitySystems;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Paper;
using Content.Shared.ParcelWrap.Components;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Requisitions.Mail;

/// <summary>
///     Every so often the colony ASRS is sent a crate of letters and parcels, each addressed to a colonist (anyone in a
///     colonist department) and each with a delivery receipt. Logistics hands them out; the colonist signs their
///     receipt, and a signed receipt sent back down the ASRS pays the colony budget like a crate invoice. A small
///     crate of one or two pieces is sent at the start of the round too.
/// </summary>
public sealed partial class CMUColonyMailSystem : EntitySystem
{
    private const string ColonyFaction = "colony";
    private const string ColonistFaction = "colonist";
    private const int LetterCount = 6;

    private static readonly EntProtoId MailCrate = "CMUColonyMailCrate";
    private static readonly EntProtoId RoundStartMailCrate = "CMUColonyMailCrateRoundStart";
    private static readonly EntProtoId Parcel = "WrappedParcel";
    private static readonly EntProtoId Letter = "CMPaper";

    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private EntityStorageSystem _entityStorage = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;

    /// <summary>How long into the round the round-start mail is sent, so the first colonists have spawned.</summary>
    private static readonly TimeSpan RoundStartMail = TimeSpan.FromSeconds(30);

    /// <summary>How long into the round the first regular mail arrives.</summary>
    private static readonly TimeSpan FirstMail = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan MailIntervalMin = TimeSpan.FromMinutes(12);
    private static readonly TimeSpan MailIntervalMax = TimeSpan.FromMinutes(20);

    /// <summary>How soon to try again when there's nobody to send to or the lift is busy.</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    // Times into the round, not server time, so the lobby doesn't use them up.
    private TimeSpan _nextMail = FirstMail;
    private bool _roundStartSent;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _nextMail = FirstMail;
            _roundStartSent = false;
        });
        SubscribeLocalEvent<CMURequisitionsCrateDeliveredEvent>(OnCrateDelivered);
        SubscribeLocalEvent<CMUColonyMailReceiptComponent, GetVerbsEvent<Verb>>(OnReceiptVerbs);
    }

    public override void Update(float frameTime)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        var now = _gameTicker.RoundDuration();
        if (!_roundStartSent && now >= RoundStartMail)
        {
            if (TrySendMail(RoundStartMailCrate))
                _roundStartSent = true;
        }

        if (now < _nextMail)
            return;

        _nextMail = now + (TrySendMail(MailCrate) ? _random.Next(MailIntervalMin, MailIntervalMax) : RetryDelay);
    }

    /// <summary>Queues a mail crate on the colony ASRS, if there's a colonist to send it to and the lift is free.</summary>
    private bool TrySendMail(EntProtoId crate)
    {
        if (FindColonists().Count == 0)
            return false;

        var sent = false;
        var elevators = EntityQueryEnumerator<RequisitionsElevatorComponent>();
        while (elevators.MoveNext(out var uid, out var elevator))
        {
            if (!string.Equals(elevator.Faction, ColonyFaction, StringComparison.OrdinalIgnoreCase))
                continue;

            // Only while it's sitting still: a lift that's moving has already packed the crates for its queue.
            if (elevator.Mode is not (RequisitionsElevatorMode.Lowered or RequisitionsElevatorMode.Raised) ||
                HasComp<RequisitionsDeliveryComponent>(uid))
            {
                continue;
            }

            var side = (int) MathF.Floor(elevator.Radius * 2 + 1);
            if (elevator.Orders.Count >= side * side)
                continue;

            elevator.Orders.Add(new RequisitionsEntry { Crate = crate, Name = Loc.GetString("cmu-colony-mail-order") });
            Dirty(uid, elevator);
            sent = true;
        }

        if (sent)
            Announce();

        return sent;
    }

    /// <summary>The colony ASRS consoles say the mail is in.</summary>
    private void Announce()
    {
        var consoles = EntityQueryEnumerator<RequisitionsComputerComponent>();
        while (consoles.MoveNext(out var uid, out var computer))
        {
            if (string.Equals(computer.Faction, ColonyFaction, StringComparison.OrdinalIgnoreCase))
                _chat.TrySendInGameICMessage(uid, Loc.GetString("cmu-colony-mail-arrived"), InGameICChatType.Speak, false,
                    ignoreActionBlocker: true);
        }
    }

    private void OnCrateDelivered(ref CMURequisitionsCrateDeliveredEvent args)
    {
        if (!TryComp(args.Crate, out CMUColonyMailCrateComponent? crate))
            return;

        var colonists = FindColonists();
        _random.Shuffle(colonists);
        var count = Math.Min(colonists.Count, _random.Next(crate.MinParcels, crate.MaxParcels + 1));
        for (var i = 0; i < count; i++)
        {
            var (recipient, job) = colonists[i];
            var name = Name(recipient);
            var (item, description) = MakeMailItem(crate, name);
            var parcel = Spawn(Parcel, MapCoordinates.Nullspace);
            if (TryComp(parcel, out WrappedParcelComponent? wrapped))
                _container.Insert(item, wrapped.Contents);

            _meta.SetEntityName(parcel, Loc.GetString("cmu-colony-mail-parcel-name", ("name", name)));
            _meta.SetEntityDescription(parcel, Loc.GetString("cmu-colony-mail-parcel-desc", ("name", name), ("job", job)));
            _entityStorage.Insert(parcel, args.Crate);

            var receipt = Spawn(crate.Receipt, MapCoordinates.Nullspace);
            var receiptComp = EnsureComp<CMUColonyMailReceiptComponent>(receipt);
            receiptComp.Recipient = name;
            receiptComp.Item = description;
            _meta.SetEntityName(receipt, Loc.GetString("cmu-colony-mail-receipt-name", ("name", name)));
            _paper.SetContent(receipt, Loc.GetString("cmu-colony-mail-receipt-content",
                ("name", name), ("job", job), ("item", description)));
            _entityStorage.Insert(receipt, args.Crate);
        }
    }

    /// <summary>Either a letter written to the colonist, or one of the crate's everyday items.</summary>
    private (EntityUid Item, string Description) MakeMailItem(CMUColonyMailCrateComponent crate, string name)
    {
        if (crate.Contents.Count == 0 || _random.Prob(crate.LetterChance))
        {
            var letter = Spawn(Letter, MapCoordinates.Nullspace);
            _meta.SetEntityName(letter, Loc.GetString("cmu-colony-mail-letter-name"));
            _paper.SetContent(letter, Loc.GetString($"cmu-colony-mail-letter-{_random.Next(1, LetterCount + 1)}", ("name", name)));
            return (letter, Loc.GetString("cmu-colony-mail-letter-name"));
        }

        var item = Spawn(_random.Pick(crate.Contents), MapCoordinates.Nullspace);
        return (item, Name(item));
    }

    /// <summary>Living players whose job is in a colonist department, with their job's name.</summary>
    private List<(EntityUid Mob, string Job)> FindColonists()
    {
        var found = new List<(EntityUid, string)>();
        var query = EntityQueryEnumerator<ActorComponent, MindContainerComponent>();
        while (query.MoveNext(out var uid, out _, out var mind))
        {
            if (mind.Mind is not { } mindId ||
                !_mobState.IsAlive(uid) ||
                !_jobs.MindTryGetJob(mindId, out var job) ||
                !_jobs.TryGetAllDepartments(job.ID, out var departments) ||
                !departments.Exists(d => d.Faction == ColonistFaction))
            {
                continue;
            }

            found.Add((uid, job.LocalizedName));
        }

        return found;
    }

    private void OnReceiptVerbs(Entity<CMUColonyMailReceiptComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Signed || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-colony-mail-sign-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/plus.svg.192dpi.png")),
            Act = () => TrySign(ent, user),
        });
    }

    private void TrySign(Entity<CMUColonyMailReceiptComponent> ent, EntityUid user)
    {
        if (ent.Comp.Signed)
            return;

        // Only the colonist it's addressed to can sign for it.
        var name = Name(user);
        if (name != ent.Comp.Recipient)
        {
            _popup.PopupEntity(Loc.GetString("cmu-colony-mail-sign-wrong", ("name", ent.Comp.Recipient)), ent, user);
            return;
        }

        ent.Comp.Signed = true;
        EntityManager.AddComponents(ent, _prototypes.Index(ent.Comp.PayoutTemplate), removeExisting: false);
        _paper.SetContent(ent.Owner, Loc.GetString("cmu-colony-mail-receipt-signed-content",
            ("name", name), ("item", ent.Comp.Item)));
        _popup.PopupEntity(Loc.GetString("cmu-colony-mail-signed"), ent, user);
    }
}

/// <summary>A crate of colony mail. It's filled with addressed parcels and their receipts when it's delivered.</summary>
[RegisterComponent]
public sealed partial class CMUColonyMailCrateComponent : Component
{
    /// <summary>Everyday items a parcel can hold.</summary>
    [DataField]
    public List<EntProtoId> Contents = new();

    /// <summary>Chance a piece of mail is a letter rather than a parcel.</summary>
    [DataField]
    public float LetterChance = 0.4f;

    [DataField]
    public int MinParcels = 2;

    [DataField]
    public int MaxParcels = 4;

    [DataField]
    public EntProtoId Receipt = "CMUColonyMailReceipt";
}

/// <summary>
///     The receipt for one piece of colony mail. The colonist it's addressed to signs it, and once signed it pays the
///     colony budget when it's sent down the ASRS.
/// </summary>
[RegisterComponent]
public sealed partial class CMUColonyMailReceiptComponent : Component
{
    [DataField]
    public string Recipient = string.Empty;

    /// <summary>What was delivered, for the receipt's text.</summary>
    [DataField]
    public string Item = string.Empty;

    [DataField]
    public bool Signed;

    [DataField]
    public EntProtoId PayoutTemplate = "CMUColonyMailReceiptPayoutTemplate";
}
