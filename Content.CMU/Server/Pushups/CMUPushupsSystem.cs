using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Standing;
using Content.Shared._RMC14.Synth;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat;
using Content.Shared.CMU14.CharacterDescription;
using Content.Shared.CMU14.Pushups;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Standing;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Pushups;

public sealed class CMUPushupsSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RMCStandingSystem _rmcStanding = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const string PushupsEmote = "CMUPushups";
    private const string SitupsEmote = "CMUSitups";
    private const int SynthLimit = 1000;

    private const string FallbackHeight = "5'9";
    private const int FallbackWeight = 160;
    private const BuildType FallbackBuild = BuildType.Average;
    private const int FallbackAge = 25;

    public override void Initialize()
    {
        SubscribeLocalEvent<HumanoidProfileComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<CMUPushupsComponent, MoveInputEvent>(OnMoveInput);
    }

    private void OnEmote(Entity<HumanoidProfileComponent> ent, ref EmoteEvent args)
    {
        if (args.Handled)
            return;

        CMUExercise exercise;
        if (args.Emote.ID == PushupsEmote)
            exercise = CMUExercise.Pushups;
        else if (args.Emote.ID == SitupsEmote)
            exercise = CMUExercise.Situps;
        else
            return;

        args.Handled = true;
        TryStart(ent, exercise);
    }

    private void TryStart(EntityUid uid, CMUExercise exercise)
    {
        var comp = EnsureComp<CMUPushupsComponent>(uid);
        if (comp.Active)
            return;

        if (_mobState.IsIncapacitated(uid) ||
            !_actionBlocker.CanInteract(uid, null) ||
            TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled)
        {
            _popup.PopupEntity(Loc.GetString("cmu-exercise-cant"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!_standing.IsDown(uid) && !_standing.Down(uid, dropHeldItems: false, downedBy: uid))
            return;

        if (TryComp<RMCRestComponent>(uid, out var rest))
            _rmcStanding.SetRest((uid, rest), true);

        comp.Exercise = exercise;
        Face(uid, exercise);

        var now = _timing.CurTime;
        comp.RestedLimit = HasComp<SynthComponent>(uid) ? SynthLimit : RollLimit(uid, exercise);
        comp.Limit = Math.Max(1, (int) MathF.Round(comp.RestedLimit * (1f - 0.8f * CurrentFatigue(comp, exercise, now))));
        comp.Count = 0;
        comp.Active = true;
        comp.Start = Transform(uid).Coordinates;
        comp.RepDuration = comp.BaseRepDuration;
        comp.RepStart = now;
        Dirty(uid, comp);
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
                continue;

            if (!_standing.IsDown(uid) ||
                _mobState.IsIncapacitated(uid) ||
                TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled ||
                !_transform.InRange(Transform(uid).Coordinates, comp.Start, comp.MoveCancelDistance))
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
            SendCount(uid, comp);

            var progress = (float) comp.Count / comp.Limit;
            comp.RepDuration = comp.BaseRepDuration * (1 + 1.2 * progress * progress * progress);
            comp.RepStart = now;
            Dirty(uid, comp);
        }
    }

    private void Fail(Entity<CMUPushupsComponent> ent)
    {
        var exercise = ent.Comp.Exercise;
        Stop(ent, true);
        _chat.TrySendInGameICMessage(ent,
            Loc.GetString(Key(exercise, "fail-emote"), ("count", ent.Comp.Count)),
            InGameICChatType.Emote,
            ChatTransmitRange.Normal,
            hideLog: true,
            ignoreActionBlocker: true);
        _popup.PopupEntity(Loc.GetString(Key(exercise, "fail"), ("count", ent.Comp.Count)), ent, ent, PopupType.MediumCaution);
        StandUp(ent);
    }

    private void Stop(Entity<CMUPushupsComponent> ent, bool failed)
    {
        var now = _timing.CurTime;
        var comp = ent.Comp;
        comp.Active = false;

        var spent = failed ? 1f : (float) comp.Count / Math.Max(1, comp.RestedLimit);
        comp.Fatigue[comp.Exercise] = Math.Clamp(CurrentFatigue(comp, comp.Exercise, now) + spent, 0f, 1f);
        comp.FatigueSetAt[comp.Exercise] = now;
        Dirty(ent);
    }

    private void StandUp(EntityUid uid)
    {
        if (TryComp<RMCRestComponent>(uid, out var rest))
            _rmcStanding.SetRest((uid, rest), false);
        else
            _standing.Stand(uid);
    }

    private void SendCount(EntityUid uid, CMUPushupsComponent comp)
    {
        var ent = Identity.Entity(uid, EntityManager);
        var message = Loc.GetString(Key(comp.Exercise, "count"), ("count", comp.Count));
        var wrapped = Loc.GetString("chat-manager-entity-me-wrap-message",
            ("entityName", FormattedMessage.EscapeText(Name(ent))),
            ("entity", ent),
            ("message", FormattedMessage.EscapeText(message)));

        var filter = Filter.Empty().AddInRange(_transform.GetMapCoordinates(uid), SharedChatSystem.VoiceRange);
        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Emotes, message, wrapped, uid, false, true, null, hidePopup: true);
    }

    private void Face(EntityUid uid, CMUExercise exercise)
    {
        var direction = exercise == CMUExercise.Situps ? Direction.East : Direction.West;
        _transform.SetLocalRotation(uid, direction.ToAngle());
    }

    private static string Key(CMUExercise exercise, string suffix)
    {
        return exercise == CMUExercise.Situps ? $"cmu-situps-{suffix}" : $"cmu-pushups-{suffix}";
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

        var situps = exercise == CMUExercise.Situps;

        float limit = build switch
        {
            BuildType.Thin => situps ? 22 : 14,
            BuildType.Lean => situps ? 34 : 24,
            BuildType.Average => situps ? 26 : 20,
            BuildType.Athletic => situps ? 46 : 38,
            BuildType.Muscular => situps ? 40 : 45,
            BuildType.Broad => situps ? 30 : 32,
            BuildType.Stocky => 26,
            BuildType.Heavyset => situps ? 12 : 10,
            _ => situps ? 26 : 20,
        };

        var inches = ParseHeightInches(height);
        var bmi = 703f * weight / (inches * inches);

        if (bmi > 25f)
            limit *= Math.Clamp(1f - (bmi - 25f) * (situps ? 0.05f : 0.045f), 0.15f, 1f);
        else if (bmi < 18.5f)
            limit *= Math.Clamp(1f - (18.5f - bmi) * 0.08f, 0.4f, 1f);

        var perInch = situps ? 0.006f : 0.015f;
        limit *= Math.Clamp(1f - (inches - 69f) * perInch, 0.8f, 1.15f);

        if (age > 35)
            limit *= Math.Clamp(1f - (age - 35) * 0.012f, 0.4f, 1f);

        limit *= _random.NextFloat(0.85f, 1.15f);
        return Math.Max(1, (int) MathF.Round(limit));
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
