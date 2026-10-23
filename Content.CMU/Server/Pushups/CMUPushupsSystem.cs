using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Standing;
using Content.Shared._RMC14.Synth;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat;
using Content.Shared.CMU14.CharacterDescription;
using Content.Shared.CMU14.Gym;
using Content.Shared.CMU14.Pushups;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Standing;
using Robust.Server.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Pushups;

public sealed partial class CMUPushupsSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCStandingSystem _rmcStanding = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const string PushupsEmote = "CMUPushups";
    private const string SitupsEmote = "CMUSitups";
    private const int RunAnnounceEvery = 30;
    private const int SynthLimit = 1000;

    private const string FallbackHeight = "5'9";
    private const int FallbackWeight = 160;
    private const BuildType FallbackBuild = BuildType.Average;
    private const int FallbackAge = 25;

    private void OnEmote(Entity<HumanoidProfileComponent> ent, ref EmoteEvent args)
    {
        if (args.Handled)
            return;

        if (args.Emote.ID == PushupsEmote)
        {
            args.Handled = true;
            if (!(TryComp<CMUPushupsComponent>(ent, out var pushups) && pushups.Active))
                OpenPushupsDialog(ent);

            return;
        }

        if (args.Emote.ID != SitupsEmote)
            return;

        args.Handled = true;
        TryStart(ent, CMUExercise.Situps);
    }

    /// <summary>
    ///     Asks how they do their pushups, regular or on the knees.
    /// </summary>
    private void OpenPushupsDialog(EntityUid uid)
    {
        var options = new List<DialogOption>
        {
            new(Loc.GetString("cmu-pushups-dialog-regular"), new CMUPushupsStyleChosenEvent(false)),
            new(Loc.GetString("cmu-pushups-dialog-knees"), new CMUPushupsStyleChosenEvent(true)),
        };

        _dialog.OpenOptions(uid,
            Loc.GetString("cmu-pushups-dialog-title"),
            options,
            Loc.GetString("cmu-pushups-dialog-style"));
    }

    private void OnPushupsStyleChosen(Entity<HumanoidProfileComponent> ent, ref CMUPushupsStyleChosenEvent args)
    {
        // The emote itself says nothing, so the chat line only shows once they actually start.
        if (!TryStart(ent, args.Knees ? CMUExercise.KneePushups : CMUExercise.Pushups))
            return;

        var message = args.CarryOn ? "cmu-pushups-carry-on-knees"
            : args.Knees ? "cmu-pushups-start-knees"
            : "cmu-pushups-start";
        _chat.TrySendInGameICMessage(ent,
            Loc.GetString(message, ("entity", ent.Owner)),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            hideLog: true,
            ignoreActionBlocker: true);
    }

    private void OnPushupsFinish(Entity<CMUPushupsComponent> ent, ref CMUPushupsFinishEvent args)
    {
        if (ent.Comp.Active || ent.Comp.KneesOfferedUntil == null)
            return;

        ent.Comp.KneesOfferedUntil = null;
        StandUp(ent);
    }

    /// <summary>
    ///     Starts a set of reps, which goes until the body gives out or the user moves. Floor exercises put them down
    ///     first; anything else needs <paramref name="equipment"/>, a machine they're strapped into or a dumbbell they hold.
    /// </summary>
    public bool TryStart(EntityUid uid, CMUExercise exercise, EntityUid? equipment = null)
    {
        var comp = EnsureComp<CMUPushupsComponent>(uid);
        if (comp.Active)
            return false;

        comp.KneesOfferedUntil = null;

        if (_mobState.IsIncapacitated(uid) ||
            !_actionBlocker.CanInteract(uid, null) ||
            equipment == null && TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled)
        {
            _popup.PopupEntity(Loc.GetString("cmu-exercise-cant"), uid, uid, PopupType.SmallCaution);
            return false;
        }

        if (IsFloorExercise(exercise))
        {
            if (!_standing.IsDown(uid) && !_standing.Down(uid, dropHeldItems: false, downedBy: uid))
                return false;

            if (TryComp<RMCRestComponent>(uid, out var rest))
                _rmcStanding.SetRest((uid, rest), true);
        }
        else if (_standing.IsDown(uid))
        {
            _popup.PopupEntity(Loc.GetString("cmu-exercise-cant"), uid, uid, PopupType.SmallCaution);
            return false;
        }

        comp.Exercise = exercise;
        comp.Equipment = equipment;
        Face(uid, exercise);

        var now = _timing.CurTime;
        (comp.Limit, comp.RestedLimit) = RollLimits(uid, comp, exercise, now);
        if (TryComp<CMUDumbbellComponent>(equipment, out var weight))
        {
            comp.Limit = Math.Max(1, (int) MathF.Round(comp.Limit * weight.RepMultiplier));
            comp.RestedLimit = Math.Max(1, (int) MathF.Round(comp.RestedLimit * weight.RepMultiplier));
        }

        comp.Count = 0;
        comp.Active = true;
        comp.Start = HasComp<CMUExerciseMachineComponent>(equipment) || HasComp<CMUTreadmillComponent>(equipment)
            ? Transform(equipment.Value).Coordinates
            : Transform(uid).Coordinates;
        comp.BaseRepDuration = BaseRepDuration(exercise);
        comp.RepDuration = comp.BaseRepDuration;
        comp.RepStart = now;
        Dirty(uid, comp);

        if (HasComp<CMUDumbbellComponent>(equipment))
        {
            _chat.TrySendInGameICMessage(uid,
                Loc.GetString("cmu-exercise-start-curls", ("dumbbell", equipment.Value)),
                InGameICChatType.Emote,
                ChatTransmitRange.Normal,
                hideLog: true,
                ignoreActionBlocker: true);
        }

        if (TryComp<CMUTreadmillComponent>(equipment, out var treadmill))
        {
            SetTreadmillRunning((equipment.Value, treadmill), true);
            _chat.TrySendInGameICMessage(uid,
                Loc.GetString("cmu-exercise-start-treadmill", ("treadmill", equipment.Value)),
                InGameICChatType.Emote,
                ChatTransmitRange.Normal,
                hideLog: true,
                ignoreActionBlocker: true);
        }

        if (TryComp<CMUExerciseMachineComponent>(equipment, out var machine))
        {
            machine.User = uid;
            Dirty(equipment.Value, machine);
            _appearance.SetData(equipment.Value, CMUExerciseMachineVisuals.InUse, true);
            _chat.TrySendInGameICMessage(uid,
                Loc.GetString("cmu-exercise-start-machine", ("machine", equipment.Value)),
                InGameICChatType.Emote,
                ChatTransmitRange.Normal,
                hideLog: true,
                ignoreActionBlocker: true);
        }

        return true;
    }

    private void OnMoveInput(Entity<CMUPushupsComponent> ent, ref MoveInputEvent args)
    {
        if (!ent.Comp.Active || !args.HasDirectionalMovement)
            return;

        Stop(ent, false);
        StandUp(ent);
        _popup.PopupEntity(Loc.GetString(Key(ent.Comp.Exercise, "stop"), ("count", ent.Comp.Count)), ent, ent);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUPushupsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Active)
            {
                // They didn't take the chance to carry on with knee pushups.
                if (comp.KneesOfferedUntil is { } offer && now >= offer)
                {
                    comp.KneesOfferedUntil = null;
                    StandUp(uid);
                }

                continue;
            }

            if (!CanContinue(uid, comp))
            {
                Stop((uid, comp), false);
                continue;
            }

            if (now < comp.RepStart + comp.RepDuration)
                continue;

            if (comp.Count >= comp.Limit)
            {
                Fail((uid, comp));
                continue;
            }

            comp.Count++;
            Face(uid, comp.Exercise);
            AnnounceRep(uid, comp);

            if (TryComp<CMUExerciseMachineComponent>(comp.Equipment, out var machine) && machine.RepSound != null)
                _audio.PlayPvs(machine.RepSound, comp.Equipment.Value);

            // Running keeps its pace, counting seconds; reps slow down as the body tires.
            var progress = (float) comp.Count / comp.Limit;
            comp.RepDuration = comp.Exercise == CMUExercise.Running
                ? comp.BaseRepDuration
                : comp.BaseRepDuration * (1 + 1.2 * progress * progress * progress);
            comp.RepStart = now;
            Dirty(uid, comp);
        }

        UpdateGym(now);
    }

    private bool CanContinue(EntityUid uid, CMUPushupsComponent comp)
    {
        if (_mobState.IsIncapacitated(uid) ||
            !_transform.InRange(Transform(uid).Coordinates, comp.Start, comp.MoveCancelDistance))
        {
            return false;
        }

        if (IsFloorExercise(comp.Exercise))
            return _standing.IsDown(uid) && !(TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled);

        if (comp.Equipment is not { } equipment || TerminatingOrDeleted(equipment) || _standing.IsDown(uid))
            return false;

        if (HasComp<CMUExerciseMachineComponent>(equipment))
            return TryComp<BuckleComponent>(uid, out var strapped) && strapped.BuckledTo == equipment;

        if (HasComp<CMUTreadmillComponent>(equipment))
            return true;

        return _hands.IsHolding(uid, equipment);
    }

    private void Fail(Entity<CMUPushupsComponent> ent)
    {
        var exercise = ent.Comp.Exercise;
        Stop(ent, true);
        _chat.TrySendInGameICMessage(ent,
            Loc.GetString(Key(exercise, "fail-emote"), ("count", ent.Comp.Count), ("entity", ent.Owner)),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            hideLog: true,
            ignoreActionBlocker: true);
        _popup.PopupEntity(Loc.GetString(Key(exercise, "fail"), ("count", ent.Comp.Count)), ent, ent, PopupType.MediumCaution);

        switch (exercise)
        {
            case CMUExercise.Pushups:
                // They can drop to their knees and keep going.
                ent.Comp.KneesOfferedUntil = _timing.CurTime + ent.Comp.KneesOfferTime;
                _dialog.OpenOptions(ent,
                    Loc.GetString("cmu-pushups-dialog-title"),
                    [
                        new DialogOption(Loc.GetString("cmu-pushups-dialog-keep-going-knees"),
                            new CMUPushupsStyleChosenEvent(true, CarryOn: true)),
                        new DialogOption(Loc.GetString("cmu-pushups-dialog-finish"), new CMUPushupsFinishEvent()),
                    ],
                    Loc.GetString("cmu-pushups-dialog-keep-going"),
                    cancelEvent: new CMUPushupsFinishEvent(),
                    timeout: ent.Comp.KneesOfferTime);
                break;
            case CMUExercise.Running:
                _standing.Down(ent, dropHeldItems: false);
                break;
            default:
                StandUp(ent);
                break;
        }
    }

    /// <summary>
    ///     Tells everyone nearby how far along they are: every rep in chat, every curl on screen too, and running every
    ///     so many seconds.
    /// </summary>
    private void AnnounceRep(EntityUid uid, CMUPushupsComponent comp)
    {
        if (comp.Exercise == CMUExercise.Running)
        {
            if (comp.Count % RunAnnounceEvery == 0)
                SendCount(uid, Loc.GetString("cmu-running-count", ("count", comp.Count)));

            return;
        }

        SendCount(uid, Loc.GetString(Key(comp.Exercise, "count"), ("count", comp.Count), ("entity", uid)));
        if (comp.Exercise != CMUExercise.Curls)
            return;

        var ordinal = Ordinal(comp.Count);
        _popup.PopupEntity(Loc.GetString("cmu-curls-popup-self", ("ordinal", ordinal)),
            Loc.GetString("cmu-curls-popup-others", ("user", Identity.Entity(uid, EntityManager)), ("ordinal", ordinal)),
            uid,
            uid);
    }

    private static string Ordinal(int number)
    {
        var suffix = (number % 100) is >= 11 and <= 13
            ? "th"
            : (number % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th",
            };

        return $"{number}{suffix}";
    }

    private void Stop(Entity<CMUPushupsComponent> ent, bool failed)
    {
        var now = _timing.CurTime;
        var comp = ent.Comp;
        comp.Active = false;

        var spent = failed ? 1f : (float) comp.Count / Math.Max(1, comp.RestedLimit);
        AddFatigue(comp, comp.Exercise, spent, now);

        var equipment = comp.Equipment;
        comp.Equipment = null;
        Dirty(ent);

        if (TryComp<CMUTreadmillComponent>(equipment, out var treadmill))
            SetTreadmillRunning((equipment.Value, treadmill), false);

        if (TryComp<CMUExerciseMachineComponent>(equipment, out var machine) && machine.User == ent.Owner)
        {
            machine.User = null;
            Dirty(equipment.Value, machine);
            _appearance.SetData(equipment.Value, CMUExerciseMachineVisuals.InUse, false);

            if (TryComp<BuckleComponent>(ent, out var buckle) && buckle.BuckledTo == equipment)
                _buckle.Unbuckle((ent.Owner, buckle), ent.Owner);
        }
    }

    private void StandUp(EntityUid uid)
    {
        if (!_standing.IsDown(uid))
            return;

        if (TryComp<RMCRestComponent>(uid, out var rest))
            _rmcStanding.SetRest((uid, rest), false);
        else
            _standing.Stand(uid);
    }

    private void SendCount(EntityUid uid, string message)
    {
        var ent = Identity.Entity(uid, EntityManager);
        var wrapped = Loc.GetString("chat-manager-entity-me-wrap-message",
            ("entityName", FormattedMessage.EscapeText(Name(ent))),
            ("entity", ent),
            ("message", FormattedMessage.EscapeText(message)));

        var filter = Filter.Empty().AddInRange(_transform.GetMapCoordinates(uid), SharedChatSystem.VoiceRange);
        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Emotes, message, wrapped, uid, false, true, null, hidePopup: true);
    }

    private void Face(EntityUid uid, CMUExercise exercise)
    {
        Direction direction;
        switch (exercise)
        {
            case CMUExercise.Pushups:
            case CMUExercise.KneePushups:
                direction = Direction.West;
                break;
            case CMUExercise.Situps:
                direction = Direction.East;
                break;
            case CMUExercise.StackLifter:
            case CMUExercise.BenchPress:
                direction = Direction.South;
                break;
            default:
                return;
        }

        _transform.SetLocalRotation(uid, direction.ToAngle());
    }

    private static bool IsFloorExercise(CMUExercise exercise)
    {
        return exercise is CMUExercise.Pushups or CMUExercise.KneePushups or CMUExercise.Situps;
    }

    private static TimeSpan BaseRepDuration(CMUExercise exercise)
    {
        return exercise switch
        {
            CMUExercise.StackLifter => TimeSpan.FromSeconds(1.5),
            CMUExercise.BenchPress => TimeSpan.FromSeconds(1.8),
            CMUExercise.Curls => TimeSpan.FromSeconds(1.2),
            CMUExercise.Running => TimeSpan.FromSeconds(1),
            _ => TimeSpan.FromSeconds(1.3),
        };
    }

    private static string Key(CMUExercise exercise, string suffix)
    {
        return $"cmu-{exercise.ToString().ToLowerInvariant()}-{suffix}";
    }

    private static float CurrentFatigue(CMUPushupsComponent comp, CMUExercise exercise, TimeSpan now)
    {
        if (!comp.Fatigue.TryGetValue(exercise, out var fatigue) ||
            !comp.FatigueSetAt.TryGetValue(exercise, out var setAt))
        {
            return 0f;
        }

        var recovered = (float) ((now - setAt) / comp.FatigueRecovery);
        return Math.Clamp(fatigue - recovered, 0f, 1f);
    }

    private static void AddFatigue(CMUPushupsComponent comp, CMUExercise exercise, float spent, TimeSpan now)
    {
        comp.Fatigue[exercise] = Math.Clamp(CurrentFatigue(comp, exercise, now) + spent, 0f, 1f);
        comp.FatigueSetAt[exercise] = now;
    }

    /// <summary>
    ///     How much they can do right now, worn down by fatigue, and how much they could do fully rested.
    /// </summary>
    private (int Limit, int Rested) RollLimits(EntityUid uid, CMUPushupsComponent comp, CMUExercise exercise, TimeSpan now)
    {
        var rested = HasComp<SynthComponent>(uid) ? SynthLimit : RollLimit(uid, exercise);
        var limit = Math.Max(1, (int) MathF.Round(rested * (1f - 0.8f * CurrentFatigue(comp, exercise, now))));
        return (limit, rested);
    }

    private int RollLimit(EntityUid uid, CMUExercise exercise)
    {
        var height = FallbackHeight;
        var weight = FallbackWeight;
        var build = FallbackBuild;
        var age = FallbackAge;
        if (TryComp<CharacterDescriptionComponent>(uid, out var body))
        {
            if (!string.IsNullOrWhiteSpace(body.Height))
                height = body.Height;
            if (body.Weight > 0)
                weight = body.Weight;
            build = body.Build;
            if (body.Age > 0)
                age = body.Age;
        }

        var limit = BaseLimit(exercise, build);

        var inches = ParseHeightInches(height);
        var bmi = 703f * weight / (inches * inches);

        // Carrying weight wears out bodyweight work and running the most; lifting barely minds it.
        var heavyPenalty = exercise switch
        {
            CMUExercise.Situps => 0.05f,
            CMUExercise.Pushups or CMUExercise.KneePushups => 0.045f,
            CMUExercise.Running => 0.06f,
            CMUExercise.Punching => 0.03f,
            _ => 0.015f,
        };

        if (bmi > 25f)
            limit *= Math.Clamp(1f - (bmi - 25f) * heavyPenalty, 0.15f, 1f);
        else if (bmi < 18.5f)
            limit *= Math.Clamp(1f - (18.5f - bmi) * 0.08f, 0.4f, 1f);

        // Long arms make for long pushups and presses; long legs help a runner.
        var perInch = exercise switch
        {
            CMUExercise.Pushups or CMUExercise.KneePushups => 0.015f,
            CMUExercise.BenchPress => 0.01f,
            CMUExercise.Running => -0.008f,
            _ => 0.006f,
        };
        limit *= Math.Clamp(1f - (inches - 69f) * perInch, 0.8f, 1.15f);

        if (age > 35)
            limit *= Math.Clamp(1f - (age - 35) * 0.012f, 0.4f, 1f);

        limit *= _random.NextFloat(0.85f, 1.15f);
        return Math.Max(1, (int) MathF.Round(limit));
    }

    /// <summary>
    ///     Reps for an average-sized 25 year old of each build, or seconds for running. Each build manages more situps
    ///     than pushups, more pushups than curls, and fewer bench presses than anything.
    /// </summary>
    private static float BaseLimit(CMUExercise exercise, BuildType build)
    {
        // Knees take a good share of the weight off, so anyone manages more of them.
        if (exercise == CMUExercise.KneePushups)
            return BaseLimit(CMUExercise.Pushups, build) * 1.6f;

        return exercise switch
        {
            CMUExercise.Situps => build switch
            {
                BuildType.Thin => 22, BuildType.Lean => 36, BuildType.Athletic => 50, BuildType.Muscular => 52,
                BuildType.Broad => 40, BuildType.Stocky => 34, BuildType.Heavyset => 14, _ => 30,
            },
            CMUExercise.StackLifter => build switch
            {
                BuildType.Thin => 8, BuildType.Lean => 13, BuildType.Athletic => 18, BuildType.Muscular => 22,
                BuildType.Broad => 18, BuildType.Stocky => 15, BuildType.Heavyset => 8, _ => 11,
            },
            CMUExercise.BenchPress => build switch
            {
                BuildType.Thin => 5, BuildType.Lean => 9, BuildType.Athletic => 13, BuildType.Muscular => 18,
                BuildType.Broad => 15, BuildType.Stocky => 12, BuildType.Heavyset => 7, _ => 8,
            },
            CMUExercise.Curls => build switch
            {
                BuildType.Thin => 10, BuildType.Lean => 15, BuildType.Athletic => 20, BuildType.Muscular => 25,
                BuildType.Broad => 20, BuildType.Stocky => 17, BuildType.Heavyset => 10, _ => 13,
            },
            CMUExercise.Punching => build switch
            {
                BuildType.Thin => 20, BuildType.Lean => 35, BuildType.Athletic => 45, BuildType.Muscular => 40,
                BuildType.Broad => 35, BuildType.Stocky => 30, BuildType.Heavyset => 15, _ => 25,
            },
            CMUExercise.Running => build switch
            {
                BuildType.Thin => 90, BuildType.Lean => 180, BuildType.Athletic => 240, BuildType.Muscular => 150,
                BuildType.Broad => 120, BuildType.Stocky => 100, BuildType.Heavyset => 45, _ => 120,
            },
            _ => build switch
            {
                BuildType.Thin => 14, BuildType.Lean => 24, BuildType.Athletic => 38, BuildType.Muscular => 45,
                BuildType.Broad => 32, BuildType.Stocky => 26, BuildType.Heavyset => 10, _ => 20,
            },
        };
    }

    private static float ParseHeightInches(string height)
    {
        var parts = height.Split('\'');
        if (parts.Length >= 2 &&
            int.TryParse(parts[0], out var feet) &&
            int.TryParse(parts[1].Trim('"', ' '), out var inches))
        {
            return Math.Max(48, feet * 12 + inches);
        }

        return 69f;
    }
}
