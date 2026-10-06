# Battle 리팩토링 계획 (캔버스 #1)

작성 2026-09-06 · 대상 `Assets/Scripts/Battle/` (UI 하위 폴더 제외) — 17 파일 3,295 줄
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)
전제: **코드량 감소가 목표.** 프리팹·에디터 수작업이 늘어도 상관없다.
프리팹/`.asset`에 붙는 MonoBehaviour·ScriptableObject는 유니티 MonoScript 규칙상
**자기 이름의 파일이 필요하다.**

---

> **조립 코드 0줄.** UI가 아니라 전투 로직이라 프리팹 이관으로 회수할 게 없다.
> 줄 감소는 `DebugComboHUD` 삭제분 286줄이 전부다.

## 0. 현황

| 파일 | 줄 | 종류 | 프리팹/씬 |
|---|---:|---|---|
| BulletTimeController.cs | 897 | MonoBehaviour | `CombatManager.prefab` |
| TagSwapController.cs | 589 | MB, `ICasterStage` 구현 | `BattleInput.prefab` |
| ComboExecutor.cs | 389 | MonoBehaviour | `CombatManager.prefab` |
| DebugComboHUD.cs | 286 | MonoBehaviour | `CombatManager.prefab` |
| TargetSelector.cs | 220 | MonoBehaviour | `CombatManager.prefab` |
| TrainingDummy.cs | 185 | MonoBehaviour | `Enemy_Dummy.prefab` (씬 2곳) |
| Tactic/TacticStates.cs | 124 | class ×4 | 0 |
| ComboMeter.cs | 110 | 순수 class | 0 |
| SkillCooldownTracker.cs | 100 | 순수 class | 0 |
| TagSwapRules.cs | 74 | 순수 static | 0 |
| Tactic/TacticStateMachine.cs | 68 | 순수 class | 0 |
| BattleCommander.cs | 53 | MonoBehaviour | `BattleInput.prefab` |
| ICasterStage.cs | 51 | interface | 0 |
| EnemyRadiusProbe.cs | 50 | MonoBehaviour | **0** |
| Tactic/TacticPhase.cs | 47 | enum + class | 0 |
| ComboSlot.cs | 26 | struct | 0 |
| ISkillCutin.cs | 26 | interface | 0 |

## 1. 의존 그래프

```
BulletTimeController ──┬─→ ComboExecutor ──┬─→ ComboSlot
  (관제탑 897)          ├─→ TagSwapController │  ICasterStage  ← TagSwapController 구현
                       ├─→ TargetSelector    └─ ISkillCutin    ← UI/SkillCutinUI 구현
                       ├─→ SkillCooldownTracker
                       ├─→ ComboSlot
                       └─→ Tactic/*

TagSwapController ──┬─→ TagSwapRules
  (589)             ├─→ BattleCommander
                    ├─→ BulletTimeController   ← 순환
                    └─→ ComboExecutor          ← 순환

TargetSelector ──→ EnemyRadiusProbe   (KnockbackIndicator도 같이 씀)
ComboMeter ──→ Core/BasicAttackCombo.cs:88 ; ←── Battle/UI/ComboDamageHUD
TrainingDummy    의존 0. 완전 고립
DebugComboHUD    읽기만 함 → 삭제 (2-1)
```

---

## 2. 결정 사항

### 2-1. `DebugComboHUD` 삭제 (286줄 + 에디터 배선 ~35줄)

자기 주석이 근거다 (`DebugComboHUD.cs:9`):

> 손패 조작은 `ComboBoardUI`가 전담한다. 여기서는 입력을 받지 않고
> 덱 · 손패 · 파티 · 적 상태를 읽어서 보여 주기만 한다.

`ComboBoardUI`(1,227줄)와 같은 데이터를 다르게 그리는 OnGUI 개발용 패널이다.
프리팹을 다시 만들 때 배선을 되살릴 값어치가 없다.

**같이 손볼 곳** (전수 조사 완료):

| 위치 | 내용 | 처리 |
|---|---|---|
| `Scripts/Party/PartyAssembler.cs:396` | `bulletTime.GetComponent<DebugComboHUD>()?.SetHero(Hero);` | 줄 삭제 (`?.`라 안전) |
| `Scripts/Party/PartyAssembler.cs:26` | 문서 주석 언급 | 문구 수정 |
| `Editor/SceneLayoutBuilder.cs:643-673` | `Undo.AddComponent` + 배선 ~30줄 | **빌더째 삭제됨** |
| `Editor/BattleInputBuilder.cs:143` | `Wire(...)` | **빌더째 삭제됨** |
| `Scripts/yg/Editor/FlowSceneBuilder.cs:207` | 문서 주석 언급 | **빌더째 삭제됨** |
| `Prefabs/CombatManager.prefab` | 컴포넌트 | 프리팹 재작성 시 제외 |

