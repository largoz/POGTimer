using System;
using System.Collections.Generic;
using Il2CppGame.Gameplay;
using Il2CppInterop.Runtime.Injection;
using MelonLoader;
using POGMods.Config;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[assembly: MelonInfo(typeof(POGMods.Timer.PogTimerMelon), "POG Timer", "1.0.3", "largo", "")]

namespace POGMods.Timer
{
    public class PogTimerMelon : MelonMod
    {
        public override void OnInitializeMelon()
        {
            ClassInjector.RegisterTypeInIl2Cpp<TimerBehaviour>();
            var go = new GameObject("POGTimerRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<TimerBehaviour>();

            try
            {
                POGConfig.Register("POG Timer", new List<ConfigEntry>
                {
                    new HeaderEntry("Display"),
                    new ToggleEntry("Show phase text (HURRY! / WARNING)",
                        () => TimerBehaviour.ShowPhaseText,
                        v => TimerBehaviour.ShowPhaseText = v,
                        "show_phase_text"),
                    new ToggleEntry("Seconds format (e.g. 127s)",
                        () => TimerBehaviour.SecondsFormat,
                        v => TimerBehaviour.SecondsFormat = v,
                        "seconds_format"),
                    new HeaderEntry("Behaviour"),
                    new SliderEntry("HURRY! threshold (sec)",
                        () => TimerBehaviour.HurryThreshold,
                        v => TimerBehaviour.HurryThreshold = v,
                        10f, 120f,
                        v => $"{(int)v}s",
                        "hurry_threshold",
                        60f, true, true),
                    new HeaderEntry("Network"),
                    new ToggleEntry("Broadcast timer to clients (host only)",
                        () => TimerBehaviour.BroadcastEnabled,
                        v => TimerBehaviour.BroadcastEnabled = v,
                        "broadcast_enabled"),
                }, new ModInfo("1.0.3", "Round timer overlay with network sync."));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGTimer] POGConfig registration skipped: {ex.Message}");
            }

            MelonLogger.Msg("POG Timer 1.0.3 (MelonLoader) loaded.");
        }
    }

    public class TimerBehaviour : MonoBehaviour
    {
        private const float PollInterval = 0.25f;

        /// <summary>ASCII "POGT" — filter for our blob on the shared unnamed-message channel.</summary>
        private const uint PayloadMagic = 0x54474F50u;

        private static CustomMessagingManager s_hookedOn;
        private static Action<ulong, FastBufferReader> s_unnamedDelegate;

        public static bool ShowPhaseText = true;
        public static bool SecondsFormat = false;
        public static float HurryThreshold = 60f;
        public static bool BroadcastEnabled = true;

        private static float s_netRemaining;
        private static bool s_netPlayedWarning;
        private static bool s_netRoundActive;
        private static bool s_hasNetworkSnapshot;

        private float _pollAccum;
        private GUIStyle _style;
        private GUIStyle _shadowStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _smallShadowStyle;
        private string _timerText = "";
        private string _phaseText = "";
        private bool _loggedHostOnce;

        public TimerBehaviour(IntPtr ptr) : base(ptr) { }

        void Update()
        {
            _pollAccum += Time.deltaTime;
            if (_pollAccum < PollInterval) return;
            _pollAccum = 0f;

            // Hook registration is independent — its failure must not kill the display
            try { EnsureUnnamedHook(); }
            catch { }

            try
            {
                var nm = NetworkManager.Singleton;

                if (nm != null && nm.IsServer)
                    TickAuthority(nm);

                else if (nm != null && nm.IsClient && !nm.IsServer)
                    TickRemoteClient();

                else
                    TickOfflineOrNoNetwork();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[POGTimer] Update error: {ex.Message}\n{ex.StackTrace}");
                ClearDisplay();
            }
        }

        private static void EnsureUnnamedHook()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null) return;

            var current = nm.CustomMessagingManager;
            // Re-hook when CustomMessagingManager instance changes (lobby switch / reconnect).
            if (s_hookedOn == current) return;

