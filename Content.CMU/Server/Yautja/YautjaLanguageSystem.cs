using Content.Shared._RMC14.Language;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja;

public sealed partial class YautjaLanguageSystem : EntitySystem
{
    private static readonly ProtoId<LanguagePrototype> Yautja = "Yautja";
    private static readonly ProtoId<LanguagePrototype> Xeno = "Xeno";

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, DetermineEntityLanguagesEvent>(OnDetermineLanguages);
    }

    private void OnDetermineLanguages(Entity<YautjaComponent> ent, ref DetermineEntityLanguagesEvent args)
    {
        args.SpokenLanguages.Add(Yautja);
        args.UnderstoodLanguages.Add(Yautja);
        args.SpokenLanguages.Add(Xeno);
        args.UnderstoodLanguages.Add(Xeno);
    }
}
