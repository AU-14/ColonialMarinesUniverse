using Content.Server.Forensics;
using Content.Shared._CMU14.Item.Stain;
using Content.Shared.Forensics;

namespace Content.Server._CMU14.Item.Stain;

/// <summary>
/// Clears visual stains when an existing forensic-cleaning do-after completes.
/// </summary>
public sealed partial class CMUItemStainCleaningSystem : EntitySystem
{
    [Dependency] private CMUItemStainSystem _stains = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CleansForensicsComponent, CleanForensicsDoAfterEvent>(OnCleanDoAfter);
    }

    private void OnCleanDoAfter(Entity<CleansForensicsComponent> ent, ref CleanForensicsDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } target)
            return;

        _stains.TryClean(target);
    }
}