> **[Builder_Refactor_Plan.md](Builder_Refactor_Plan.md) 1단계를 먼저 하면
> 6곳 중 3곳이 저절로 사라진다.** 빌더 삭제를 앞에 둘 것.

`public` API는 `SetHero(Player)` 하나, 호출자 1곳. 테스트 참조 0
(`Assets/Editor/Tests/` 전수 확인).

### 2-2. `TrainingDummy` — 이번 범위 밖

`Enemy_Dummy.prefab` + `Skill_test.unity`에 붙어 있다. `CombatManager` ·
`BattleInput` 프리팹 재작성에 딸려오지 않는다. 의존 0이라 언제 옮겨도 안전.
**제자리 유지.**

### 2-3. `Tactic/` 전부 `BulletTimeController.cs`로 이관

상세: **[Tactic_Refactor_Plan.md](Tactic_Refactor_Plan.md)**

`Tactic/` 3파일 239줄은 `BulletTimeController`가 소유하고
(`:92` 필드 · `:179` 유일한 생성처), 외부 소비자 2곳이 전부 그를 거쳐 접근한다.
프리팹 참조 0이라 지금 바로 가능하다.

**결과: `BulletTimeController.cs` 897 → ~1,121줄.**

> **넘침 규칙** — `BulletTimeController.cs`가 1,500줄을 넘으면 유니티 비의존
> 부분을 `Battle/System/`으로 뺀다. 나가는 순서: `Tactic/*`(239) →
> `SkillCooldownTracker`(100) → `ComboSlot`(26). 지금은 1,121이라 안 넘는다.
>
> `Battle/System/` 입주 기준: **`UnityEngine` 의존이 없어 EditMode에서 그대로
> 돌릴 수 있는 것.** `ComboMeter`가 이미 그 기준으로 서 있다.

---

## 3. 병합 — 17 → 7

**파일명 고정 6개**: `BulletTimeController` · `TagSwapController` ·
`ComboExecutor` · `TargetSelector` · `BattleCommander` · `TrainingDummy`
(`DebugComboHUD`는 삭제)

`EnemyRadiusProbe`만 MonoBehaviour인데 **프리팹 참조 0** — 유일하게 흡수 가능하다.

```
Battle/
├ BulletTimeController.cs   + ComboSlot + SkillCooldownTracker
│                           + Tactic 3개          (프리팹 고정)   ~1,121
├ TagSwapController.cs      + TagSwapRules        (프리팹 고정)     ~660
├ ComboExecutor.cs          + ICasterStage + ISkillCutin
│                                                 (프리팹 고정)     ~464
├ TargetSelector.cs         + EnemyRadiusProbe    (프리팹 고정)     ~268
├ BattleCommander.cs                              (프리팹 고정)       53
├ TrainingDummy.cs                                (프리팹 고정)      185
└ ComboMeter.cs                                                     110
                                                          합계   ~2,861
```

**병합 근거**

- `ComboSlot`(26) · `SkillCooldownTracker`(100)는 소비자가 `BulletTimeController`
  하나뿐. `Tactic/`은 2-3 참조.
- `ICasterStage`(구현: `TagSwapController`) · `ISkillCutin`(구현: `UI/SkillCutinUI`)
  둘 다 **소비자가 `ComboExecutor` 하나**다. 계약은 소비자 옆에 둔다.

  > **구현체가 1개씩이지만 지우면 안 된다.** `ISkillCutin`을 지우면
  > `Battle/ComboExecutor → Battle/UI/SkillCutinUI` 직접 의존이 생겨 순환한다.
  > **순환을 끊는 장치**이지 "추상화를 위한 추상화"가 아니다.
- `EnemyRadiusProbe` 주석: *"`TargetSelector`의 조준 HUD와 `KnockbackIndicator`의
  화살표 대상 목록이 **같은 함수**를 봐야 한다."* 1:1 결합 + 프리팹 0 → 흡수.

**합치지 않는 것**

- `BattleCommander`(53) — `TagSwapController`에 합치고 싶지만
  `BattleInput.prefab`에 물려 있어 파일 유지. 존재 이유가 태그 교대 제약
  그 자체다 (`:8`): *"절대 꺼지지 않는 오브젝트에 붙어야 한다. 태그로 내려가는
  몸에 붙이면 교대하는 순간 되돌아올 키까지 함께 죽는다."*
- `ComboMeter`(110) — 유니티 비의존 순수 계산. 주석: *"EditMode에서 그대로
  돌릴 수 있다."* 소비자도 `Battle/UI/ComboDamageHUD` 하나뿐이라 축이 다르다.

---

## 4. 단계

