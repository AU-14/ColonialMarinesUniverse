using Content.Server._RMC14.Language.Systems;
using Content.Shared._RMC14.Language;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.CMU14.Language;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Language;

public sealed partial class AllLanguagesSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private LanguageSystem _language = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AllLanguagesComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AllLanguagesComponent, DetermineEntityLanguagesEvent>(OnDetermineLanguages);
    }

    private void OnStartup(Entity<AllLanguagesComponent> ent, ref ComponentStartup args)
        => _language.UpdateEntityLanguages(ent.Owner);

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
