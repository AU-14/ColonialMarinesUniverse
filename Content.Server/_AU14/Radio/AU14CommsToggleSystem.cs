using Content.Shared._AU14.CCVar;
using Content.Shared.Radio;
using Robust.Shared.Configuration;

namespace Content.Server._AU14.Radio;

/// <summary>
///     One place to ask whether the comms overhaul applies. There is the master switch and
///     a separate one for the CLF/INSFOR nets, so an admin can hand the cell back stock
///     radio mid-round without taking the system off GOVFOR and OPFOR as well.
/// </summary>
public sealed partial class AU14CommsToggleSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;

    public const string ClfFaction = "clf";

    private bool _enabled;
    private bool _clfEnabled;

    public override void Initialize()
    {
        Subs.CVar(_config, AU14CCVars.NewCommsSystem, v => _enabled = v, true);
        Subs.CVar(_config, AU14CCVars.NewCommsSystemClf, v => _clfEnabled = v, true);
    }

    /// <summary>
    ///     The master switch on its own, for behaviour that belongs to no particular net.
    /// </summary>
    public bool Enabled => _enabled;

    public bool ClfEnabled => _clfEnabled;

    /// <summary>
    ///     Whether the overhaul governs traffic on this channel. A null channel is treated
    ///     as faction-less - direct frequencies and sentinel channels answer to the master
    ///     switch alone.
    /// </summary>
    public bool EnabledOn(RadioChannelPrototype? channel)
    {
        if (!_enabled)
            return false;

        return _clfEnabled || !IsClf(channel);
    }

    public static bool IsClf(RadioChannelPrototype? channel)
    {
        return channel != null &&
               string.Equals(channel.Faction, ClfFaction, StringComparison.OrdinalIgnoreCase);
    }
}
