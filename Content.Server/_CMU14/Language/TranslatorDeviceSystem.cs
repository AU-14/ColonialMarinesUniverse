using Content.Server._RMC14.Language.Systems;
using Content.Shared._CMU14.Language;
using Content.Shared.Inventory.Events;
using Content.Shared._RMC14.Language.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Language;

public sealed partial class TranslatorDeviceSystem : EntitySystem
{
    [Dependency] private LanguageSystem _language = default!;

    private readonly Dictionary<(EntityUid User, ProtoId<LanguagePrototype> Language, bool Spoken), LanguageGrant> _grants = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<TranslatorDeviceComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<TranslatorDeviceComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<TranslatorDeviceComponent> ent, ref GotEquippedEvent args)
    {
        foreach (var lang in ent.Comp.SpokenLanguages)
            AddGrant(args.Equipee, lang, spoken: true);

        foreach (var lang in ent.Comp.UnderstoodLanguages)
            AddGrant(args.Equipee, lang, spoken: false);
    }

    private void OnUnequipped(Entity<TranslatorDeviceComponent> ent, ref GotUnequippedEvent args)
    {
        foreach (var lang in ent.Comp.SpokenLanguages)
            RemoveGrant(args.Equipee, lang, spoken: true);

        foreach (var lang in ent.Comp.UnderstoodLanguages)
            RemoveGrant(args.Equipee, lang, spoken: false);
    }

    private void AddGrant(EntityUid user, ProtoId<LanguagePrototype> language, bool spoken)
    {
        var key = (user, language, spoken);
        if (_grants.TryGetValue(key, out var grant))
        {
            _grants[key] = grant with { Count = grant.Count + 1 };
            return;
        }

        var alreadyKnown = spoken
            ? _language.CanSpeak(user, language)
            : _language.CanUnderstand(user, language);
        _grants[key] = new LanguageGrant(1, alreadyKnown);
        _language.AddLanguage(user, language, addSpoken: spoken, addUnderstood: !spoken);
    }

    private void RemoveGrant(EntityUid user, ProtoId<LanguagePrototype> language, bool spoken)
    {
        var key = (user, language, spoken);
        if (!_grants.TryGetValue(key, out var grant))
            return;

        if (grant.Count > 1)
        {
            _grants[key] = grant with { Count = grant.Count - 1 };
            return;
        }

        _grants.Remove(key);
        if (!grant.AlreadyKnown)
            _language.RemoveLanguage(user, language, removeSpoken: spoken, removeUnderstood: !spoken);
    }

    private sealed record LanguageGrant(int Count, bool AlreadyKnown);
}
