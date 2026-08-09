using Content.Shared._RMC14.Targeting;
using Content.Shared._RMC14.Weapons.Ranged.Brute;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Client.CMU.Weapons.Ranged.Brute;

/// <summary>
/// Selects the CMU-owned guided lock-on art when a BRUTE launcher targets an entity.
/// </summary>
public sealed class CMUBruteTargetingVisualSystem : EntitySystem
{
    private static readonly ResPath BruteTargetedRsi = new("/Textures/_CMU14/Effects/targeted_brute.rsi");
    private static readonly ResPath DefaultTargetedRsi = new("/Textures/_RMC14/Effects/targeted.rsi");
    private const string DefaultLockOnState = "sniper_lockon";
    private const string DefaultLockOnDirectionState = "sniper_lockon_direction";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCTargetedComponent, ComponentStartup>(OnTargetedStartup);
        SubscribeLocalEvent<RMCTargetedComponent, AfterAutoHandleStateEvent>(OnTargetedState);
    }

    private void OnTargetedStartup(Entity<RMCTargetedComponent> ent, ref ComponentStartup args)
    {
        RefreshTargetingVisuals(ent.Comp);
    }

    private void OnTargetedState(Entity<RMCTargetedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshTargetingVisuals(ent.Comp);
    }

    private void RefreshTargetingVisuals(RMCTargetedComponent targeted)
    {
        foreach (var targeter in targeted.TargetedBy)
        {
            if (!TryComp(targeter, out RMCBruteLauncherComponent? brute))
                continue;

            targeted.RsiPath = BruteTargetedRsi;
            targeted.LockOnState = brute.LockOnState;
            targeted.LockOnStateDirection = brute.LockOnStateDirection;
            return;
        }

        if (targeted.RsiPath != BruteTargetedRsi)
            return;

        targeted.RsiPath = DefaultTargetedRsi;
        targeted.LockOnState = DefaultLockOnState;
        targeted.LockOnStateDirection = DefaultLockOnDirectionState;
    }
}
