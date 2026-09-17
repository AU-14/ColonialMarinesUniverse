using Content.Shared._RMC14.Language;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.CMU14.Language;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Language;

public sealed partial class AllLanguagesSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AllLanguagesComponent, DetermineEntityLanguagesEvent>(OnDetermineLanguages);
    }

    private void OnDetermineLanguages(Entity<AllLanguagesComponent> ent, ref DetermineEntityLanguagesEvent args)
    {
        foreach (var proto in _proto.EnumeratePrototypes<LanguagePrototype>())
        {
            var id = new ProtoId<LanguagePrototype>(proto.ID);

            args.SpokenLanguages.Add(id);
            args.UnderstoodLanguages.Add(id);
        }
    }
}
