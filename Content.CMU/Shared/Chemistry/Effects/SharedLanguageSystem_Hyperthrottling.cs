/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Language.Systems;

// LanguageComponent is [Access]-restricted to SharedLanguageSystem and SharedLanguageLearningSystem,
// so the writes for the Hyperthrottling chemical property live here as a partial.
public abstract partial class SharedLanguageSystem
{
    /// <summary>
    /// Adds every language prototype (minus <paramref name="excluded"/>) to the entity's spoken and understood sets.
    /// Only languages that were not already known are recorded in the two "added" sets, so
    /// <see cref="RevokeGrantedLanguages"/> can later remove exactly what was granted and nothing else.
    /// </summary>
    public void GrantAllLanguages(
        EntityUid uid,
        List<ProtoId<LanguagePrototype>> excluded,
        HashSet<ProtoId<LanguagePrototype>> addedSpoken,
        HashSet<ProtoId<LanguagePrototype>> addedUnderstood)
    {
        if (!TryComp(uid, out LanguageComponent? comp))
            return;

        foreach (var proto in _prototypeManager.EnumeratePrototypes<LanguagePrototype>())
        {
            var id = new ProtoId<LanguagePrototype>(proto.ID);
            if (excluded.Contains(id))
                continue;

            if (comp.SpokenLanguages.Add(id))
                addedSpoken.Add(id);

            if (comp.UnderstoodLanguages.Add(id))
                addedUnderstood.Add(id);
        }

        Dirty(uid, comp);
    }

    /// <summary>
    /// Removes only the languages a previous <see cref="GrantAllLanguages"/> added. Anything the entity
    /// learned or was given in the meantime (e.g. a pamphlet) is kept.
    /// </summary>
    public void RevokeGrantedLanguages(
        EntityUid uid,
        HashSet<ProtoId<LanguagePrototype>> addedSpoken,
        HashSet<ProtoId<LanguagePrototype>> addedUnderstood)
    {
        if (!TryComp(uid, out LanguageComponent? comp))
            return;

        comp.SpokenLanguages.ExceptWith(addedSpoken);
        comp.UnderstoodLanguages.ExceptWith(addedUnderstood);

        // If they were speaking a language they can no longer speak, fall back to their default.
        if (comp.CurrentLanguage is { } current && !comp.SpokenLanguages.Contains(current))
            comp.CurrentLanguage = null;

        Dirty(uid, comp);
    }
}
