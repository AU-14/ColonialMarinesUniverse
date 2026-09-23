using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using System.Linq;

namespace Content.Server.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUTacticalReconstructionSystem
{
    private TimeSpan _nextContacts;
    private TimeSpan _nextPrototypeContacts;

    private void SendContacts()
    {
        if (_timing.CurTime < _nextContacts) return;
        _nextContacts = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        var refreshPrototype = _timing.CurTime >= _nextPrototypeContacts;
        if (refreshPrototype) _nextPrototypeContacts = _timing.CurTime + TimeSpan.FromSeconds(1);
        var refreshed = new HashSet<EntityUid>();
        foreach (var (key, survey) in _surveys)
        {
            if (!CanUse(key.Console, key.Actor) || !IsCurrentSurvey(key.Console, survey)) continue;
            if (TryComp<TacticalMapComputerComponent>(key.Console, out var computer))
            {
                // Ordinary computer interfaces already refresh through TacticalMapSystem.
                if (refreshPrototype && HasComp<CMUTacticalReconstructionComponent>(key.Console) && refreshed.Add(key.Console))
                    EntityManager.System<Content.Server._RMC14.TacticalMap.TacticalMapSystem>().RefreshReconstructionContacts((key.Console, computer));
            }
            SendContacts(key.Console, key.Actor, survey);
        }
    }

    private void SendContacts(EntityUid source, EntityUid actor, Survey survey)
    {
        var message = Contacts(source, actor, survey);
        if (survey.LastContacts is { } last && last.OperatorPosition == message.OperatorPosition &&
            last.OperatorDepth == message.OperatorDepth && last.Contacts.SequenceEqual(message.Contacts)) return;
        survey.LastContacts = message;
        _ui.ServerSendUiMessage(source, UiKey(source), message, actor);
    }

    private CMUReconContactsMessage Contacts(EntityUid source, EntityUid actor, Survey survey)
    {
        var contacts = new List<CMUReconContact>();
        var seen = new HashSet<int>();
        void Add(Dictionary<int, TacticalMapBlip> blips)
        {
            foreach (var (id, blip) in blips)
            {
                if (!seen.Add(id) || !TryComp<TransformComponent>(new EntityUid(id), out var transform)) continue;
                var level = Array.IndexOf(survey.Atlas.Maps, transform.MapUid);
                if (level >= 0) contacts.Add(new CMUReconContact(survey.Atlas.MinDepth + level, blip));
            }
        }
        if (TryComp<TacticalMapUserComponent>(source, out var user))
        {
            // The classic squad tab has a separate authorized live feed (including SL and fireteam badges).
            // Prefer it over an older published faction snapshot; do not look up unfiltered tracked actors.
            if (user.HasSquad) Add(user.SquadBlips);
            if (user.Marines) Add(user.MarineBlips);
            if (user.Xenos) { Add(user.XenoBlips); Add(user.XenoStructureBlips); }
            if (user.Opfor) Add(user.OpforBlips);
            if (user.Govfor) Add(user.GovforBlips);
            if (user.Clf) Add(user.ClfBlips);
            if (user.WeYu) Add(user.WeYuBlips);
            if (user.Abomination) Add(user.AbominationBlips);
        }
        else if (TryComp<TacticalMapComputerComponent>(source, out var computer))
        {
            Add(computer.SquadBlips);
            Add(computer.Blips);
        }
        var operatorLevel = Array.IndexOf(survey.Atlas.Maps, Transform(actor).MapUid);
        return new CMUReconContactsMessage(survey.Generation, contacts.ToArray())
        {
            OperatorPosition = operatorLevel >= 0 ? _transform.GetWorldPosition(actor) : null,
            OperatorDepth = survey.Atlas.MinDepth + operatorLevel,
        };
    }
}
