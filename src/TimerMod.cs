using System;
using System.IO;
using Il2CppInterop.Runtime.Injection;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(POGMods.Timer.TimerMod), "POG Timer", POGMods.Timer.TimerMod.VERSION, "largo", "https://github.com/largoz/POGTimer")]
[assembly: MelonOptionalDependencies("POGConfig")]

namespace POGMods.Timer
{
    /// <summary>
    /// Round countdown in the top-right corner. Alone or as host the mod reads the round itself;
    /// in someone else's game it shows the time the host sends (see <see cref="TimerNet"/>).
    /// </summary>
    public class TimerMod : MelonMod
    {
        public const string VERSION = "1.1.0";

        internal static MelonLogger.Instance Log { get; private set; }
        internal static TimerSettings Settings { get; private set; }

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Settings = new TimerSettings();

            // POG Config records each row's value at registration as the one RESTORE DEFAULTS goes back to.
            Settings.CapturingDefaults = true;
            try { ConfigPage.Register(Settings); }
            catch (Exception ex) when (ex is FileNotFoundException || ex is TypeLoadException || ex is MissingMethodException)
            {
                LoggerInstance.Msg($"POG Config not installed: settings are in UserData/MelonPreferences.cfg [{TimerSettings.Category}]");
            }
            finally { Settings.CapturingDefaults = false; }

            ClassInjector.RegisterTypeInIl2Cpp<TimerBehaviour>();
            var go = new GameObject("POGTimerRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<TimerBehaviour>();

            LoggerInstance.Msg($"POG Timer {VERSION} loaded.");
        }

        public override void OnUpdate() => Settings?.SaveWhenSettled();
    }
}
