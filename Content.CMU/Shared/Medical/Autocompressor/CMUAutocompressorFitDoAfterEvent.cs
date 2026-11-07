// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Medical.Autocompressor;

[Serializable, NetSerializable]
public sealed partial class CMUAutocompressorFitDoAfterEvent : SimpleDoAfterEvent;
