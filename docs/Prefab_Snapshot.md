# 프리팹 · 씬 배치 실측 + 컴파일 baseline

작성 2026-09-06 · Unity 6000.5.5f1 에디터에서 직접 조회
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)

리팩토링 착수 전 기준값. **재작성 후 이 문서와 대조한다.**

---

## 0. 컴파일 baseline

```
컴파일 에러      0
콘솔 에러        0
콘솔 경고        1   (MCP 서명 수집 — 환경 문제, 프로젝트 무관)
Missing Script   0   (프리팹 25개 전수 검사)
```

**리팩토링 후 이보다 늘면 작업이 만든 것이다.**

### 컴파일 경고 — deprecated `FindObjectsSortMode` (CS0618)

| 파일                            | 줄       |
| ----------------------------- | ------- |
| `Control/PlayerPilot.cs`      | 83      |
| `Party/PartySpawnPoint.cs`    | 48-49   |
| `Stage/StageRunner.cs`        | 85      |
| `yg/BattleSceneController.cs` | 232-233 |

Unity 6에서 `FindObjectsByType<T>(FindObjectsInactive, FindObjectsSortMode)`가
폐기됐다. 파라미터만 빼면 된다 — **4곳 각 1~2줄.** 리팩토링과 무관하게 지금 고칠 수 있다.

---

## 1. 프리팹 구조 — 중첩이다

```
Player/BattleInput.prefab
├─ BattleInput          [PlayerInput, PlayerInputController, InputMapSwitcher,
│                        PlayerPilot, TagSwapController, BattleCommander,
│                        PartyAssembler, PartyHealthHUD]
├─ Party
├─ CameraAnchor         [CameraAnchor]
└─ CombatManager        ← 중첩: CombatManager.prefab
                        [BulletTimeController, ComboExecutor, TargetSelector,
                         DebugComboHUD, ComboBoardUI, RecentHitEnemyHUD,
                         DeckInspectorUI, SkillCutinUI]
```

두 가지가 여기서 결정된다.

**① `CombatManager.prefab`은 `BattleInput.prefab`의 자식이다.**
컴포넌트 8개가 **전부 GameObject 하나**에 붙어 있다.

**② UI 프리팹화는 `CombatManager` 분할을 요구한다.**
지금은 UI 4개가 각자 코드로 캔버스를 세우니까 한 오브젝트에 얹혀 있을 수 있다.
프리팹으로 되돌리면 각 UI가 자기 캔버스 계층을 가져야 하므로
**`CombatManager` 아래 자식 4개로 쪼개야 한다.**
[UI_Refactor_Plan.md](UI_Refactor_Plan.md) 4절 3단계 참조.

---

## 2. 프리팹 배치 (중첩은 원본에 귀속)

### 전투 제어 · UI — 각 1곳뿐

| 컴포넌트 | 프리팹 |
|---|---|
| BulletTimeController · ComboExecutor · TargetSelector · DebugComboHUD | `CombatManager.prefab` |
| ComboBoardUI · DeckInspectorUI · SkillCutinUI · RecentHitEnemyHUD | `CombatManager.prefab` |
| TagSwapController · BattleCommander · PartyAssembler · PartyHealthHUD | `Player/BattleInput.prefab` |
| PlayerInputController · InputMapSwitcher · PlayerPilot · CameraAnchor | `Player/BattleInput.prefab` |

**전부 1곳이다.** 프리팹 재작성 대상은 이 두 프리팹뿐.

### 캐릭터 — 12~13곳

| 컴포넌트 | 수 | 프리팹 |
|---|---:|---|
| `Attack` | **13** | 적 6 + 아군 6 + `Projectile.prefab` |
| `Combat` · `Physics` · `EntityAnimator` · `BeltScrollView` | **12** | 적 6 + 아군 6 |
| `Enemy` · `EnemyBasicAttack` · `EnemyControl` | 6 | `enemy` · `Enemy_Boss/Charger/Dummy/Melee/Ranged` |
| `AllyBasicAttack` · `Pilotable` | 6 | 아군 5 + `Player.prefab` |
| `Ally` | 5 | `Ally` · `Ally_Arc/Tan/War/Wiz` |
| `Player` | 1 | `Player/Player.prefab` |
| `Projectile` | 1 | `Projectile.prefab` |
| `TrainingDummy` | 1 | `Enemy_Dummy.prefab` |

### 프리팹에 없는 것

```
Entity              ← Player·Enemy·Ally가 상속. 파생만 붙는다
BasicAttackProfile  ← abstract. 파생만 붙는다
EnemyRadiusProbe    ← 코드로만 AddComponent
CameraFollow · RebindUI · ComboDamageHUD · PartySpawnPoint  ← 씬 전용 (3절)
BattleLogSettings   ← 프리팹에도 씬에도 없음. 사문화 (4절)
```

---

