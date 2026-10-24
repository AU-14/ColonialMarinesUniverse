using Content.Server.CMU14.FactionRoster;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.CMU14.CriminalRecords;
using Content.Shared.CriminalRecords;
using Content.Shared.CriminalRecords.Components;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Events;

namespace Content.Server.CriminalRecords.Systems;

public sealed partial class CriminalRecordsConsoleSystem
{
    [Dependency] private CMUUniversalRecordsSystem _cmuRecords = default!;
    [Dependency] private GameTicker _cmuTicker = default!;
    [Dependency] private MetaDataSystem _cmuMeta = default!;
    [Dependency] private Content.Shared.Paper.PaperSystem _cmuPaper = default!;
    [Dependency] private MindSystem _cmuMinds = default!;
    [Dependency] private FactionRosterConsoleSystem _cmuRoster = default!;

    private const int CMUMaxSentenceMinutes = 120;

    private void CMUInitialize()
    {
        SubscribeLocalEvent<RecordModifiedEvent>(OnCMURecordModified);
        SubscribeLocalEvent<CriminalRecordsConsoleComponent, CMURecordIssueWarrantMsg>(OnCMUIssueWarrant);
        SubscribeLocalEvent<CriminalRecordsConsoleComponent, CMURecordRevokeWarrantMsg>(OnCMURevokeWarrant);
        SubscribeLocalEvent<CriminalRecordsConsoleComponent, CMURecordLogArrestMsg>(OnCMULogArrest);
        SubscribeLocalEvent<CriminalRecordsConsoleComponent, CMURecordDeleteArrestMsg>(OnCMUDeleteArrest);
        SubscribeLocalEvent<CriminalRecordsConsoleComponent, CMURecordPrintPhotoMsg>(OnCMUPrintPhoto);
        SubscribeLocalEvent<CriminalRecordsConsoleComponent, CMURecordOpenPersonnelMsg>(OnCMUOpenPersonnel);
        SubscribeLocalEvent<CriminalRecordsConsoleComponent, CMURecordPrintRecordMsg>(OnCMUPrintRecord);
    }

