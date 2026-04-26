# Unity Multiplayer (Mirror) — Раунды, Здоровье, Урон, Респавн + Перемотка времени

Сетевой мультиплеер на базе **Mirror Networking** с полноценным циклом матча: разогрев → раунды → конец матча.
Поддерживается фирменная фича — **перемотка времени игрока (rewind)** на N секунд назад с откатом нанесённого урона.

---

## Архитектура

### Скрипты (`Assets/Scripts/`)

| Файл | Назначение |
|------|------------|
| **GameManager.cs** | Серверный менеджер матча: фазы (Warmup/Round/RoundEnd/MatchEnd), таймер, счёт, спавн-точки, респавн |
| **MatchNetworkManager.cs** | Кастомный `NetworkManager`: спавнит `GameManager` и игроков в случайных точках |
| **NetworkSpawnPoint.cs** | Маркер точки спавна (пустой GameObject + этот компонент) |
| **MatchHUD.cs** | UI матча: таймер, раунд, статус, баннер, таблица счёта (Tab) |
| **ScoreboardEntry.cs** | Одна строка таблицы счёта |
| **PlayerHealth.cs** | Здоровье + смерть (делегирует подсчёт и респавн в `GameManager`) |
| **PlayerShooting.cs** | Стрельба (raycast на сервере), перезарядка, инвентарь патронов |
| **PlayerRewind.cs** | **🌀 Фича перемотки времени:** снапшоты позиции/здоровья/патронов + откат урона |
| **PlayerRewindInput.cs** | Локальный ввод для активации перемотки (по умолчанию `Z`) |
| **FPSInput.cs** | Контроллер передвижения от первого лица (через Input System) |
| **MouseLookX.cs / MouseLookY.cs** | Обзор мышью |
| **PlayerHUD.cs / PlayerUIController.cs** | HUD игрока: здоровье, оружие, патроны |
| **Recoil.cs** | Отдача оружия |
| **WeaponData.cs** | ScriptableObject описания оружия |
| **MainMenuController.cs** | Главное меню (Host / Join + выбор сцены) |

### Префабы (`Assets/PreFab/Game/`)
- **GameManager.prefab** — `NetworkIdentity` + `GameManager`. Указывается в поле `gameManagerPrefab` у `MatchNetworkManager`.
- **SpawnPoint.prefab** — пустой GameObject + `NetworkSpawnPoint`. Расставьте по карте.

---

## Цикл матча (FSM `GameManager.MatchState`)

```
Warmup ──► RoundActive ──► RoundEnd ──┐
   ▲                                  │
   └────────── (если раунды кончились) ──► MatchEnd
   └────────── (если ещё есть раунды) ──► RoundActive
```

- **Warmup** — разогрев перед первым раундом, урон отключён.
- **RoundActive** — игроки бьются, ведётся подсчёт киллов/смертей.
- **RoundEnd** — пауза, начисление победителю +1 в `RoundWins`, экран "Раунд окончен".
- **MatchEnd** — финальный экран, ждёт ручного `RestartMatch()`.

---

## Поток урона и смерти

```
PlayerShooting.CmdShoot()
   ├─ Raycast на сервере
   ├─ playerRewind.RegisterOutgoingDamage(target, dmg)  ──► id
   └─ target.TakeDamage(dmg, attacker, id)
         ├─ target.PlayerRewind.RegisterIncomingDamage(...)
         ├─ health -= dmg
         └─ if (health <= 0): Die(killer)
                 ├─ shooting.CancelReload()
                 ├─ TargetOnDied(client)
                 └─ GameManager.ReportKill(victim, killer)
                         ├─ Scores[victim].Deaths++
                         ├─ Scores[killer].Kills++ (если есть)
                         └─ StartCoroutine(RespawnAfter(victim, respawnDelay))
                                 └─ player.ServerRespawn(spawnPos, spawnRot)
```

---

## Фича перемотки времени 🌀

Игрок нажимает `Z` (по умолчанию) — `PlayerRewindInput` шлёт `CmdRequestRewind`.

