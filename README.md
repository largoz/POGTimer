# POGTimer

Оверлей раунд-таймера для **Pit of Goblin** (MelonLoader, IL2CPP + Unity Netcode).

Публікується на Thunderstore як **POG Timer**. Player-facing опис і changelog —
у [`../../ThunderstorePackages/POGTimer/`](../../ThunderstorePackages/POGTimer/).

**Версія:** 1.0.3
**Namespace:** `POGMods.Timer`
**Збірка:** `POGTimer.dll`
**Залежності:** MelonLoader, Unity Netcode Runtime; опційно [POGConfig](../POGConfig/) (soft dependency)

---

## Що робить

Завжди показує залишок часу раунду в правому верхньому куті (`MM:SS` або
`127s`, конфігурується), з фазовими підписами **HURRY!** (нижче порогу,
типово 60с) і **WARNING** (після `m_playedWarning` гри).

У ко-опі **хост тримає таймер синхронним для всіх**: клієнти не читають
`GameMode_Round` самі, а отримують значення від хоста мережею.

## Як це влаштовано

### Джерело правди про час

`GameMode_Round.Instance` (`Assembly-CSharp`, `NetworkBehaviour`-синглтон):
- `m_gameStarted` — чи йде раунд.
- `ElapsedGameTime` / `RoundTime` → `remaining = RoundTime - ElapsedGameTime`.
- `m_playedWarning` — чи вже програна репліка "король/королева втрачає терпіння".

### Три режими роботи (`TickAuthority` / `TickRemoteClient` / `TickOfflineOrNoNetwork`)

`Update()` кожні 0.25с (`PollInterval`) визначає роль через `NetworkManager.Singleton`:

- **`IsServer`** (хост, або single-player — теж хост локально) — читає
  `GameMode_Round` напряму й **розсилає** стан клієнтам.
- **`IsClient && !IsServer`** (звичайний клієнт) — **не** чіпає
  `GameMode_Round`, лише показує останній отриманий мережевий знімок.
- **офлайн / нема `NetworkManager`** — читає `GameMode_Round` локально, без
  розсилки (той самий шлях, що й на хості, без мережевої частини).

### Кастомний мережевий канал

Розсилка йде через **unnamed messages** `CustomMessagingManager`
(`SendUnnamedMessageToAll` / `OnUnnamedMessage`) — спільний "безіменний"
канал Netcode, тому мод сам фільтрує свої пакети 4-байтовим magic-префіксом
`"POGT"` (`PayloadMagic = 0x54474F50`). Формат payload: `[magic:4][floatBits:4][flags:1]`
(`flags` біт 0 = round active, біт 1 = played warning). Ручна little-endian
серіалізація (`ReadUInt32LE`/`WriteHeader`) — без готового `BitConverter`
для порядку байтів, бо `FastBufferReader/Writer` працює побайтово.

`EnsureUnnamedHook()` перепідписується, якщо `CustomMessagingManager`
змінився (вихід у лобі / реконект) — інакше підписка лишилась би на
знищений інстанс.

### Broadcast toggle

`BroadcastEnabled` (тоггл у POGConfig, host-only) — при вимкненні хост
одноразово шле "неактивний" пакет (`TryBroadcastUnnamed`), щоб клієнти
одразу очистили дисплей, а не чекали timeout.

## Збірка

```powershell
dotnet build ModSource/POGTimer/POGTimer.csproj -c Release
```

## Roadmap

Див. [ROADMAP.md](ROADMAP.md).
