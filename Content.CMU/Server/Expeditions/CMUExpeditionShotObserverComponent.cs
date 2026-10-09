namespace Content.Server.CMU14.Expeditions;

/// <summary>
/// Gives expedition perception its own directed shot subscription without taking the
/// GunComponent subscription already owned by the native ghillie system.
/// </summary>
[RegisterComponent]
public sealed partial class CMUExpeditionShotObserverComponent : Component;
