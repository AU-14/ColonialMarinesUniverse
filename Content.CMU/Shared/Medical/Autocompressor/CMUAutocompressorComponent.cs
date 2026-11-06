// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Medical.Autocompressor;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(CMUAutocompressorSystem))]
public sealed partial class CMUAutocompressorComponent : Component
{
    [DataField]
    public TimeSpan AttachDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public int MedicalSkill = 2;

    [DataField]
    public TimeSpan CompressionInterval = TimeSpan.FromSeconds(7.5);

    [DataField]
    public TimeSpan FractureDelay = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan CompoundFractureDelay = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan ShatteredFractureDelay = TimeSpan.FromMinutes(3);

    [DataField]
    public FixedPoint2 LungDamage = 5;

    [DataField]
    public bool LungDamageApplied;

    [DataField]
    public bool Training;

    [DataField]
    public float ChargePerCompression = 20;

    [DataField]
    public float MinimumCharge = 50;

    [DataField, AutoNetworkedField]
    public EntityUid? Patient;

    [DataField, AutoNetworkedField]
    public bool Running;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextCompression;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan CompressionStartedAt;

    [DataField]
    public FractureSeverity FractureStage = FractureSeverity.None;

}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(CMUAutocompressorSystem))]
public sealed partial class CMUAutocompressorPatientComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Device;

    [DataField, AutoNetworkedField]
    public bool Running;
}

[RegisterComponent]
public sealed partial class CMUAutocompressorRunningComponent : Component;

[Serializable, NetSerializable]
public enum CMUAutocompressorVisuals : byte { Running, Battery }

[Serializable, NetSerializable]
public enum CMUAutocompressorLayers : byte { Device, Battery }

[Serializable, NetSerializable]
public enum CMUAutocompressorBattery : byte { Empty, Low, Medium, High }
