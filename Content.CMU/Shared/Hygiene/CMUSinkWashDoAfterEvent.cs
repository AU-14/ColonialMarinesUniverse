using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using System;

namespace Content.Shared.CMU14.Hygiene;

[Serializable, NetSerializable]
public sealed partial class CMUSinkWashDoAfterEvent : SimpleDoAfterEvent;
