using Content.Server.Humanoid.Systems;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Humanoid;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.Configuration;

namespace Content.Server._RMC14.Humanoid;

public sealed class HiddenAppearanceSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private HumanoidProfileSystem _profile = default!;

    private bool _hidePlayerIdentities;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RMCSetGenderOnMapInitComponent, MapInitEvent>(
            OnSetGenderMapInit,
            after: [typeof(RandomHumanoidAppearanceSystem)]);

        Subs.CVar(_config, RMCCVars.HidePlayerIdentities, OnHidePlayerIdentitiesChanged, true);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!_hidePlayerIdentities ||
            !TryComp<HiddenAppearanceComponent>(args.Mob, out var hidden) ||
            !TryComp<HumanoidProfileComponent>(args.Mob, out var profile))
        {
            return;
        }

        var random = HumanoidCharacterProfile.RandomWithSpecies(profile.Species);
        SetHiddenAppearance((args.Mob, hidden), new HiddenHumanoidAppearance(random.Species, random.Sex, random.Appearance));
    }

    public void SetHiddenAppearance(
        Entity<HiddenAppearanceComponent?> ent,
        HiddenHumanoidAppearance appearance)
    {
        var hidden = EnsureComp<HiddenAppearanceComponent>(ent.Owner);
        hidden.Appearance = new HiddenHumanoidAppearance(appearance.Species, appearance.Sex, appearance.Appearance);
        Dirty(ent.Owner, hidden);
    }

    private void OnSetGenderMapInit(Entity<RMCSetGenderOnMapInitComponent> ent, ref MapInitEvent args)
    {
        _profile.SetGender(ent.Owner, ent.Comp.Gender);
    }

    private void OnHidePlayerIdentitiesChanged(bool value)
    {
        _hidePlayerIdentities = value;
        if (value)
            return;

        var query = EntityQueryEnumerator<HiddenAppearanceComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            RemCompDeferred<HiddenAppearanceComponent>(uid);
        }
    }
}
