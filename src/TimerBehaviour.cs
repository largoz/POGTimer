using System;
using System.Collections.Generic;
using Il2CppGame.Gameplay;
using MelonLoader;
using Unity.Netcode;
using UnityEngine;

namespace POGMods.Timer
{
    /// <summary>
    /// Decides what the countdown shows a few times a second and draws it. Alone or as host it reads
    /// the round; in someone else's game it shows what the host sent (<see cref="TimerNet"/>).
    /// </summary>
    public class TimerBehaviour : MonoBehaviour
    {
        private const float PollInterval = 0.25f;

        private const string TextHurry = "HURRY!", TextWarning = "WARNING", TextTimesUp = "TIME'S UP";

        private TimerNet _net;
        private NetRole _role = NetRole.None;
        private bool _roundLogged;
        private readonly HashSet<string> _reportedErrors = new HashSet<string>();

        private float _pollAccum;
        private string _timerText = "";
        private string _phaseText = "";
        private GUIStyle _style, _shadowStyle, _smallStyle, _smallShadowStyle;

        public TimerBehaviour(IntPtr ptr) : base(ptr) { }

        void Update()
        {
            _pollAccum += Time.unscaledDeltaTime;
            if (_pollAccum < PollInterval) return;
            _pollAccum = 0f;

            try { Tick(); }
            catch (Exception ex)
            {
                // The same error repeats every tick if it repeats at all: report it once, then only in debug.
                if (_reportedErrors.Add(ex.Message)) TimerMod.Log.Error($"{ex.Message}\n{ex.StackTrace}");
                else MelonDebug.Msg($"[POGTimer] again: {ex.Message}");
                Show(TimerState.None);
            }
        }

        private void Tick()
        {
            _net ??= new TimerNet(TimerMod.Log);
            var settings = TimerMod.Settings;
            var nm = NetworkManager.Singleton;
            var role = TimerNet.RoleOf(nm);
            if (role != _role) { _net.RoleChanged(_role, role); _role = role; }

            switch (role)
            {
                case NetRole.Host:
                    var hosted = ReadRound();
                    _net.HostTick(nm, hosted, settings.SendToClients);
                    Show(hosted);
                    break;
                case NetRole.Client:
                    _net.ClientTick(nm);
                    Show(_net.ClientState());
                    break;
                default:
                    Show(ReadRound());
                    break;
            }
        }

        /// <summary>The round as this game sees it; only meaningful alone or as host.</summary>
        private TimerState ReadRound()
        {
            var round = GameMode_Round.Instance;
            if (round == null || !round.m_gameStarted) { _roundLogged = false; return TimerState.None; }

            float total = round.RoundTime;
            if (!_roundLogged)
            {
                TimerMod.Log.Msg($"Round started: {total:0} s.");
                _roundLogged = true;
            }
            return new TimerState
            {
                Active = true,
                Remaining = Mathf.Max(0f, total - round.ElapsedGameTime),
                Warning = round.m_playedWarning,
            };
        }

        private void Show(TimerState s)
        {
            if (!s.Active) { _timerText = ""; _phaseText = ""; return; }

            var settings = TimerMod.Settings;
            float remaining = s.Remaining;
            if (remaining <= 0f)
            {
                _timerText = settings.SecondsFormat ? "0s" : "00:00";
                _phaseText = settings.ShowPhaseText ? TextTimesUp : "";
                return;
            }

            _timerText = settings.SecondsFormat
                ? $"{(int)remaining}s"
                : $"{(int)(remaining / 60f):D2}:{(int)(remaining % 60f):D2}";

            if (!settings.ShowPhaseText) _phaseText = "";
            else if (remaining < settings.HurryThreshold) _phaseText = TextHurry;
            else if (s.Warning) _phaseText = TextWarning;
            else _phaseText = "";
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(_timerText)) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 30,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperRight,
                };
                _style.normal.textColor = Color.white;

                _shadowStyle = new GUIStyle(_style);
                _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);

                _smallStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperRight,
                };
                _smallStyle.normal.textColor = new Color(1f, 0.8f, 0.3f);

                _smallShadowStyle = new GUIStyle(_smallStyle);
                _smallShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            }

            bool urgent = _phaseText == TextHurry || _phaseText == TextTimesUp;
            _style.normal.textColor = urgent ? Color.red : Color.white;

            const float w = 200f;
            float x = Screen.width - w - 20f;
            const float y = 15f;

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
