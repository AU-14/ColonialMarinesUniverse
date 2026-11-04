using Content.Shared.CMU14.Clothing;
using Content.Shared.Inventory.Events;
using Robust.Server.Audio;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Clothing;

public sealed class CMUSelfEquipSoundSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSelfEquipSoundComponent, GotEquippedEvent>(OnGotEquipped);
    }

    private void OnGotEquipped(Entity<CMUSelfEquipSoundComponent> ent, ref GotEquippedEvent args)
    {
        _audio.PlayGlobal(ent.Comp.Sound, Filter.Entities(args.EquipTarget), false);
    }
}
