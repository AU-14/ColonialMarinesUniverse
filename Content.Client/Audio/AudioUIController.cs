using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Configuration;

namespace Content.Client.Audio;

public sealed partial class AudioUIController : UIController
{
    [Dependency] private IAudioManager _audioManager = default!;
    [Dependency] private IConfigurationManager _configManager = default!;
    [Dependency] private IResourceCache _cache = default!;

    private float _interfaceGain;
    private IAudioSource? _clickSource;
    private IAudioSource? _hoverSource;

    private const float ClickGain = 0.25f;
    private const float HoverGain = 0.05f;

    public override void Initialize()
    {
        base.Initialize();

        /*
         * This exists to load UI sounds outside of the game sim.
         */

        // No unsub coz never shuts down until program exit.
        _configManager.OnValueChanged(CCVars.InterfaceVolume, SetInterfaceVolume, true);
        _configManager.OnValueChanged(CCVars.UIClickSound, SetClickSound, true);
        _configManager.OnValueChanged(CCVars.UIHoverSound, SetHoverSound, true);
    }

    private void SetInterfaceVolume(float obj)
    {
        _interfaceGain = obj;

        if (_clickSource != null)
        {
            _clickSource.Gain = ClickGain * _interfaceGain;
        }

        if (_hoverSource != null)
        {
            _hoverSource.Gain = HoverGain * _interfaceGain;
        }
    }

    private void SetClickSound(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            var resource = GetSoundOrFallback(value, CCVars.UIClickSound.DefaultValue);
            var source = resource != null
                ? TryCreateUiSource(resource, ClickGain)
                : null;

            _clickSource = source;
            UIManager.SetClickSound(source);
        }
        else
        {
            _clickSource = null;
            UIManager.SetClickSound(null);
        }
    }

    private void SetHoverSound(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            var hoverResource = GetSoundOrFallback(value, CCVars.UIHoverSound.DefaultValue);
            var hoverSource = hoverResource != null
                ? TryCreateUiSource(hoverResource, HoverGain)
                : null;

            _hoverSource = hoverSource;
            UIManager.SetHoverSound(hoverSource);
        }
        else
        {
            _hoverSource = null;
            UIManager.SetHoverSound(null);
        }
    }

    private AudioResource? GetSoundOrFallback(string path, string fallback)
    {
        if (TryGetSound(path, out var resource))
            return resource;

        if (string.IsNullOrEmpty(fallback) ||
            path.Equals(fallback, StringComparison.Ordinal))
        {
            return null;
        }

        return TryGetSound(fallback, out resource)
            ? resource
            : null;
    }

    private bool TryGetSound(string path, out AudioResource? resource)
    {
        try
        {
            return _cache.TryGetResource(path, out resource);
        }
        catch (Exception e)
        {
            Logger.GetSawmill("ui.audio").Warning($"Failed to load UI sound '{path}': {e}");
            resource = null;
            return false;
        }
    }

    private IAudioSource? TryCreateUiSource(AudioResource resource, float gain)
    {
        try
        {
            var source = _audioManager.CreateAudioSource(resource);
            if (source == null)
                return null;

            source.Gain = gain * _interfaceGain;
            source.Global = true;
            return source;
        }
        catch (Exception e)
        {
            Logger.GetSawmill("ui.audio").Warning($"Failed to create UI audio source: {e}");
            return null;
        }
    }
}