`PlayerRewind` на сервере:
1. Каждые `snapshotInterval` (0.1с) пишет снапшот: позиция, ротация, здоровье, патроны.
2. Хранит окно `rewindWindowSeconds` (5с).
3. По запросу:
   - блокирует ввод клиента (`TargetSetLocalControls(false)`),
   - проигрывает обратное движение через ключевые точки (`rewindPlaybackDuration` = 0.45с),
   - применяет состояние из прошлого: позицию, здоровье, патроны,
   - **откатывает исходящий урон** (`UndoOutgoingDamageAfter`) — лечит тех, кого ранил после точки отката,
   - **очищает входящий урон** в чужих списках (через `RemoveIncomingDamage` / `RemoveOutgoingDamage` парами),
   - возвращает управление клиенту.
4. Кулдаун `rewindCooldownSeconds` (8с).

---

## Что было удалено при рефакторинге

| Файл | Причина |
|------|---------|
| `MatchManager.cs` | Дублировал `GameManager` (старая FSM, не использовалась) |
| `NetworkShootingAndHealth.cs` | Дублировал `PlayerShooting` + завязка на InfimaGames LPSP |
| `BulletHandler_Networked.cs` | Пустой `CmdPerformShot`, эффекты делает `PlayerShooting.RpcShowHitEffect` |
| `NetworkPlayerSetup.cs` | Логика отключения чужих компонентов уже есть в `FPSInput` |
| `Assets/_Recovery/` | Папка автовосстановления Unity (мусор) |
| `multiplayer_match.patch` | Неиспользуемый patch-файл |
| `SETUP_README.md` | Дублировал содержимое этого README |
| Перегрузки `PlayerHealth.ServerRespawn(...)` (5 шт.) | Сведены к одной `ServerRespawn(Vector3, Quaternion)` |

Битые ссылки в `Assets/PreFab/P_LPSP_FP_CH.prefab` на удалённые скрипты вычищены.

---

## Как настроить в Unity

### 1. Префаб игрока (`Assets/PreFab/P_LPSP_FP_CH.prefab`)
На GameObject игрока должны висеть:
- `NetworkIdentity`
- `NetworkTransform` (для синка позиции)
- `NetworkAnimator` (если есть аниматор)
- `CharacterController`
- `PlayerHealth`
- `PlayerShooting` (заполнить `loadout`, `weaponHolder`, `recoilScript`, `fireAction`)
- `PlayerRewind`
- `PlayerRewindInput`
- `FPSInput`, `MouseLookX`, `MouseLookY`

### 2. `MatchNetworkManager` в сцене главного меню
- `Player Prefab` → `P_LPSP_FP_CH.prefab`
- `Game Manager Prefab` → `Assets/PreFab/Game/GameManager.prefab`
- В `Spawnable Prefabs` зарегистрировать `GameManager.prefab`
- `Game Scene Names` — список имён игровых сцен (`Demo_1`, `Demo_02`)

### 3. На игровой сцене
- Расставить `Assets/PreFab/Game/SpawnPoint.prefab` по карте (минимум 2-4 шт.)
- Бросить `MatchHUD` на Canvas и привязать ссылки на TMP-тексты, скорборд и баннер.

---

## Управление по умолчанию

| Клавиша | Действие |
|---------|----------|
| WASD | Передвижение |
| Shift | Спринт |
| Ctrl | Присесть |
| Space | Прыжок |
| ЛКМ | Стрельба |
| R | Перезарядка |
| **Z** | **🌀 Перемотка времени (5с назад)** |
| Tab | Таблица счёта |

---

## Зависимости
- **Unity 2022.3+** (использован `FindObjectsByType`)
- **Mirror Networking** (входит в проект, `Assets/Mirror/`)
- **TextMeshPro**
- **Input System** (новый)

---

## Как играть локально
1. Открыть сцену `MainMenu`.
2. Host → выбрать локацию (`Demo_1` или `Demo_02`).
3. Второй клиент: `localhost` → Join.
4. Идёт Warmup (5с) → раунд (180с) → перерыв (5с) → следующий раунд... Всего 5 раундов.
