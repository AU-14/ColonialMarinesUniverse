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
        SubscribeLocalEvent<XenoComponent, MapInitEvent>(OnXenoMapInit, after: [typeof(LanguageSystem)]);
        SubscribeLocalEvent<LanguageComponent, MapInitEvent>(OnLanguageMapInit, after: [typeof(LanguageSystem)]);
        SubscribeLocalEvent<XenoComponent, HiveChangedEvent>(OnXenoHiveChanged);
        SubscribeLocalEvent<XenoComponent, DetermineLanguageEvent>(OnXenoDetermineLanguage);
    }

    private void OnXenoMapInit(Entity<XenoComponent> ent, ref MapInitEvent args)
    {
        RefreshEnglish(ent.Owner);
    }

    private void OnLanguageMapInit(Entity<LanguageComponent> ent, ref MapInitEvent args)
    {
        if (HasComp<XenoComponent>(ent.Owner))
            RefreshEnglish(ent.Owner);
    }

    private void OnXenoHiveChanged(Entity<XenoComponent> ent, ref HiveChangedEvent args)
    {
        RefreshEnglish(ent.Owner);
    }

    private void OnXenoDetermineLanguage(Entity<XenoComponent> ent, ref DetermineLanguageEvent args)
    {
        if (ShouldUseEnglish(ent.Owner))
            args.Language = SharedLanguageSystem.CommonLanguage;
    }

    public void RefreshEnglish(EntityUid uid)
    {
        if (!HasComp<XenoComponent>(uid) ||
            !HasComp<LanguageComponent>(uid))
        {
            return;
        }

        if (ShouldUseEnglish(uid))
        {
            ApplyEnglish(uid);
            return;
        }

        RestoreEnglish(uid);
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

    private void ApplyEnglish(EntityUid uid)
    {
        if (!TryComp(uid, out CMUXenoEnglishLanguageComponent? english))
        {
            english = EnsureComp<CMUXenoEnglishLanguageComponent>(uid);
            english.HadSpokenEnglish = _language.CanSpeak(uid, SharedLanguageSystem.CommonLanguage);
            english.HadUnderstoodEnglish = _language.CanUnderstand(uid, SharedLanguageSystem.CommonLanguage);
            english.PreviousLanguage = _language.GetCurrentLanguage(uid);
        }

        _language.AddLanguage(uid, SharedLanguageSystem.CommonLanguage);
        _language.SetLanguage((uid, null), SharedLanguageSystem.CommonLanguage);
    }

    private void RestoreEnglish(EntityUid uid)
    {
        if (!TryComp(uid, out CMUXenoEnglishLanguageComponent? english))
            return;

        var removeSpoken = !english.HadSpokenEnglish;
        var removeUnderstood = !english.HadUnderstoodEnglish;

        if (removeSpoken || removeUnderstood)
            _language.RemoveLanguage((uid, null), SharedLanguageSystem.CommonLanguage, removeSpoken, removeUnderstood);

        if (_language.CanSpeak(uid, english.PreviousLanguage))
            _language.SetLanguage((uid, null), english.PreviousLanguage);

        RemCompDeferred<CMUXenoEnglishLanguageComponent>(uid);
    }
}
