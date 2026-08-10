using System.Linq;
using Content.Server.GameTicking.Events;
using Content.Shared._RMC14.Language;
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Language.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Mentor.ImaginaryFriend;

namespace Content.Server._RMC14.Language.Systems;

public sealed partial class LanguageSystem : SharedLanguageSystem
{
    [Dependency] private LanguageLearningSystem _learning = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IComponentFactory _compFactory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LanguageComponent, MapInitEvent>(OnInitLanguageSpeaker);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeNetworkEvent<LanguagesSetMessage>(OnClientSetLanguage);
        SubscribeLocalEvent<RemoveLanguageComponent, DetermineEntityLanguagesEvent>(OnRemoveLanguages);
        SubscribeLocalEvent<RemoveLanguageComponent, ComponentStartup>(OnRemoveLanguageStartup);
    }

    private void OnInitLanguageSpeaker(Entity<LanguageComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Preset is { } presetId && presetId.TryGet(out var preset, _prototypeManager, _compFactory))
        {
            ent.Comp.SpokenLanguages = new(preset.SpokenLanguages);
            ent.Comp.UnderstoodLanguages = new(preset.UnderstoodLanguages);
            ent.Comp.CurrentLanguage ??= preset.CurrentLanguage;
            ent.Comp.DefaultLanguage ??= preset.DefaultLanguage;
        }

        if (ent.Comp.CurrentLanguage == null)
            ent.Comp.CurrentLanguage = ent.Comp.DefaultLanguage ?? ent.Comp.SpokenLanguages.FirstOrDefault();

        UpdateEntityLanguages(ent.AsNullable());
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        ReseedObfuscationForRound();
    }

    private void OnClientSetLanguage(LanguagesSetMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { Valid: true } uid)
            return;

        if (!TryComp<LanguageComponent>(uid, out var component))
            return;

        if (!CanSpeak(uid, message.CurrentLanguage))
            return;

        SetLanguage(uid, message.CurrentLanguage);
    }

    private void OnRemoveLanguages(
        Entity<RemoveLanguageComponent> ent,
        ref DetermineEntityLanguagesEvent args)
    {
        foreach (var language in ent.Comp.Languages)
        {
            // Don't remove the last spoken language
            if (ent.Comp.RemoveSpoken &&
                args.SpokenLanguages.Count > 1)
            {
                args.SpokenLanguages.Remove(language);
            }

            // Don't remove the last understood language
            if (ent.Comp.RemoveUnderstood &&
                args.UnderstoodLanguages.Count > 1)
            {
                args.UnderstoodLanguages.Remove(language);
            }
        }
    }

    private void OnRemoveLanguageStartup(Entity<RemoveLanguageComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<LanguageComponent>(ent.Owner, out var language))
            return;

        foreach (var lang in ent.Comp.Languages)
        {
            if (ent.Comp.RemoveSpoken)
                language.SpokenLanguages.Remove(lang);

            if (ent.Comp.RemoveUnderstood)
                language.UnderstoodLanguages.Remove(lang);

            if (ent.Comp.ClearCurrentLanguage &&
                language.CurrentLanguage == lang)
            {
                language.CurrentLanguage = null;
            }
        }

        UpdateEntityLanguages(ent.Owner);
    }

    public void SetLanguage(Entity<LanguageComponent?> ent, ProtoId<LanguagePrototype> language)
    {
        if (!CanSpeak(ent, language) || !Resolve(ent, ref ent.Comp) || ent.Comp.CurrentLanguage == language)
            return;

        ent.Comp.CurrentLanguage = language;
        var update = new LanguagesUpdateEvent();
        RaiseLocalEvent(ent, ref update, true);
        Dirty(ent);
    }

    public void AddLanguage(
        EntityUid uid,
        ProtoId<LanguagePrototype> language,
        bool addSpoken = true,
        bool addUnderstood = true)
    {
        if (!TryComp<LanguageComponent>(uid, out var component))
            return;

        if (addSpoken && !component.SpokenLanguages.Contains(language))
            component.SpokenLanguages.Add(language);

        if (addUnderstood && !component.UnderstoodLanguages.Contains(language))
            component.UnderstoodLanguages.Add(language);

        UpdateEntityLanguages((uid, component));
    }

    public void RemoveLanguage(
        Entity<LanguageComponent?> ent,
        ProtoId<LanguagePrototype> language,
        bool removeSpoken = true,
        bool removeUnderstood = true)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (removeSpoken)
            ent.Comp.SpokenLanguages.Remove(language);

        if (removeUnderstood)
            ent.Comp.UnderstoodLanguages.Remove(language);

        UpdateEntityLanguages(ent.Owner);
    }

    public bool TryFixCurrentLanguage(Entity<LanguageComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        ProtoId<LanguagePrototype>? target = null;
        if (ent.Comp.SpokenLanguages.Count == 0)
            target = null;
        else if (ent.Comp.DefaultLanguage != null && ent.Comp.SpokenLanguages.Contains(ent.Comp.DefaultLanguage.Value))
            target = ent.Comp.DefaultLanguage.Value;
        else
            target = ent.Comp.SpokenLanguages.First();

        if (ent.Comp.CurrentLanguage == target)
            return false;

        ent.Comp.CurrentLanguage = target;
        var update = new LanguagesUpdateEvent();
        RaiseLocalEvent(ent, ref update);
        Dirty(ent);
        return true;
    }

    public void UpdateEntityLanguages(Entity<LanguageComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var ev = new DetermineEntityLanguagesEvent();

        foreach (var spoken in ent.Comp.SpokenLanguages)
            ev.SpokenLanguages.Add(spoken);

        foreach (var understood in ent.Comp.UnderstoodLanguages)
            ev.UnderstoodLanguages.Add(understood);

        RaiseLocalEvent(ent, ref ev);

        ent.Comp.SpokenLanguages.Clear();
        ent.Comp.UnderstoodLanguages.Clear();

        ent.Comp.SpokenLanguages.UnionWith(ev.SpokenLanguages);
        ent.Comp.UnderstoodLanguages.UnionWith(ev.UnderstoodLanguages);

        if (!TryFixCurrentLanguage(ent))
        {
            var update = new LanguagesUpdateEvent();
            RaiseLocalEvent(ent, ref update);
        }

        Dirty(ent);
    }

    public string ObfuscateMessageForSpeaker(EntityUid speaker, string message, ProtoId<LanguagePrototype> language)
    {
        if (CanUnderstand(speaker, language))
            return message;

        if (TryComp<LanguageLearningComponent>(speaker, out var learningComp) &&
            learningComp.Languages.ContainsKey(language))
        {
            return _learning.ProcessMessageForSpeaker(speaker, message, language);
        }

        var languageLearningEv = new ProcessSpeakerLanguageEvent(speaker, language, message);
        RaiseLocalEvent(speaker, ref languageLearningEv);
        return languageLearningEv.ProcessedMessage;
    }

    public string ObfuscateMessageForListener(EntityUid listener, string speakerMessage, ProtoId<LanguagePrototype> language, EntityUid speaker)
    {
        if (CanUnderstand(listener, language))
            return speakerMessage;

        // imaginary friends always speak in a way their owner understands
        if (TryComp<ImaginaryFriendComponent>(speaker, out var friend) && friend.Imaginer == listener)
            return speakerMessage;

        var similarity = GetSisterLanguageSimilarity(listener, language);

        if (TryComp<LanguageLearningComponent>(listener, out var learningComp) &&
            learningComp.Languages.ContainsKey(language))
        {
            var learnedData = learningComp.Languages[language];

            // if learning is better than sister language, use learning system fully
            if (learnedData.Progress >= similarity)
                return _learning.ProcessMessageForListener(listener, speakerMessage, language);

            // otherwise combine — sister language baseline + individually learned words
            if (similarity > 0f || learnedData.LearnedWords.Count > 0)
                return ObfuscateMessageSisterLanguageWithLearning(speakerMessage, language, similarity, learnedData);
        }

        if (similarity > 0f)
            return ObfuscateMessageSisterLanguage(speakerMessage, language, similarity);

        return ObfuscateMessage(speakerMessage, language);
    }

    public void SetExclusiveLanguage(Entity<LanguageComponent?> ent, ProtoId<LanguagePrototype> language)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        ent.Comp.SpokenLanguages.Clear();
        ent.Comp.UnderstoodLanguages.Clear();

        ent.Comp.SpokenLanguages.Add(language);
        ent.Comp.UnderstoodLanguages.Add(language);

        ent.Comp.CurrentLanguage = language;
        ent.Comp.DefaultLanguage = language;

        UpdateEntityLanguages(ent);
    }

    private string ObfuscateMessageSisterLanguageWithLearning(
        string message,
        ProtoId<LanguagePrototype> language,
        float similarity,
        LanguageLearningData learnedData)
    {
        if (!_prototypeManager.TryIndex(language, out var proto))
            return ObfuscateMessage(message, language);

        var words = message.Split(' ');
        var result = new System.Text.StringBuilder();

        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];

            if (i > 0)
                result.Append(' ');

            if (string.IsNullOrWhiteSpace(word))
            {
                result.Append(word);
                continue;
            }

            var wordLower = word.ToLowerInvariant();

            // check if this specific word was individually learned above threshold
            var wordComprehension = learnedData.LearnedWords.GetValueOrDefault(wordLower, 0f);
            if (wordComprehension >= proto.ClearComprehensionThreshold)
            {
                result.Append(word);
                continue;
            }

            // fall back to sister language probability
            var roll = (float) PseudoRandomNumber(word.GetHashCode() + i, 0, 1000) / 1000f;
            if (roll < similarity)
            {
                result.Append(word);
            }
            else
            {
                result.Append(ObfuscateMessageInternal(word, proto.ObfuscationMethod, proto.RandomizeObfuscation));
            }
        }

        return result.ToString();
    }

    private string ObfuscateMessageSisterLanguage(string message, ProtoId<LanguagePrototype> language, float similarity)
    {
        if (!_prototypeManager.TryIndex(language, out var proto))
            return ObfuscateMessage(message, language);

        var words = message.Split(' ');
        var result = new System.Text.StringBuilder();

        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];

            if (i > 0)
                result.Append(' ');

            if (string.IsNullOrWhiteSpace(word))
            {
                result.Append(word);
                continue;
            }

            // use word index as seed so same word always shows/hides consistently per round
            var roll = (float) PseudoRandomNumber(word.GetHashCode() + i, 0, 1000) / 1000f;

            if (roll < similarity)
            {
                // show full word
                result.Append(word);
            }
            else
            {
                // fully obfuscate this word
                result.Append(ObfuscateMessageInternal(word, proto.ObfuscationMethod, proto.RandomizeObfuscation));
            }
        }

        return result.ToString();
    }
}