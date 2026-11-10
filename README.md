# POG Timer

A round countdown for Pit of Goblin in the top-right corner of the screen: `MM:SS` (or seconds), with HURRY!, WARNING and TIME'S UP under it. In co-op the host sends the countdown to the other players, so everyone with the mod sees the same time.

## Multiplayer

- The host's game knows the round time. While a round runs it sends the time to the other players once a second, and once more when the round ends. The players count down on their own between updates.
- Every player who wants to see the timer needs POG Timer. Players without it are not affected.
- POG Timer 1.0.x and 1.1.x understand each other in one game.
- The host can stop sending: "Send the timer to other players".
- In the main menu the mod does no network work and writes nothing to the log.

## Settings

With [POG Config](https://thunderstore.io/c/pit-of-goblin/p/Largo/POG_Config/) installed: MODS, then POG Timer.

| Setting | Default | |
|---|---|---|
| Show phase text (HURRY! / WARNING) | on | the line under the timer |
| Seconds format | off | `127s` instead of `02:07` |
| HURRY! threshold | 60 s | 10 to 120 seconds left |
| Send the timer to other players (host only) | on | |

POG Config is optional. Without it, edit `UserData/MelonPreferences.cfg`, section `[POG_Timer]`: `ShowPhaseText`, `SecondsFormat`, `HurryThreshold`, `SendToClients`.

## Install

With a Thunderstore mod manager (Gale, r2modman), or put `POGTimer.dll` into the game's `Mods` folder. Needs MelonLoader.

## For mod developers

The host sends a Netcode unnamed message, little-endian: `[u32 magic "POGT"][f32 seconds left][u8 flags]`, flags bit 0 = round running, bit 1 = the game's warning has played. The layout has not changed since 1.0.1 and will not; anything new would get a new magic.

Build against the game, or a mod manager profile, that holds `MelonLoader/` and `Mods/`:

```
dotnet build POGTimer.csproj -c Release -p:GameDir=<that folder>
```

Source: [github.com/largoz/POGTimer](https://github.com/largoz/POGTimer)
