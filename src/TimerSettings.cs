using MelonLoader;
using UnityEngine;

namespace POGMods.Timer
{
    /// <summary>
    /// The player's settings, stored in MelonPreferences [POG_Timer]: the single source of truth,
    /// with or without POG Config. The POG Config page only reads and writes these.
    /// </summary>
    internal sealed class TimerSettings
    {
        public const string Category = "POG_Timer";

        public const float HurryMin = 10f, HurryMax = 120f, HurryDefault = 60f;

        // A dragged slider changes the value many times a second; write the file once it settles.
        private const float SaveDelay = 1f;

        private readonly MelonPreferences_Category _category;
        private readonly MelonPreferences_Entry<bool> _showPhaseText, _secondsFormat, _broadcast;
        private readonly MelonPreferences_Entry<float> _hurryThreshold;
        private bool _dirty;
        private float _saveAt;

        /// <summary>While true every getter returns the default (see TimerMod.OnInitializeMelon).</summary>
        public bool CapturingDefaults;

        public TimerSettings()
        {
            _category = MelonPreferences.CreateCategory(Category, "POG Timer");
            _showPhaseText = _category.CreateEntry("ShowPhaseText", true, "Show phase text",
                "HURRY! / WARNING / TIME'S UP under the timer");
            _secondsFormat = _category.CreateEntry("SecondsFormat", false, "Seconds format",
                "127s instead of 02:07");
            _hurryThreshold = _category.CreateEntry("HurryThreshold", HurryDefault, "HURRY! threshold",
                $"Seconds left when HURRY! appears ({HurryMin}-{HurryMax})");
            _broadcast = _category.CreateEntry("SendToClients", true, "Send the timer to other players",
                "Host only: players in your game who have POG Timer see your countdown");
        }

        public bool ShowPhaseText { get => Get(_showPhaseText); set => Set(_showPhaseText, value); }
        public bool SecondsFormat { get => Get(_secondsFormat); set => Set(_secondsFormat, value); }
        public bool SendToClients { get => Get(_broadcast); set => Set(_broadcast, value); }

        public float HurryThreshold
        {
            get => Mathf.Clamp(Get(_hurryThreshold), HurryMin, HurryMax);
            set => Set(_hurryThreshold, Mathf.Clamp(value, HurryMin, HurryMax));
        }

        private T Get<T>(MelonPreferences_Entry<T> e) => CapturingDefaults ? e.DefaultValue : e.Value;

        private void Set<T>(MelonPreferences_Entry<T> e, T value)
        {
            if (Equals(e.Value, value)) return;
            e.Value = value;
            _dirty = true;
            _saveAt = Time.unscaledTime + SaveDelay;
        }

        public void SaveWhenSettled()
        {
            if (!_dirty || Time.unscaledTime < _saveAt) return;
            _dirty = false;
            _category.SaveToFile(false);
        }
    }
}
