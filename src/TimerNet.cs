using System;
using MelonLoader;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace POGMods.Timer
{
    internal enum NetRole { None, Host, Client }

    /// <summary>
    /// The multiplayer side. The host sends; a client listens; nobody asks.
    ///   - Menu or no session: nothing happens here. Netcode has no message channel then
    ///     (CustomMessagingManager is null until a session starts; measured in the main menu).
    ///   - Host: never listens. Sends while a round runs: when it starts, when the phase changes,
    ///     and once per <see cref="SendInterval"/>; then one "stopped" packet when it ends.
    ///   - Client: listens from joining someone's game until leaving it, and again in the next one.
    ///     Counts down itself between packets.
    /// Failures are logged once per session, never per tick.
    /// </summary>
    internal sealed class TimerNet
    {
        private const float SendInterval = 1f;

        // A client hides the timer when the host has said nothing for this long during a round
        // (host paused, mod removed, connection stalling): better no timer than a wrong one.
        private const float StaleAfter = 5f;

        // Sequenced: a late "running" packet must never arrive after "stopped".
        private const NetworkDelivery Delivery = NetworkDelivery.ReliableSequenced;

        private readonly MelonLogger.Instance _log;

        // Client. The handler is converted to an IL2CPP delegate once and kept: removing it needs that
        // same object (a fresh conversion of the same method is not removed; measured).
        private CustomMessagingManager.UnnamedMessageDelegate _handler;
        private CustomMessagingManager _listeningOn;
        private IntPtr _failedOn;
        private TimerState _received;
        private float _receivedAt;
        private bool _hasReceived;
        private bool _parseWarned;

        // Host.
        private TimerState _lastSent;
        private bool _sentAny;
        private float _nextSend;
        private bool _sendWarned;

        public TimerNet(MelonLogger.Instance log) { _log = log; }

        public static NetRole RoleOf(NetworkManager nm)
        {
            if (nm == null || !nm.IsListening) return NetRole.None;
            if (nm.IsServer) return NetRole.Host;
            return nm.IsClient ? NetRole.Client : NetRole.None;
        }

        /// <summary>Called when the role changes: drop everything that belonged to the old session.</summary>
        public void RoleChanged(NetRole from, NetRole to)
        {
            MelonDebug.Msg($"[POGTimer] network role {from} -> {to}");
            StopListening();
            _failedOn = IntPtr.Zero;
            _hasReceived = false;
            _sentAny = false;
            _sendWarned = false;
        }

        // ---------------------------------------------------------------- host

        public void HostTick(NetworkManager nm, TimerState now, bool sendEnabled)
        {
            if (!sendEnabled)
            {
                // Sending was switched off mid-round: tell clients once so they clear their display.
                if (_sentAny && _lastSent.Active) Send(nm, TimerState.None);
                return;
            }
            bool changed = !_sentAny || !now.SameShape(_lastSent);
            bool due = now.Active && Time.unscaledTime >= _nextSend;
            if (changed || due) Send(nm, now);
        }

        private void Send(NetworkManager nm, TimerState s)
        {
            // Recorded as sent even if sending fails: a failure is retried at the next interval, not every tick.
            _lastSent = s;
            _sentAny = true;
            _nextSend = Time.unscaledTime + SendInterval;

            var cmm = nm.CustomMessagingManager;
            if (cmm == null) return;
            var w = new FastBufferWriter(TimerPacket.Size, Allocator.Temp);
            try
            {
                TimerPacket.Write(w, s);
                cmm.SendUnnamedMessageToAll(w, Delivery);
            }
            catch (Exception ex)
            {
                if (!_sendWarned) _log.Warning($"Could not send the timer to other players: {ex.Message}");
                else MelonDebug.Msg($"[POGTimer] send failed again: {ex.Message}");
                _sendWarned = true;
            }
            finally { w.Dispose(); }
        }

        // -------------------------------------------------------------- client

        public void ClientTick(NetworkManager nm)
        {
            var cmm = nm.CustomMessagingManager;
            if (cmm == null) { StopListening(); return; }
            Listen(cmm);
        }

        /// <summary>Listens on this channel unless already listening on it (or it failed this session).</summary>
        internal void Listen(CustomMessagingManager cmm)
        {
            // Compare the native objects: each property read returns a new managed wrapper.
            if (_listeningOn != null && _listeningOn.Pointer == cmm.Pointer) return;
            if (_failedOn == cmm.Pointer) return;

            StopListening();
            try
            {
                if (_handler == null)
                {
                    Action<ulong, FastBufferReader> handler = OnMessage;
                    _handler = handler;
                }
                cmm.add_OnUnnamedMessage(_handler);
                _listeningOn = cmm;
                _hasReceived = false;
                _log.Msg("In someone else's game: showing the host's timer.");
            }
            catch (Exception ex)
            {
                _failedOn = cmm.Pointer;
                _log.Warning($"Cannot receive the host's timer in this game: {ex.Message}");
            }
        }

        /// <summary>What a client shows: the last packet, counted down locally since it arrived.</summary>
        public TimerState ClientState()
        {
            if (!_hasReceived || !_received.Active) return TimerState.None;
            float since = Time.unscaledTime - _receivedAt;
            if (since > StaleAfter) return TimerState.None;
            var s = _received;
            s.Remaining = Mathf.Max(0f, s.Remaining - since);
            return s;
        }

        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            try
            {
                if (sender != NetworkManager.ServerClientId) return;
                if (!TimerPacket.TryRead(reader, out var s)) return;
                _received = s;
                _receivedAt = Time.unscaledTime;
                _hasReceived = true;
            }
            catch (Exception ex)
            {
                if (!_parseWarned) _log.Warning($"Unreadable timer packet from the host: {ex.Message}");
                _parseWarned = true;
            }
        }

        internal void StopListening()
        {
            if (_listeningOn == null) return;
            try { _listeningOn.remove_OnUnnamedMessage(_handler); }
            catch (Exception ex) { MelonDebug.Msg($"[POGTimer] unsubscribe failed (session already gone): {ex.Message}"); }
            _listeningOn = null;
            _hasReceived = false;
            MelonDebug.Msg("[POGTimer] stopped listening for the host's timer");
        }
    }
}
