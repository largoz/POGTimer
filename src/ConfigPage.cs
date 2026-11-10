using System.Collections.Generic;
using System.Runtime.CompilerServices;
using POGMods.Config;

namespace POGMods.Timer
{
    /// <summary>
    /// The optional POG Config page. Everything that touches POG Config types is in here, behind a
    /// NoInlining call, so the mod loads and works without POG Config installed.
    /// </summary>
    internal static class ConfigPage
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Register(TimerSettings s)
        {
            POGConfig.Register("POG Timer", new List<ConfigEntry>
            {
                new HeaderEntry("Display"),
                new ToggleEntry("Show phase text (HURRY! / WARNING)", () => s.ShowPhaseText, v => s.ShowPhaseText = v),
                new ToggleEntry("Seconds format (e.g. 127s)", () => s.SecondsFormat, v => s.SecondsFormat = v),
                new SliderEntry("HURRY! threshold", () => s.HurryThreshold, v => s.HurryThreshold = v,
                    TimerSettings.HurryMin, TimerSettings.HurryMax, v => $"{(int)v}s", null,
                    TimerSettings.HurryDefault, true, true),
                new HeaderEntry("Multiplayer"),
                new ToggleEntry("Send the timer to other players (host only)", () => s.SendToClients, v => s.SendToClients = v),
            }, new ModInfo(TimerMod.VERSION,
                "Round countdown in the top-right corner. As host you send it to the other players; " +
                "in someone else's game you see the host's time (both need POG Timer).", "largo"));
        }
    }
}