각 단계 = 별도 커밋. **1~3단계는 프리팹 배선을 안 건드린다** — 내용만 옮기고
파일명을 유지하므로 guid가 산다.

### 0단계 — 선행

[Builder_Refactor_Plan.md](Builder_Refactor_Plan.md) 1단계(빌더 삭제).
2-1의 처리 항목 6개 중 3개가 여기서 사라진다.

### 1단계 — `DebugComboHUD` 삭제 (-286줄)

2-1의 남은 3곳 처리.

### 2단계 — `Tactic/` 병합

[Tactic_Refactor_Plan.md](Tactic_Refactor_Plan.md). 1단계와 같은 커밋 가능 —
`DebugComboHUD:38-44`가 `TacticPhase`를 쓰는 마지막 외부 참조다.

### 3단계 — 나머지 병합 (17 → 7)

- `TagSwapRules` → `TagSwapController.cs`
- `ICasterStage` · `ISkillCutin` → `ComboExecutor.cs`
- `EnemyRadiusProbe` → `TargetSelector.cs`

흡수되는 쪽이 전부 프리팹 0이고, 흡수하는 쪽은 파일명을 유지한다.

### 4단계 — 프리팹 재작성 시 정리

프리팹은 **중첩 구조**다 ([Prefab_Snapshot.md](Prefab_Snapshot.md) 1절):

```
Player/BattleInput.prefab
├─ BattleInput      [PlayerInputController, InputMapSwitcher, PlayerPilot,
│                    TagSwapController, BattleCommander, PartyAssembler,
│                    PartyHealthHUD]
├─ Party
├─ CameraAnchor     [CameraAnchor]
└─ CombatManager    ← 중첩: CombatManager.prefab
                    [BulletTimeController, ComboExecutor, TargetSelector,
                     DebugComboHUD, ComboBoardUI, RecentHitEnemyHUD,
                     DeckInspectorUI, SkillCutinUI]
```

이 문서 소관 컴포넌트는 **전부 한 곳에만 있다**:

| 프리팹 | 붙는 것 |
|---|---|
| `CombatManager.prefab` | `BulletTimeController` · `ComboExecutor` · `TargetSelector` |
| `BattleInput.prefab` | `TagSwapController` · `BattleCommander` |
| (제외) | `DebugComboHUD` — 1단계에서 삭제됨 |

> **`CombatManager` 자체는 UI 프리팹화 때 자식 4개로 쪼개진다**
> ([UI_Refactor_Plan.md](UI_Refactor_Plan.md) 4절). 이 문서의 3개는 루트에 남는다.

> 인스펙터 `[SerializeField]` 값은 **[Prefab_Snapshot.md](Prefab_Snapshot.md) 6절**에
> 떠 두었다. `BulletTimeController`만 튜닝값이 15개다.

---

## 5. 결과

```
Battle/   17 파일 3,295 줄  →  7 파일 3,009 줄   (-286)
Tactic/ 폴더               →  BulletTimeController.cs로 흡수
```

---

## 6. 이 문서 밖

- `TrainingDummy` 이동 (2-2)
- 에디터 빌더 → [Builder_Refactor_Plan.md](Builder_Refactor_Plan.md)
- `Core/BasicAttackCombo.cs`의 `BasicComboRules` — `ComboMeter`가 참조.
  [Core_Refactor_Plan.md](Core_Refactor_Plan.md) 소관

---

## 검증 — 씬 재생해서 눈으로 확인할 것

**1단계 후 (`DebugComboHUD` 삭제)**
- [ ] 전투 진입 시 예외 없음 — `PartyAssembler:396` 삭제 확인
- [ ] 좌상단 OnGUI 패널만 사라지고 `ComboBoardUI` 손패는 그대로

**2단계 후 (`Tactic` 병합)**
- [ ] 불릿타임 `Freeze` → `Order` → `Resolve` → `RealTime` 전이
- [ ] 태그 교대가 `RealTime`에서만 되는가 (`TagSwapController:137`)

**3단계 후 (나머지 병합)**
- [ ] 콤보 큐 실행 순서, 스킬 컷인 재생 (`ISkillCutin`)
- [ ] 조준 "몇 명 맞는가" 카운트 (`EnemyRadiusProbe`)
- [ ] 넉백 화살표 대상이 조준 카운트와 일치하는가
- [ ] 태그 교대 후 지휘 키 유지 (`BattleCommander`)

**4단계 후 (프리팹 재작성)**
- [ ] `slotGap` 등 인스펙터 튜닝값이 새 프리팹에 반영됐는가
- [ ] 프리팹 6개가 `Missing (Mono Script)`가 아닌가
- [ ] Boot 씬 경유 / 배틀 씬 단독 Play 양쪽