    private void OnCMURecordModified(ref RecordModifiedEvent args)
    {
        var query = EntityQueryEnumerator<CriminalRecordsConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateUserInterface((uid, comp));
        }
    }

    private EntityUid? CMUGetRecordsStation(EntityUid console)
    {
        return _cmuRecords.GetRecords();
    }

    /// <summary>The selected record, and the officer's name, if the user may edit it.</summary>
    private bool CMUTryGetEditable(Entity<CriminalRecordsConsoleComponent> ent, EntityUid actor, out StationRecordKey key,
        out CriminalRecord record, out string officer)
    {
        key = default;
        record = default!;
        officer = string.Empty;
        if (!CheckSelected(ent, actor, out var mob, out var selected) ||
            !_records.TryGetRecord<CriminalRecord>(selected.Value, out var found))
        {
            return false;
        }

        key = selected.Value;
        record = found;
        officer = _identity.GetIdentityShortInfo(mob.Value, ent) ?? Loc.GetString("criminal-records-console-unknown-officer");
        return true;
    }

    private void CMUSave(Entity<CriminalRecordsConsoleComponent> ent, StationRecordKey key)
    {
        _records.Synchronize(key);
        UpdateUserInterface(ent);
    }

    private void OnCMUIssueWarrant(Entity<CriminalRecordsConsoleComponent> ent, ref CMURecordIssueWarrantMsg msg)
    {
        var charge = msg.Charge.Trim();
        if (charge.Length < 1 || charge.Length > ent.Comp.MaxStringLength ||
            !CMUTryGetEditable(ent, msg.Actor, out var key, out var record, out var officer))
        {
            return;
        }

        record.CMUWarrants.Add(new CMURecordWarrant
        {
            Charge = charge,
            IssuedBy = officer,
            IssuedAt = _cmuTicker.RoundDuration(),
        });
        CMUSave(ent, key);
    }

    private void OnCMURevokeWarrant(Entity<CriminalRecordsConsoleComponent> ent, ref CMURecordRevokeWarrantMsg msg)
    {
        if (!CMUTryGetEditable(ent, msg.Actor, out var key, out var record, out _) ||
            msg.Index < 0 || msg.Index >= record.CMUWarrants.Count)
        {
            return;
        }

        record.CMUWarrants.RemoveAt(msg.Index);
        CMUSave(ent, key);
    }

    private void OnCMULogArrest(Entity<CriminalRecordsConsoleComponent> ent, ref CMURecordLogArrestMsg msg)
    {
        var charges = msg.Charges.Trim();
        if (charges.Length < 1 || charges.Length > ent.Comp.MaxStringLength ||
            msg.SentenceMinutes < 0 || msg.SentenceMinutes > CMUMaxSentenceMinutes ||
            !CMUTryGetEditable(ent, msg.Actor, out var key, out var record, out var officer))
        {
            return;
        }

        record.CMUArrests.Add(new CMURecordArrest
        {
            Charges = charges,
            SentenceMinutes = msg.SentenceMinutes,
            Officer = officer,
            ArrestedAt = _cmuTicker.RoundDuration(),
        });
        CMUSave(ent, key);
    }

    private void OnCMUDeleteArrest(Entity<CriminalRecordsConsoleComponent> ent, ref CMURecordDeleteArrestMsg msg)
    {
        if (!CMUTryGetEditable(ent, msg.Actor, out var key, out var record, out _) ||
            msg.Index < 0 || msg.Index >= record.CMUArrests.Count)
        {
            return;
        }

        record.CMUArrests.RemoveAt(msg.Index);
        CMUSave(ent, key);
    }

    /// <summary>Prints the selected record on paper: who they are, their status, warrants, arrests and crime history.</summary>
    private void OnCMUPrintRecord(Entity<CriminalRecordsConsoleComponent> ent, ref CMURecordPrintRecordMsg msg)
    {
        if (!CheckSelected(ent, msg.Actor, out _, out var key) ||
            !_records.TryGetRecord<GeneralStationRecord>(key.Value, out var general) ||
            !_records.TryGetRecord<CriminalRecord>(key.Value, out var record))
        {
            return;
        }

        var na = Loc.GetString("generic-not-available-shorthand");
        var lines = new System.Text.StringBuilder();
        foreach (var warrant in record.CMUWarrants)
        {
            lines.AppendLine(Loc.GetString("cmu-records-print-warrant", ("time", CMUFormatTime(warrant.IssuedAt)),
                ("charge", Robust.Shared.Utility.FormattedMessage.EscapeText(warrant.Charge)),
                ("officer", Robust.Shared.Utility.FormattedMessage.EscapeText(warrant.IssuedBy))));
        }

        var warrants = lines.Length > 0 ? lines.ToString().TrimEnd() : Loc.GetString("cmu-records-warrants-none");
        lines.Clear();
        foreach (var arrest in record.CMUArrests)
        {
            lines.AppendLine(Loc.GetString("cmu-records-print-arrest", ("time", CMUFormatTime(arrest.ArrestedAt)),
                ("charges", Robust.Shared.Utility.FormattedMessage.EscapeText(arrest.Charges)),
                ("minutes", arrest.SentenceMinutes),
                ("officer", Robust.Shared.Utility.FormattedMessage.EscapeText(arrest.Officer))));
        }

        var arrests = lines.Length > 0 ? lines.ToString().TrimEnd() : Loc.GetString("cmu-records-arrests-none");
        lines.Clear();
        foreach (var entry in record.History)
        {
            lines.AppendLine(Loc.GetString("cmu-records-print-history", ("time", CMUFormatTime(entry.AddTime)),
                ("crime", Robust.Shared.Utility.FormattedMessage.EscapeText(entry.Crime))));
        }

        var history = lines.Length > 0 ? lines.ToString().TrimEnd() : Loc.GetString("criminal-records-no-history");
        var status = Loc.GetString($"criminal-records-status-{record.Status.ToString().ToLowerInvariant()}");

        var paper = Spawn(CMURecordPaper, Transform(ent).Coordinates);
        _cmuMeta.SetEntityName(paper, Loc.GetString("cmu-records-print-name", ("name", general.Name)));
        _cmuPaper.SetContent(paper, Loc.GetString("cmu-records-print-content",
            ("name", Robust.Shared.Utility.FormattedMessage.EscapeText(general.Name)),
            ("job", string.IsNullOrEmpty(general.JobTitle) ? na : Robust.Shared.Utility.FormattedMessage.EscapeText(general.JobTitle)),
            ("age", general.Age > 0 ? general.Age.ToString() : na),
            ("gender", Loc.GetString($"cmu-records-gender-{general.Gender.ToString().ToLowerInvariant()}")),
            ("height", general.CMUHeight is { } height ? Robust.Shared.Utility.FormattedMessage.EscapeText(height) : na),
            ("prints", general.Fingerprint ?? na),
            ("dna", general.DNA ?? na),
            ("status", status),
            ("reason", record.Reason is { } reason ? Robust.Shared.Utility.FormattedMessage.EscapeText(reason) : na),
            ("warrants", warrants),
            ("arrests", arrests),
            ("history", history)));
        _popup.PopupEntity(Loc.GetString("cmu-records-print-printed"), ent, msg.Actor);
    }

    private const string CMURecordPaper = "CMUPaperCMB";

    private static string CMUFormatTime(TimeSpan time)
    {
        return $"{(int) time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }

    private void OnCMUPrintPhoto(Entity<CriminalRecordsConsoleComponent> ent, ref CMURecordPrintPhotoMsg msg)
    {
        if (!CheckSelected(ent, msg.Actor, out _, out var key) ||
            !_records.TryGetRecord<GeneralStationRecord>(key.Value, out var general) ||
            general.CMUPortrait is not { } portrait)
        {
            return;
        }

        var photo = Spawn(CMUSuspectPhotoComponent.Prototype, Transform(ent).Coordinates);
        var comp = EnsureComp<CMUSuspectPhotoComponent>(photo);
        comp.Name = general.Name;
        comp.Description = Loc.GetString("cmu-records-photo-description",
            ("age", general.Age),
            ("gender", Loc.GetString($"cmu-records-gender-{general.Gender.ToString().ToLowerInvariant()}")),
            ("height", general.CMUHeight ?? Loc.GetString("generic-not-available-shorthand")));
        comp.Portrait = portrait;
        Dirty(photo, comp);
        _cmuMeta.SetEntityName(photo, Loc.GetString("cmu-records-photo-name", ("name", general.Name)));
        _popup.PopupEntity(Loc.GetString("cmu-records-photo-printed"), ent, msg.Actor);
    }

    /// <summary>
    ///     Sends the selected person's personnel file, built the way the personnel roster builds it. It needs them to be
    ///     alive somewhere with a mind, since the file comes from their body and character description.
    /// </summary>
    private void OnCMUOpenPersonnel(Entity<CriminalRecordsConsoleComponent> ent, ref CMURecordOpenPersonnelMsg msg)
    {
        if (ent.Comp.ActiveKey is not { } id ||
            CMUGetRecordsStation(ent) is not { } station ||
            !_records.TryGetRecord<GeneralStationRecord>(new StationRecordKey(id, station), out var general))
        {
            return;
        }

        var query = EntityQueryEnumerator<MindContainerComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var meta))
        {
            if (meta.EntityName != general.Name || !_cmuMinds.TryGetMind(uid, out var mindId, out _))
                continue;

            _ui.ServerSendUiMessage(ent.Owner, CriminalRecordsConsoleKey.Key,
                new CMURecordPersonnelMsg(_cmuRoster.BuildEntry(uid, mindId, false)), msg.Actor);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-records-personnel-none"), ent, msg.Actor);
    }
}
