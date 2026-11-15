// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;
using Robust.Shared.Player;
using Robust.Server.GameObjects;

namespace Content.Server.CMU14.Government;

public sealed partial class ColonyGovernmentSystem
{
    // How far from the console the person must be for a licence.
    public const float LicenseRange = 7f;

    #region Licences

    // The mayor gives a licence to a person who stands by the console: the fee is taken from the bank account of the person (the card of the account is looked for, wherever it is), goes to the
    // treasury, and a card is printed into the hand of the mayor. What the card says is looked at by the law officers, who write the fines themselves.
    private void OnIssueLicense(Entity<ColonyGovernmentConsoleComponent> console, ref IssueLicenseMessage msg)
    {
        var government = EnsureGovernment();
        var comp = government.Comp;
        if (!_proto.TryIndex<ColonyLicensePrototype>(msg.License, out var proto))
            return;

        var target = GetEntity(msg.Target);
        if (!Exists(target) || !_transform.InRange(Transform(console.Owner).Coordinates, Transform(target).Coordinates, LicenseRange))
            return;

        if (_bank.FindOwnedCard(target) is not { } owned || owned.card.AccountBalance < proto.Fee)
            return;

        owned.card.AccountBalance -= proto.Fee;
        Dirty(owned.uid, owned.card);
        _budget.AddToBudget(proto.Fee);

        var now = _timing.CurTime;
        var serial = $"L-{comp.NextLicenseNumber++}";
        var holder = MetaData(target).EntityName;
        var record = new LicenseRecord(serial, proto.ID, holder, now, now + TimeSpan.FromSeconds(proto.DurationSeconds));

        var card = Spawn(proto.Card, Transform(console.Owner).Coordinates);
        if (TryComp(card, out CityLicenseCardComponent? cardComp))
        {
            cardComp.Serial = serial;
            cardComp.TypeName = Loc.GetString(proto.Name);
            cardComp.Holder = holder;
            cardComp.ExpiresAt = record.ExpiresAt;
        }

        record.Card = card;
        comp.Licenses[serial] = record;
        _hands.TryPickupAnyHand(msg.Actor, card);

        Refresh();
    }

    // A licence is taken back: the record says so, and the card, if it still exists, says so when it is looked at.
    private void OnRevokeLicense(Entity<ColonyGovernmentConsoleComponent> console, ref RevokeLicenseMessage msg)
    {
        if (!TryGovernment(out var government) || !government.Comp.Licenses.TryGetValue(msg.Serial, out var record) || record.Revoked)
            return;

        record.Revoked = true;
        if (record.Card is { } card && TryComp(card, out CityLicenseCardComponent? cardComp))
            cardComp.Revoked = true;

        Refresh();
    }

    // The people who stand near a console, to whom a licence can be given.
    private List<NearbyEntry> NearbyPeople(EntityUid console)
    {
        var nearby = new List<NearbyEntry>();
        foreach (var person in _lookup.GetEntitiesInRange<ActorComponent>(Transform(console).Coordinates, LicenseRange))
            nearby.Add(new NearbyEntry(GetNetEntity(person.Owner), MetaData(person.Owner).EntityName));

        return nearby.OrderBy(n => n.Name).ToList();
    }

    #endregion
}
