using Content.Shared.Verbs;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Database;
using Robust.Shared.Log;
using Robust.Shared.Utility;
using Robust.Shared.GameObjects;

namespace Content.Server._RMC14.Tracker
{
    public sealed class BattleBuddySystem : EntitySystem
    {
        [Dependency] private readonly SquadLeaderTrackerSystem _trackerSys = default!;
        [Dependency] private readonly ILogManager _log = default!;
        [Dependency] private readonly DialogSystem _dialog = default!;

        private ISawmill _sawmill = default!;

        public override void Initialize()
        {
            base.Initialize();
            _sawmill = _log.GetSawmill("battlebuddy");
            SubscribeLocalEvent<GetVerbsEvent<Verb>>(AddBattleBuddyVerb);
            SubscribeLocalEvent<BattleBuddyAcceptEvent>(OnBattleBuddyAccept);
            SubscribeLocalEvent<BattleBuddyDeclineEvent>(OnBattleBuddyDecline);
        }

        private void AddBattleBuddyVerb(GetVerbsEvent<Verb> args)
        {
            var user = args.User;
            var target = args.Target;

            _sawmill.Debug("Checking battle buddy verb conditions for user {0} target {1}", user, target);

            if (user == target)
            {
                _sawmill.Debug("Skipping: user == target");
                return;
            }

            if (!args.CanInteract || !args.CanAccess || !args.CanComplexInteract)
            {
                _sawmill.Debug("Skipping: interaction/access checks failed");
                return;
            }

            if (!HasComp<MarineComponent>(user) || !HasComp<MarineComponent>(target))
            {
                _sawmill.Debug("Skipping: either user or target is not marine");
                return;
            }

            if (!TryComp<MarineComponent>(user, out var userMarine) || !TryComp<MarineComponent>(target, out var targetMarine))
            {
                _sawmill.Debug("Skipping: failed to read marine components");
                return;
            }

            if (string.IsNullOrEmpty(userMarine.Faction) || userMarine.Faction != targetMarine.Faction)
            {
                _sawmill.Debug("Skipping: factions don't match or are empty ({0} vs {1})", userMarine.Faction, targetMarine.Faction);
                return;
            }

            if (!HasComp<SquadLeaderTrackerComponent>(user) || !HasComp<SquadLeaderTrackerComponent>(target))
            {
                _sawmill.Debug("Skipping: either user or target lacks SquadLeaderTrackerComponent");
                return;
            }

            var verb = new Verb
            {
                Text = Loc.GetString("Invite as buddy"),
                Icon = new SpriteSpecifier.Rsi(new ResPath("_RMC14/Effects/emotes.rsi"), "emote_highfive"),
                Impact = LogImpact.Low,
                Act = () => SendBuddyRequest(user, target)
            };

            args.Verbs.Add(verb);
            _sawmill.Info("Added 'Invite as buddy' verb for {0} -> {1}", user, target);
        }

        private void SendBuddyRequest(EntityUid requester, EntityUid target)
        {
            _sawmill.Debug("SendBuddyRequest: requester={0} target={1}", requester, target);
            var options = new List<DialogOption>
            {
                new DialogOption(Loc.GetString("Accept"), new BattleBuddyAcceptEvent(GetNetEntity(requester), GetNetEntity(target))),
                new DialogOption(Loc.GetString("Decline"), new BattleBuddyDeclineEvent(GetNetEntity(requester), GetNetEntity(target)))
            };

            var title = Loc.GetString("Battle Buddy Invitation");
            var message = Loc.GetString($"{Name(requester)} has invited you to be their battle buddy.");


            // Open the options UI for the invited player (target). The 'actor' parameter decides which client's UI opens.
            _dialog.OpenOptions(target, target, title, options, message);
            _sawmill.Info("Sent battle buddy request from {0} to {1}", requester, target);
        }

        private void OnBattleBuddyAccept(BattleBuddyAcceptEvent ev)
        {
            if (!TryGetEntity(ev.Requester, out var requester) || !TryGetEntity(ev.Target, out var eventTarget))
            {
                _sawmill.Warning("Battle buddy accept: net entities not found: {0} / {1}", ev.Requester, ev.Target);
                return;
            }

            _sawmill.Info("Battle buddy request accepted: {0} <-> {1}", requester, eventTarget);
            _trackerSys.SetBattleBuddy(requester.Value, eventTarget.Value);
        }

        private void OnBattleBuddyDecline(BattleBuddyDeclineEvent ev)
        {
            if (!TryGetEntity(ev.Requester, out var requester) || !TryGetEntity(ev.Target, out var eventTarget))
            {
                _sawmill.Warning("Battle buddy decline: net entities not found: {0} / {1}", ev.Requester, ev.Target);
                return;
            }

            _sawmill.Info("Battle buddy request declined: {0} -> {1}", requester, eventTarget);
        }
    }
}
