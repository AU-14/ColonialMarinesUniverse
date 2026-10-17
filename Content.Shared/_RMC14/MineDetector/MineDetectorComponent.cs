using Content.Shared._RMC14.MotionDetector;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.MineDetector;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
[Access(typeof(MineDetectorSystem))]
public sealed partial class MineDetectorComponent : Component, IDetectorComponent
{
    [DataField, AutoNetworkedField] public bool Enabled;

    [DataField, AutoNetworkedField] public int Range;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextScanAt;

    [DataField, AutoNetworkedField] public bool CanToggleRange = true;

    [DataField, AutoNetworkedField] public bool Short;

    // You can edit the motion detector ranges here:
    [DataField, AutoNetworkedField] public int ShortRange = 4;
    [DataField, AutoNetworkedField] public int LongRange = 9;

    [DataField, AutoNetworkedField] public TimeSpan ShortRefresh = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField] public TimeSpan LongRefresh = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public List<Blip> Blips { get; set; } = new();

    [DataField, AutoNetworkedField]
    public TimeSpan LastScan { get; set; }

    [DataField, AutoNetworkedField]
    public TimeSpan ScanDuration { get; set; } = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField] public SoundSpecifier? ScanSound = new SoundPathSpecifier("/Audio/_RMC14/Effects/motion_detector.ogg", AudioParams.Default.WithMaxDistance(7f));

    [DataField, AutoNetworkedField] public SoundSpecifier? ScanEmptySound = new SoundPathSpecifier("/Audio/_RMC14/Effects/motion_detector_none.ogg");

    [DataField, AutoNetworkedField] public SoundSpecifier? ToggleSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/click.ogg");

    [DataField, AutoNetworkedField] public bool HandToggleable = true;

    [DataField, AutoNetworkedField] public bool DeactivateOnDrop = true;

    [DataField, AutoNetworkedField] public EntityUid? LastUser;
}

[Serializable, NetSerializable]
public enum MineDetectorLayer
{
    Setting,
}

[Serializable, NetSerializable]
public enum MineDetectorVisualLayers
{
    Base,
    Folded,
}

[Serializable, NetSerializable]
public enum MineDetectorSetting
{
    Short,
    Long,
}
