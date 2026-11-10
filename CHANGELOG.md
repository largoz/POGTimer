## 1.1.0

- No more log spam. 1.0.x wrote "Hook failed" four times a second from the main menu on: it tried to listen for the host before any game existed to listen to. Now nothing happens in the menu, and a failure is written once.
- The host no longer listens for its own timer. It sends only during a round: when the round starts, when its phase changes, once a second, and once more when it ends. 1.0.x sent four times a second, in the lobby too.
- Players in the host's game count down between updates. They start listening again in every game they join; 1.0.x subscribed once per launch, so after the first game it could miss the host's time.
- If the host goes silent for 5 seconds during a round, the timer hides instead of showing a wrong time.
- Settings: phase text, seconds format, HURRY! threshold, and sending to other players. In POG Config (optional) or in `UserData/MelonPreferences.cfg` under `[POG_Timer]`.
- Works together with POG Timer 1.0.x in one game.
- Source on GitHub.

## 1.0.2

- Fixed the timer not showing after multiplayer sync was added (Il2CppInterop delegate compatibility).

## 1.0.1

- Multiplayer sync: the host sends the countdown to all players.

## 1.0.0

- First release.
