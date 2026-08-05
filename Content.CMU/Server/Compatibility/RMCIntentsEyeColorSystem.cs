using Content.Server.Humanoid.Systems;
using Content.Shared._RMC14.Humanoid.Markings;
using Content.Shared.CombatMode;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Server._RMC14.Humanoid.Markings;

public sealed partial class RMCIntentsEyeColorSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedCombatModeSystem _combatMode = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCIntentsEyeColorComponent, MapInitEvent>(OnMapInit,
            after: [typeof(RandomHumanoidAppearanceSystem)]);
        SubscribeLocalEvent<RMCIntentsEyeColorComponent, ToggleCombatActionEvent>(OnCombatModeChanged,
            after: [typeof(SharedCombatModeSystem)]);
        SubscribeLocalEvent<RMCIntentsEyeColorComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMapInit(Entity<RMCIntentsEyeColorComponent> ent, ref MapInitEvent args)
        => SetEyeColor(ent, GetColor(ent));

    private void OnCombatModeChanged(Entity<RMCIntentsEyeColorComponent> ent, ref ToggleCombatActionEvent args)
    {
        if (!_mobState.IsDead(ent))
            SetEyeColor(ent, GetColor(ent));
    }

    private void OnMobStateChanged(Entity<RMCIntentsEyeColorComponent> ent, ref MobStateChangedEvent args)
        => SetEyeColor(ent, _mobState.IsDead(ent) ? ent.Comp.DeadEyeColor : GetColor(ent));

    private Color GetColor(Entity<RMCIntentsEyeColorComponent> ent)
        => _combatMode.IsInCombatMode(ent) ? ent.Comp.EyeColorHarm : ent.Comp.EyeColorHelp;

    public void SetEyeColor(EntityUid uid, Color color)
    {
        if (!TryComp(uid, out HumanoidAppearanceComponent? humanoid))
            return;
        humanoid.EyeColor = color;
        Dirty(uid, humanoid);
    }
}
