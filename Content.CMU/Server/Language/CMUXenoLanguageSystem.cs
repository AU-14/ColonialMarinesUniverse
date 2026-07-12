using Content.Server._RMC14.Language.Systems;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Language;
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Systems;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;

namespace Content.Server._CMU14.Language;

public sealed partial class CMUXenoLanguageSystem : EntitySystem
{
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private LanguageSystem _language = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoComponent, DetermineEntityLanguagesEvent>(OnXenoDetermineEntityLanguages);
        SubscribeLocalEvent<XenoComponent, DetermineLanguageEvent>(OnXenoDetermineLanguage);
        SubscribeLocalEvent<LanguageComponent, HiveChangedEvent>(OnLanguageHiveChanged);
    }

    private void OnXenoDetermineEntityLanguages(Entity<XenoComponent> ent, ref DetermineEntityLanguagesEvent args)
    {
        if (!ShouldUseEnglish(ent.Owner))
        {
            args.SpokenLanguages.Remove(SharedLanguageSystem.CommonLanguage);
            args.UnderstoodLanguages.Remove(SharedLanguageSystem.CommonLanguage);
            return;
        }

        args.SpokenLanguages.Add(SharedLanguageSystem.CommonLanguage);
        args.UnderstoodLanguages.Add(SharedLanguageSystem.CommonLanguage);
    }

    private void OnXenoDetermineLanguage(Entity<XenoComponent> ent, ref DetermineLanguageEvent args)
    {
        if (ShouldUseEnglish(ent.Owner) &&
            !_language.CanSpeak(ent.Owner, args.Language))
        {
            args.Language = SharedLanguageSystem.CommonLanguage;
        }
    }

    private void OnLanguageHiveChanged(Entity<LanguageComponent> ent, ref HiveChangedEvent args)
    {
        RefreshEnglish(ent.Owner);
    }

    public void RefreshEnglish(EntityUid uid)
    {
        if (!HasComp<XenoComponent>(uid) ||
            !HasComp<LanguageComponent>(uid))
        {
            return;
        }

        _language.UpdateEntityLanguages(uid);
        if (ShouldUseEnglish(uid) &&
            _language.CanSpeak(uid, SharedLanguageSystem.CommonLanguage))
        {
            _language.SetLanguage(uid, SharedLanguageSystem.CommonLanguage);
        }
    }

    private bool ShouldUseEnglish(EntityUid uid)
    {
        return IsHivebrokenXeno(uid) ||
               _hive.GetHive(uid) is { Comp.Corrupted: true };
    }

    private bool IsHivebrokenXeno(EntityUid uid)
    {
        return HasComp<YautjaHivebrokenXenoComponent>(uid) ||
               TryComp(uid, out YautjaThrallComponent? thrall) && thrall.Hivebroken;
    }

}
