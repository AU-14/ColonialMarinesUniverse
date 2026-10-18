using System.Text;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Traits.Quirks;

/// <summary>
///     Heavy sleepers shrug off the first couple of shakes.
/// </summary>
public sealed class CMUHeavySleeperSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    ///     Called when someone else shakes a sleeper. Returns true if they sleep through it, which counts towards the
    ///     shakes it takes to wake them.
    /// </summary>
    public bool SleepsThroughShake(EntityUid sleeper, EntityUid user)
    {
        if (user == sleeper || !TryComp(sleeper, out CMUHeavySleeperComponent? heavy))
            return false;

        var now = _timing.CurTime;
        if (now > heavy.LastShake + heavy.ShakeWindow)
            heavy.Shakes = 0;

        heavy.LastShake = now;
        heavy.Shakes++;
        Dirty(sleeper, heavy);

        if (heavy.Shakes >= heavy.ShakesToWake)
        {
            heavy.Shakes = 0;
            return false;
        }

        _popup.PopupClient(Loc.GetString("cmu-heavy-sleeper-stirs", ("target", Identity.Entity(sleeper, EntityManager))),
            sleeper,
            user);
        return true;
    }

    public float DamageToWakeMultiplier(EntityUid sleeper)
    {
        return TryComp(sleeper, out CMUHeavySleeperComponent? heavy) ? heavy.DamageToWakeMultiplier : 1f;
    }
}

/// <summary>
///     The mute can't speak; vocal emotes come out silent.
/// </summary>
public sealed class CMUMuteSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUMuteComponent, SpeakAttemptEvent>(OnSpeakAttempt);
        SubscribeLocalEvent<CMUMuteComponent, EmoteEvent>(OnEmote, before: [typeof(VocalSystem)]);
    }

    private void OnSpeakAttempt(Entity<CMUMuteComponent> ent, ref SpeakAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        _popup.PopupClient(Loc.GetString("cmu-mute-cant-speak"), ent, ent);
        args.Cancel();
    }

    private void OnEmote(Entity<CMUMuteComponent> ent, ref EmoteEvent args)
    {
        // Still shows the emote text, like pantomiming it.
        if (!args.Handled && args.Emote.Category.HasFlag(EmoteCategory.Vocal))
            args.Handled = true;
    }
}

/// <summary>
///     Slow thinkers trail off into the odd "um..." or "uh..." now and then.
/// </summary>
public sealed class CMUSlowThinkingSystem : EntitySystem
{
    private static readonly string[] Fillers = ["um...", "uh...", "er...", "uhh...", "umm..."];

    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSlowThinkingComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(Entity<CMUSlowThinkingComponent> ent, ref AccentGetEvent args)
    {
        if (_net.IsClient || string.IsNullOrWhiteSpace(args.Message) || !_random.Prob(ent.Comp.FillerChance))
            return;

        var words = new List<string>(args.Message.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (words.Count == 0)
            return;

        InsertFiller(words);
        if (words.Count > 8 && _random.Prob(ent.Comp.SecondFillerChance))
            InsertFiller(words);

        args.Message = string.Join(' ', words);
    }

    private void InsertFiller(List<string> words)
    {
        var filler = _random.Pick(Fillers);
        var index = _random.Next(0, Math.Min(words.Count, 6));
        if (index == 0)
        {
            // Leading off with it: capitalise the filler instead of the first word.
            words[0] = LowerFirst(words[0]);
            // string + string, not char + string: the sandbox rejects the span the compiler makes for a char
            words.Insert(0, filler.Substring(0, 1).ToUpperInvariant() + filler.Substring(1));
            return;
        }

        words.Insert(index, filler);
    }

    private static string LowerFirst(string word)
    {
        // Leave "I" and acronyms alone.
        if (word.Length < 2 || char.IsUpper(word[1]) || word == "I")
            return word;

        var builder = new StringBuilder(word);
        builder[0] = char.ToLowerInvariant(builder[0]);
        return builder.ToString();
    }
}
