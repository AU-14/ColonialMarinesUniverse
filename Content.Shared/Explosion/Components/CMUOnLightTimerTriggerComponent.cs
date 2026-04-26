using System.Linq;
using Content.Shared.Guidebook;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Explosion.Components
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class CMUOnLightTimerTriggerComponent : Component
    {
        [DataField] public float Delay = 1f;

        /// <summary>
        ///     If not null, a user can use verbs to configure the delay to one of these options.
        /// </summary>
        [DataField] public List<float>? DelayOptions = null;

        /// <summary>
        ///     If not null, this timer will periodically play this sound while active.
        /// </summary>
        [DataField] public SoundSpecifier? BeepSound;

        /// <summary>
        ///     Time before beeping starts. Defaults to a single beep interval. If set to zero, will emit a beep immediately after use.
        /// </summary>
        [DataField] public float? InitialBeepDelay;

        /// <summary>
        ///     New stuffs for CMU and i dont give a hell about making new component...
        /// </summary>
        [DataField("min")] public float Randmin;

        [DataField("max")] public float Randmax;

        [DataField("instantfusechance")] public bool InstantFuseChance = false;

        [DataField("instfusechance")] public float Instfusechance;

        [DataField] public float BeepInterval = 1;

        /// <summary>
        ///     Allows changing the start-on-stick quality.
        /// </summary>
        [DataField("canToggleStartOnStick")] public bool AllowToggleStartOnStick;

        /// <summary>
        ///     Whether you can examine the item to see its timer or not.
        /// </summary>
        [DataField] public bool Examinable = true;

        /// <summary>
        ///     Whether or not to show the user a popup when starting the timer.
        /// </summary>
        [DataField] public bool DoPopup = true;

        #region GuidebookData

        [GuidebookData]
        public float? ShortestDelayOption => DelayOptions?.Min();

        [GuidebookData]
        public float? LongestDelayOption => DelayOptions?.Max();

        #endregion GuidebookData
    }
}
