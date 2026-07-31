using System.Linq;
using Content.Server._RMC14.Language.Systems;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Language;
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Language.Systems;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Language;

public sealed partial class CMUXenoLanguageSystem : EntitySystem
{
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private LanguageSystem _language = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private static readonly HashSet<string> CorruptedXenoExcludedSpoken = new()
    {
        "Primitive",
        "Binary",
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoComponent, DetermineEntityLanguagesEvent>(OnXenoDetermineEntityLanguages);
        SubscribeLocalEvent<XenoComponent, DetermineLanguageEvent>(OnXenoDetermineLanguage);
        SubscribeLocalEvent<LanguageComponent, DetermineEntityLanguagesEvent>(OnDetermineCorruptedHiveLanguages);
        SubscribeLocalEvent<LanguageComponent, HiveChangedEvent>(OnLanguageHiveChanged);
    }

    private void OnDetermineCorruptedHiveLanguages(Entity<LanguageComponent> ent, ref DetermineEntityLanguagesEvent args)
    {
        if (!TryComp<HiveMemberComponent>(ent.Owner, out var hiveMember) ||
            !TryComp<HiveComponent>(hiveMember.Hive, out var hive) ||
            !hive.Corrupted)
            return;

        foreach (var proto in _prototypeManager.EnumeratePrototypes<LanguagePrototype>())
        {
            var id = new ProtoId<LanguagePrototype>(proto.ID);

            if (!CorruptedXenoExcludedSpoken.Contains(proto.ID))
                args.SpokenLanguages.Add(id);

            args.UnderstoodLanguages.Add(id);
        }
    }

    private void OnLanguageHiveChanged(Entity<LanguageComponent> ent, ref HiveChangedEvent args)
    {
        _language.UpdateEntityLanguages(ent.AsNullable());
        RefreshEnglish(ent.Owner);
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

    public void RefreshEnglish(EntityUid uid)
    {
        if (!HasComp<XenoComponent>(uid) ||
            !HasComp<LanguageComponent>(uid))
            return;

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
