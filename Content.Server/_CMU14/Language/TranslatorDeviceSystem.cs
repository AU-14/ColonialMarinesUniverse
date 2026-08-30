using Content.Server._RMC14.Language.Systems;
using Content.Shared.CMU14.Language;
using Content.Shared.Inventory.Events;

namespace Content.Server.CMU14.Language;

public sealed partial class TranslatorDeviceSystem : EntitySystem
{
    [Dependency] private LanguageSystem _language = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TranslatorDeviceComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<TranslatorDeviceComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<TranslatorDeviceComponent> ent, ref GotEquippedEvent args)
    {
        foreach (var lang in ent.Comp.SpokenLanguages)
                _language.AddLanguage(args.EquipTarget, lang, addSpoken: true, addUnderstood: false);

        foreach (var lang in ent.Comp.UnderstoodLanguages)
                _language.AddLanguage(args.EquipTarget, lang, addSpoken: false, addUnderstood: true);
    }

    private void OnUnequipped(Entity<TranslatorDeviceComponent> ent, ref GotUnequippedEvent args)
    {
        foreach (var lang in ent.Comp.SpokenLanguages)
                _language.RemoveLanguage(args.EquipTarget, lang, removeSpoken: true, removeUnderstood: false);

        foreach (var lang in ent.Comp.UnderstoodLanguages)
                _language.RemoveLanguage(args.EquipTarget, lang, removeSpoken: false, removeUnderstood: true);
    }
}
