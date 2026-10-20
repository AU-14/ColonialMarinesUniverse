using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Photography;

/// <summary>
/// A camera that photographs a square of tiles: click a tile while holding it and a print of that area, as the
/// photographer sees it, comes out into the photographer's hand. Alt click cycles the frame size. Each shot uses
/// film, as SS13's cameras did: ten shots a roll. An empty camera only clicks until a fresh
/// <see cref="CMUCameraFilmComponent"/> roll is loaded.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUPhotoCameraComponent : Component
{
    /// <summary>Frame sizes in tiles, all odd so the clicked tile is the centre.</summary>
    [DataField]
    public int[] Sizes = { 1, 3, 5, 7 };

    [DataField, AutoNetworkedField]
    public int Size = 3;

    /// <summary>How far from the photographer, in tiles, the clicked tile may be.</summary>
    [DataField]
    public float Range = 10;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(2);

    /// <summary>Shots left on the loaded roll. A new camera comes loaded.</summary>
    [DataField, AutoNetworkedField]
    public int Shots = 10;

    /// <summary>Shots on a fresh roll.</summary>
    [DataField]
    public int MaxShots = 10;

    [DataField, AutoPausedField]
    public TimeSpan NextPhoto;

    /// <summary>How long after the shutter the print comes out, so the two sounds do not run together.</summary>
    [DataField]
    public TimeSpan PrintDelay = TimeSpan.FromSeconds(0.6);

    /// <summary>How soon an empty camera can be clicked again.</summary>
    [DataField]
    public TimeSpan EmptyCooldown = TimeSpan.FromSeconds(0.5);

    [DataField]
    public SoundSpecifier ShutterSound = new SoundPathSpecifier("/Audio/CMU14/Photography/camera_shutter.ogg");

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/CMU14/Photography/polaroid_print.ogg",
        AudioParams.Default.WithVolume(-4));

    /// <summary>The dry click of the shutter with no film behind it.</summary>
    [DataField]
    public SoundSpecifier EmptySound = new SoundPathSpecifier("/Audio/Weapons/Guns/Empty/empty.ogg",
        AudioParams.Default.WithVolume(-6));

    /// <summary>A brief light at the photographer as the shutter fires.</summary>
    [DataField]
    public EntProtoId? Flash = "CMUEffectCameraFlash";
}

/// <summary>A roll of camera film. Using it on a photo camera reloads the camera to a full roll.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUCameraFilmComponent : Component;

/// <summary>
/// A printed photograph. The picture itself is held by the server and fetched by <see cref="Id"/>; a tiny
/// thumbnail of it travels with the print so it shows on the item's sprite.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class CMUPhotoComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Id;

    /// <summary>Frame size in tiles.</summary>
    [DataField, AutoNetworkedField]
    public int Size;

    [DataField, AutoNetworkedField]
    public string Photographer = string.Empty;

    /// <summary>What the photographer could see in the frame when it was taken, for examining the print.</summary>
    [DataField, AutoNetworkedField]
    public List<string> Contents = new();

    /// <summary>The picture shrunk to <see cref="CMUPhotoLimits.ThumbnailPixels"/> square, as a PNG.</summary>
    [DataField, AutoNetworkedField]
    public byte[] Thumbnail = Array.Empty<byte>();

    /// <summary>When the print came out; old prints look handled.</summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan Printed;
}

/// <summary>The print's sprite layer that shows its thumbnail.</summary>
[Serializable, NetSerializable]
public enum CMUPhotoVisualLayers : byte
{
    Thumbnail,
}

[Serializable, NetSerializable]
public enum CMUPhotoUiKey : byte
{
    Key,
}

public static class CMUPhotoLimits
{
    /// <summary>Pixels per tile in a print. Captures are resampled to this.</summary>
    public const int TilePixels = 32;

    public const int MaxBytes = 160 * 1024;

    /// <summary>Side of the thumbnail drawn in the print sprite's window.</summary>
    public const int ThumbnailPixels = 12;

    /// <summary>How long the server waits for the photographer's client to send the picture.</summary>
    public static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Prints kept per round; the oldest pictures are forgotten past this.</summary>
    public const int PhotosPerRound = 400;

    /// <summary>How far from the photographer things can be and still be made out in a print.</summary>
    public const float DescribeRange = 16;

    /// <summary>People listed in a print's description, then grouped objects.</summary>
    public const int DescribedPeople = 8;
    public const int DescribedObjects = 8;
}

/// <summary>Server to photographer: capture this square of your view and send it back as a PNG.</summary>
[Serializable, NetSerializable]
public sealed class CMUPhotoCaptureRequestEvent(int request, NetCoordinates centre, int size) : EntityEventArgs
{
    public readonly int Request = request;
    public readonly NetCoordinates Centre = centre;
    public readonly int Size = size;
}

/// <summary>Photographer to server: the captured picture, or an empty array when the area was not in view.</summary>
[Serializable, NetSerializable]
public sealed class CMUPhotoCaptureResultEvent(int request, byte[] png) : EntityEventArgs
{
    public readonly int Request = request;
    public readonly byte[] Png = png;
}

[Serializable, NetSerializable]
public sealed class CMUPhotoImageRequestEvent(int photo) : EntityEventArgs
{
    public readonly int Photo = photo;
}

[Serializable, NetSerializable]
public sealed class CMUPhotoImageEvent(int photo, byte[] png) : EntityEventArgs
{
    public readonly int Photo = photo;
    public readonly byte[] Png = png;
}