## 3. 씬 배치 (`Assets/` 13개)

| 컴포넌트 | 수 | 씬 |
|---|---:|---|
| `BattleSceneController` · `CameraFollow` · `PartyHealthHUD` | **10** | Stage_01~05 · Boss · Mini · Training · SampleScene · Skill_test |
| `PartySpawnPoint` | 9 | 위에서 Skill_test 제외 |
| `EnemySpawnService` | 5 | Stage_01~05 |
| `StageDirector` | 3 | Stage_01 · 03 · 04 (웨이브형) |
| `ArenaDirector` · `ArenaGate` · `StageRunner` · `StageBounds` | 2 | Stage_02 · 05 (아레나형) |
| `TrainingDummy` | 2 | Stage_Training · Skill_test |
| `GameManager` · `SceneLoader` · `AudioManager` · `UIManager` · `BootStrapper` · `RebindUI` · `ComboDamageHUD` | 1 | `Boot.unity` |
| `MainMenuController` | 1 | `MainMenu.unity` |

> `PartyHealthHUD`가 씬 10곳에 뜨는 건 `BattleInput.prefab` 인스턴스가 각 씬에
> 놓여 있기 때문이다. **실체는 프리팹 1곳.**

> `StageDirector`(웨이브 3씬)와 `ArenaDirector`(아레나 2씬)가 겹치지 않는다 —
> [Stage_Refactor_Plan.md](Stage_Refactor_Plan.md) 1절의 "둘로 갈린 이유"가
> 배치로 확인된다.

---

## 4. `BattleLogSettings` — 사문화 확정

**프리팹 25개 · 씬 13개 어디에도 없다.** 코드 참조도 0.

`BattleLog.Mask`는 항상 기본값 `LogCategory.All`로 돈다 —
로그 카테고리 토글 기능이 이미 죽어 있다.

[Core_Refactor_Plan.md](Core_Refactor_Plan.md) 1-2의 삭제 판단이 확인됐다.

---

## 5. Brain `.asset` 타입 바인딩

런타임 로드까지 확인. **5개 전부 정상, 파일명 고정.**

```
Brain_Boss.asset    -> BossBrainAsset
Brain_Charger.asset -> ChargerBrainAsset
Brain_Dummy.asset   -> DummyBrainAsset
Brain_Melee.asset   -> MeleeBrainAsset
Brain_Ranged.asset  -> RangedBrainAsset
```

`DummyBrainAsset`은 코드 소비자가 삭제 예정 빌더(`TrainingSceneBuilder`)뿐이지만
`.asset`이 물고 있으므로 **살려 둔다.**

기타: `SkillData` 에셋 **10개**.

---

## 6. 인스펙터 `[SerializeField]` 값

프리팹 재작성 후 이 값들과 대조할 것.

> `{fileID: 0}` = 널 참조. **런타임에 코드가 찾아 넣는다** —
> 손으로 물릴 필요가 없다는 뜻이라 재작성 부담이 그만큼 적다.

```
컴파일 에러      0
컴파일 경고      CS0618 1종 · 4파일
```

### 유일한 경고 — deprecated `FindObjectsSortMode`

| 파일 | 줄 |
|---|---|
| `Control/PlayerPilot.cs` | 83 |
| `Party/PartySpawnPoint.cs` | 48-49 |
| `Stage/StageRunner.cs` | 85 |
| `yg/BattleSceneController.cs` | 232-233 |

> Unity 6에서 `FindObjectsByType<T>(FindObjectsInactive, FindObjectsSortMode)`가
> 폐기됐다. `FindObjectsByType<T>()` 또는 `FindObjectsByType<T>(FindObjectsInactive)`로
> 바꾸면 된다. **4곳 각 1~2줄.** 리팩토링과 무관하게 지금 고칠 수 있다.

로그의 나머지 에러는 전부 **환경 문제**로 프로젝트와 무관하다 —
라이선스 404/handshake, d3d12 info queue, Project ID 403, vscode 확장 서명.

---

## 1. 프리팹 인스펙터 값

프리팹/씬 YAML에서 직접 추출했다 (에디터 불필요).
재작성 후 이 표와 대조할 것.

> `{fileID: 0}` = 널 참조. **런타임에 코드가 찾아 넣는다** —
> 프리팹 재작성 시 손으로 물릴 필요가 없다는 뜻이라 배선 부담이 그만큼 적다.

### 컴포넌트 위치 (guid 전수 확인)

| 컴포넌트 | 위치 |
|---|---|
| ComboBoardUI · DeckInspectorUI · SkillCutinUI · RecentHitEnemyHUD | `Prefabs/CombatManager.prefab` |
| BulletTimeController · ComboExecutor · TargetSelector | `CombatManager.prefab` + `Player/BattleInput.prefab` |
| DebugComboHUD | `CombatManager.prefab` (삭제 예정) |
| TagSwapController · BattleCommander · PartyHealthHUD | `Player/BattleInput.prefab` |
| RebindUI · ComboDamageHUD | `Scenes/Boot.unity` |
| TrainingDummy | `Scenes/Skill_test.unity` + `Prefabs/Enemy_Dummy.prefab` |
| EnemyRadiusProbe | **없음** (코드로만 붙는다) |

