using Content.Server._RMC14.Chat.Chat;
using Content.Server.Radio;
using Content.Shared._RMC14.Deafness;
using Content.Shared.Chat;
using Robust.Shared.Random;

namespace Content.Server._RMC14.Deafness;

public sealed partial class DeafnessSystem : SharedDeafnessSystem
{
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
        SubscribeLocalEvent<DeafComponent, ChatMessageOverrideInVoiceRangeEvent>(OnOverrideInVoiceRange);
    }

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        var user = Transform(args.RadioReceiver).ParentUid;

        if (!HasComp<DeafComponent>(user))
            return;

        args.Cancelled = true;
    }

    private void OnOverrideInVoiceRange(Entity<DeafComponent> ent, ref ChatMessageOverrideInVoiceRangeEvent args)
    {
        if (args.Channel == ChatChannel.Emotes
            || args.Channel == ChatChannel.Damage
            || args.Channel == ChatChannel.Visual
            || args.Channel == ChatChannel.Notifications
            || args.Channel == ChatChannel.OOC
            || args.Channel == ChatChannel.LOOC
        )
            return;

        if (_random.Prob(ent.Comp.HearChance))
        {
            var words = args.Message.Split(' ');
            var heardWord = words[_random.Next(words.Length)];
            var finalWord = RemovePunctuation(heardWord);

            var isSelf = ent.Owner != args.Source ? "rmc-deaf-hear-others" : "rmc-deaf-hear-self";
            args.WrappedMessage = Loc.GetString(isSelf, ("message", finalWord));
            args.Message = finalWord;
        }
        else
        {
            var message = Loc.GetString(ent.Owner != args.Source ? "rmc-deaf-talk-others" : "rmc-deaf-talk-self");
            args.WrappedMessage = message;
            args.Message = message;
        }
    }

    private static string RemovePunctuation(string word)
    {
        if (word.Length == 0)
            return word;

        var start = IsPunctuation(word[0]) ? 1 : 0;
        var length = word.Length - start;

        if (length > 0 && IsPunctuation(word[^1]))
            length--;

        if (start == 0 && length == word.Length)
            return word;

        return length <= 0 ? string.Empty : word.Substring(start, length);
    }

    private static bool IsPunctuation(char character)
    {
        return character is ',' or '!' or '.' or ';' or '?';
    }
}
