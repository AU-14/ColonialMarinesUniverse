using Content.Shared.Access.Systems;
using Content.Shared.CMU14.Access;
using Content.Shared.GameTicking;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Access;

/// <summary>
///     Writes a spawning character's name onto any cover ID cards they carry.
/// </summary>
public sealed partial class CMUCoverIdCardSystem : EntitySystem
{
    [Dependency] private SharedIdCardSystem _idCard = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        ApplyCoverNames(args.Mob);
    }

    /// <summary>
    ///     Names every cover ID card on <paramref name="mob"/>, including ones packed inside worn storage.
    /// </summary>
    public void ApplyCoverNames(EntityUid mob)
    {
        var name = Name(mob);
        var stack = new Stack<EntityUid>();
        stack.Push(mob);

        while (stack.TryPop(out var current))
        {
            if (!TryComp(current, out ContainerManagerComponent? containers))
                continue;

            foreach (var container in containers.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (HasComp<CMUCoverIdCardComponent>(contained))
                        _idCard.TryChangeFullName(contained, name);

                    stack.Push(contained);
                }
            }
        }
    }
}
