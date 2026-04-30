using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Bones.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.Bones;

public sealed class BoneSystem : SharedBoneSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier BoneBreakSound =
        new SoundCollectionSpecifier("CMUBoneBreakSounds");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BoneComponent, BoneFracturedEvent>(OnFractured);
    }

    private void OnFractured(Entity<BoneComponent> ent, ref BoneFracturedEvent args)
    {
        if (!args.New.IsAtLeast(FractureSeverity.Compound))
            return;
        _audio.PlayPvs(BoneBreakSound, ent.Owner);
    }
}
