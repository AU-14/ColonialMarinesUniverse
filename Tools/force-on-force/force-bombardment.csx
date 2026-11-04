using System.Linq;
// Run in scsi while controlling a character on a floor map.
// 0: Rolling thunder; 1: Laser curtain; 2: Meteor strike; 3: Shock and awe.
// This uses the real alarm, red popup, four shuffled arrangements, and safety checks.
ressys<Content.Server.CMU14.ForceOnForce.ForceOnForceBombardmentSystem>().ForcePreview(
    res<Robust.Server.Player.IPlayerManager>().Sessions.Single().AttachedEntity.Value, 0);
