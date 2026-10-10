namespace Content.Server.CMU14.Expeditions;

/// <summary>
/// Gives expedition perception its own directed shot subscription without taking the
/// GunComponent subscription already owned by the native ghillie system. Rebuilt on use;
/// this runtime observer must not be written into map files.
/// </summary>
[RegisterComponent, UnsavedComponent]
public sealed partial class CMUExpeditionShotObserverComponent : Component;