            if (s_unnamedDelegate == null)
                s_unnamedDelegate = OnUnnamedModMessage;

            if (s_hookedOn != null)
            {
                try { s_hookedOn.OnUnnamedMessage -= s_unnamedDelegate; }
                catch { }
            }

            current.OnUnnamedMessage += s_unnamedDelegate;
            s_hookedOn = current;
            s_hasNetworkSnapshot = false;
            MelonLogger.Msg("[POGTimer] Subscribed to Netcode unnamed messages.");
        }

        private static void OnUnnamedModMessage(ulong senderClientId, FastBufferReader reader)
        {
            try
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                    return;

                if (senderClientId != NetworkManager.ServerClientId)
                    return;

                int startPos = reader.Position;

                // Magic + float + flags = 9 bytes minimum.
                if (reader.Length - startPos < 4) return;

                uint magic = ReadUInt32LE(reader);
                if (magic != PayloadMagic)
                {
                    // Not ours — restore position so any other handler sees a clean reader.
                    reader.Seek(startPos);
                    return;
                }

                if (reader.Length - reader.Position < 5)
                {
                    reader.Seek(startPos);
                    return;
                }

                uint bits = ReadUInt32LE(reader);
                float remaining = BitConverter.UInt32BitsToSingle(bits);

                reader.ReadByteSafe(out byte flags);

                s_netRoundActive = (flags & 1) != 0;
                s_netPlayedWarning = (flags & 2) != 0;
                s_netRemaining = remaining;
                s_hasNetworkSnapshot = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGTimer] OnUnnamedMessage parse error: {ex.Message}");
            }
        }

        private static uint ReadUInt32LE(FastBufferReader reader)
        {
            reader.ReadByteSafe(out byte b0);
            reader.ReadByteSafe(out byte b1);
            reader.ReadByteSafe(out byte b2);
            reader.ReadByteSafe(out byte b3);
            return (uint)b0 | ((uint)b1 << 8) | ((uint)b2 << 16) | ((uint)b3 << 24);
        }

        private static void WriteHeader(FastBufferWriter writer, uint value)
        {
            writer.WriteByteSafe((byte)(value & 0xFF));
            writer.WriteByteSafe((byte)((value >> 8) & 0xFF));
            writer.WriteByteSafe((byte)((value >> 16) & 0xFF));
            writer.WriteByteSafe((byte)((value >> 24) & 0xFF));
        }

        private static void WriteFloatBits(FastBufferWriter writer, float value)
        {
            uint bits = BitConverter.SingleToUInt32Bits(value);
            WriteHeader(writer, bits);
        }

        private void TickAuthority(NetworkManager nm)
        {
            s_hasNetworkSnapshot = false;

            var round = GameMode_Round.Instance;
            if (round == null || !round.m_gameStarted)
            {
                ClearDisplay();
                TryBroadcastUnnamed(nm, false, 0f, false);
                return;
            }

            float elapsed = round.ElapsedGameTime;
            float total = round.RoundTime;
            float remaining = total - elapsed;

            if (!_loggedHostOnce)
            {
                MelonLogger.Msg($"[POGTimer] Host round sync: total={total}s, elapsed={elapsed:F1}s");
                _loggedHostOnce = true;
            }

            ApplyDisplayFromRound(remaining, round.m_playedWarning);
            TryBroadcastUnnamed(nm, true, Mathf.Max(0f, remaining), round.m_playedWarning);
        }

        private bool _wasbroadcastEnabled = true;

        private void TryBroadcastUnnamed(NetworkManager nm, bool active, float remaining, bool playedWarning)
        {
            try
            {
                bool enabled = BroadcastEnabled;

                // Broadcast just turned off — send one "inactive" packet so clients clear their display.
                if (_wasbroadcastEnabled && !enabled)
                    BroadcastUnnamed(nm, false, 0f, false);

                _wasbroadcastEnabled = enabled;

                if (!enabled) return;

                BroadcastUnnamed(nm, active, remaining, playedWarning);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[POGTimer] Broadcast skipped: {ex.Message}");
            }
        }

        private static void BroadcastUnnamed(NetworkManager nm, bool active, float remaining, bool playedWarning)
        {
            byte flags = 0;
            if (active) flags |= 1;
            if (playedWarning) flags |= 2;

            var w = new FastBufferWriter(32, Allocator.Temp);
            try
            {
                WriteHeader(w, PayloadMagic);
                WriteFloatBits(w, remaining);
                w.WriteByteSafe(flags);
                nm.CustomMessagingManager.SendUnnamedMessageToAll(w, NetworkDelivery.Reliable);
            }
            finally
            {
                w.Dispose();
            }
        }

        private void TickRemoteClient()
        {
            if (!s_hasNetworkSnapshot || !s_netRoundActive)
            {
                ClearDisplay();
                return;
            }

            ApplyDisplayFromNetwork(s_netRemaining, s_netPlayedWarning);
        }

        private void TickOfflineOrNoNetwork()
        {
            s_hasNetworkSnapshot = false;

            var round = GameMode_Round.Instance;
            if (round == null || !round.m_gameStarted)
            {
                ClearDisplay();
                return;
            }

            float elapsed = round.ElapsedGameTime;
            float total = round.RoundTime;
            float remaining = total - elapsed;

            ApplyDisplayFromRound(remaining, round.m_playedWarning);
        }

        private void ApplyDisplay(float remaining, bool playedWarning)
        {
            if (remaining <= 0f)
            {
                _timerText = SecondsFormat ? "0s" : "00:00";
                _phaseText = ShowPhaseText ? "TIME'S UP" : "";
                return;
            }

            if (SecondsFormat)
                _timerText = $"{(int)remaining}s";
            else
            {
                int min = (int)(remaining / 60f);
                int sec = (int)(remaining % 60f);
                _timerText = $"{min:D2}:{sec:D2}";
            }

            if (!ShowPhaseText)
                _phaseText = "";
            else if (remaining < HurryThreshold)
                _phaseText = "HURRY!";
            else if (playedWarning)
                _phaseText = "WARNING";
            else
                _phaseText = "";
        }

        private void ApplyDisplayFromRound(float remaining, bool playedWarning)
            => ApplyDisplay(remaining, playedWarning);

        private void ApplyDisplayFromNetwork(float remaining, bool playedWarning)
            => ApplyDisplay(remaining, playedWarning);

        private void ClearDisplay()
        {
            _timerText = "";
            _phaseText = "";
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(_timerText)) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label);
                _style.fontSize = 30;
                _style.fontStyle = FontStyle.Bold;
                _style.alignment = TextAnchor.UpperRight;
                _style.normal.textColor = Color.white;

                _shadowStyle = new GUIStyle(_style);
                _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);

                _smallStyle = new GUIStyle(GUI.skin.label);
                _smallStyle.fontSize = 16;
                _smallStyle.fontStyle = FontStyle.Bold;
                _smallStyle.alignment = TextAnchor.UpperRight;
                _smallStyle.normal.textColor = new Color(1f, 0.8f, 0.3f);

                _smallShadowStyle = new GUIStyle(_smallStyle);
                _smallShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            }

            bool urgent = _phaseText == "HURRY!" || _phaseText == "TIME'S UP";
            _style.normal.textColor = urgent ? Color.red : Color.white;

            float w = 200f;
            float x = Screen.width - w - 20f;
            float y = 15f;

            GUI.Label(new Rect(x + 2f, y + 2f, w, 40f), _timerText, _shadowStyle);
            GUI.Label(new Rect(x, y, w, 40f), _timerText, _style);

            if (!string.IsNullOrEmpty(_phaseText))
            {
                GUI.Label(new Rect(x + 2f, y + 36f, w, 25f), _phaseText, _smallShadowStyle);
                GUI.Label(new Rect(x, y + 34f, w, 25f), _phaseText, _smallStyle);
            }
        }
    }
}