```
==============================================================================
## Prefabs\CombatManager.prefab
==============================================================================

### BulletTimeController
    player                             = {fileID: 0}
    executor                           = {fileID: 0}
    targetSelector                     = {fileID: 0}
    swap                               = {fileID: 0}
    maxGauge                           = 100
    gaugeRegen                         = 8
    requiredRatio                      = 1
    parryGaugeReward                   = 12
    manaCost                           = 0
    cooldown                           = 0
    realtimeCooldown                   = 1
    realtimeCooldownMul                = 1
    realtimeGlobalCooldown             = 1.5
    buildDeckOnStart                   = 1
    standaloneStartupMode              = 0
    purgeCardsOnAllyDeath              = 1
    useFixedHand                       = 0
    fixedHand                          = (빈 값 / 하위 블록)
    freezeDuration                     = 0

### ComboExecutor
    slotGap                            = 0.05
    slotTimeout                        = 5

### TargetSelector
    cam                                = {fileID: 0}
    groundY                            = 0
    radiusProbe                        = {fileID: 0}
    cursorSpeed                        = 9
    cursorOrigin                       = {fileID: 0}

### DebugComboHUD
    bulletTime                         = {fileID: 1532985249278245213}
    targetSelector                     = {fileID: 4049905781375668179}
    player                             = {fileID: 0}
    swap                               = {fileID: 0}
    show                               = 1

### ComboBoardUI
    targetSelector                     = {fileID: 0}

### SkillCutinUI
    slideIn                            = 0.15
    hold                               = 0.6
    slideOut                           = 0.15
    labelDelay                         = 0.08
    timeScale                          = 0.15
    portraitSize                       = 360
    portraitShownX                     = 48
    labelShownX                        = 300

==============================================================================
## Prefabs\Player\BattleInput.prefab
==============================================================================

### InputMapSwitcher
    bulletTime                         = {fileID: 0}
    targetSelector                     = {fileID: 0}

### TagSwapController
    player                             = {fileID: 0}
    bulletTime                         = {fileID: 1490962342387795252}
    cameraFollow                       = {fileID: 0}
    cameraAnchor                       = {fileID: 331118652805039931}
    targetSelector                     = {fileID: 4162047843367154618}
    pilot                              = {fileID: 1992139283966176409}
    swapCooldown                       = 1.5

### BattleCommander
    bulletTime                         = {fileID: 1490962342387795252}
    swap                               = {fileID: 8887573751772618589}

### PartyAssembler
    defaultPlayerPrefab                = {fileID: 4540215255994790702, guid: 55a1a8cdc70a56046b881d889cdeeafb, type: 3}
    defaultAllyPrefab                  = {fileID: 9215232113246201951, guid: ff6fb2c9411d8a242958bbd3dc8bdd52, type: 3}
    partyRoot                          = {fileID: 1419850568724524005}
    swap                               = {fileID: 8887573751772618589}
    bulletTime                         = {fileID: 1490962342387795252}
    standaloneLoadout                  = {fileID: 11400000, guid: fba9e8aabde39ad41be2d9cede68d05e, type: 2}

### PartyHealthHUD
    swap                               = {fileID: 8887573751772618589}
    show                               = 1
    showNumbers                        = 1

### CameraAnchor
    rate                               = 20
    follow                             = {fileID: 0}

==============================================================================
## Scenes\Boot.unity
==============================================================================

### GameManager
    deckStartupMode                    = 1
    defaultLoadout                     = {fileID: 0}

### RebindUI
    fallbackActions                    = {fileID: -944628639613478452, guid: 2bcd2660ca9b64942af0de543d8d7100, type: 3}

### ComboDamageHUD
    comboWindow                        = 2.5
    lingerDuration                     = 1.5
    rightMargin                        = 56
    verticalOffset                     = 90
    show                               = 1

### SceneLoader
    fadeCanvas                         = {fileID: 1899919778}
    fadeDuration                       = 0.3

### AudioManager
    bgmSource                          = {fileID: 3673599}
    sfxSource                          = {fileID: 38379368}
    menuBgm                            = {fileID: 8300000, guid: 65845dd52fae34443a943097f74fd331, type: 3}
    battleBgm                          = {fileID: 8300000, guid: bb73edc45b64e604bbc0b3ef3dcb0f7d, type: 3}
    masterVolume                       = 1
    bgmVolume                          = 1
    sfxVolume                          = 1
    hitPitchRange                      = {x: 0.94, y: 1.06}
    hitSfxInterval                     = 0.04

```
