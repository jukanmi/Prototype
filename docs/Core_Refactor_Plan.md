# Core 리팩토링 계획 (캔버스 #5)

작성 2026-09-06 · 대상 `Assets/Scripts/Core/` — 14 파일 1,438 줄
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)
자매 문서: [Builder](Builder_Refactor_Plan.md) · [Battle](Battle_Refactor_Plan.md) ·
[UI](UI_Refactor_Plan.md) · [Tactic](Tactic_Refactor_Plan.md) · [Entities](Entities_Refactor_Plan.md)

> **Core는 이미 잘 정리된 폴더다.** 평균 103줄, 대부분 순수 규칙 테이블과
> enum/struct다. 여기서 뜯어낼 군살은 **죽은 코드 33줄**뿐이다.
>
> 대신 **다른 계획들의 도착지**다 — `UiKit.cs` · `UiLayer.cs`가 여기로 들어온다.

---

## 0. 현황

| 파일 | 줄 | 담긴 것 | 프리팹/씬 |
|---|---:|---|---:|
| Enums.cs | 195 | enum 14개 | 0 |
| CombatStateRules.cs | 185 | `static CombatStateRules` | 0 |
| HitData.cs | 178 | `struct DamageData` · `struct HitData` | 0 |
| CameraFollow.cs | 162 | `CameraFollow : MonoBehaviour` | **10** |
| BattleRegistry.cs | 162 | `static BattleRegistry` | 0 |
| BasicAttackCombo.cs | 152 | `struct BasicAttackStage` · `enum BasicComboStep` · `static BasicComboRules` | 0 |
| CameraAnchor.cs | 132 | `CameraAnchor : MonoBehaviour` | **1** |
| BattleLog.cs | 80 | `enum LogCategory` · `static BattleLog` | 0 |
| StatusRules.cs | 52 | `static StatusRules` | 0 |
| AttackInputBuffer.cs | 46 | `struct AttackInputBuffer` | 0 |
| TimeControl.cs | 31 | `static TimeControl` | 0 |
| BattleLogSettings.cs | 25 | `BattleLogSettings : MonoBehaviour` | **0** ← 문제 |
| RoleNames.cs | 24 | `static RoleNames` | 0 |
| Interfaces.cs | 14 | `IHittable` · `IDamageable` | 0 |

**프리팹에 물린 건 2개뿐** — `CameraFollow`(10곳) · `CameraAnchor`(1곳).
나머지 12개는 순수 코드라 병합이 자유롭다.

### 타입별 소비자 (파일이 아니라 타입 기준)

```
BattleLog · LogCategory  40      Role              12
DamageData               20      TargetingType      9
CombatState              16      StatType           9
HitData                  14      AttackType         9
                                 StatusKind         8
Command · Debuff · Faction  7    PhysicsState · KnockbackMode  6
TargetPick               5       EnergyType         4
RoleNames · AttackInputBuffer  3
IHittable                2       BasicComboStep     2
IDamageable              1
GameState                0   ←  죽음
BattleLogSettings        0   ←  죽음
```

---

## 1. 삭제 — 죽은 코드 2건 33줄

### 1-1. `enum GameState` (Enums.cs:187-194, 8줄)

`Assets/Scripts` · `Assets/Editor` 전수 검색에서 **선언부 외 참조 0**.

```csharp
public enum GameState { Title, InGame, Paused, Settings, Dead }
```

씬 흐름은 `yg/GameManager.cs` · `yg/SceneNames.cs`가 따로 관리한다.
이 enum은 그 설계 이전의 잔재다.

### 1-2. `BattleLogSettings.cs` (25줄) — 어디에도 안 붙어 있다

주석은 *"인스펙터에서 로그 카테고리를 토글한다. 씬 아무 오브젝트에나 하나 붙이면
된다"* 인데, **에디터에서 전수 조회한 결과 프리팹 25개 · 씬 13개 어디에도 없다**
([Prefab_Snapshot.md](Prefab_Snapshot.md) 4절). 코드 참조도 0.

결과적으로 `BattleLog.Mask`는 항상 기본값 `LogCategory.All`로만 돈다 —
**토글 기능은 이미 죽어 있다.**

> 판단이 갈릴 수 있는 지점: 지우지 말고 **씬에 붙여서 살릴** 수도 있다.
> 로그 카테고리 40개 소비자가 전부 `All`로 쏟아지는 게 불편했다면 그쪽이 맞다.
> 그게 아니면 삭제. **이 문서는 삭제로 잡는다** — 한 달 넘게 안 붙어 있었다면
> 필요 없었던 것이다.

---

## 2. 병합 — 14 → 11

병합은 줄을 안 줄인다. 파일 개수와 탐색 비용만 줄인다.

### 2-1. `Interfaces.cs`(14) → `HitData.cs`

`IHittable`의 시그니처가 `HitData`를 그대로 받는다:

```csharp
bool Hit(in HitData hitData, Combat attacker);
```

14줄짜리 독립 파일을 유지할 이유가 없다. `IDamageable` 소비자 1
(`Entities/Combat.cs`), `IHittable` 소비자 2 (`Combat` · `Attack`).

### 2-2. `StatusRules.cs`(52) + `CombatStateRules.cs`(185) → `Rules.cs`(237)

`StatusRules` 주석이 직접 형제 관계를 선언한다:

> `CombatStateRules`와 **같은 성격이다** — 유니티 객체를 만지지 않으므로
> EditMode에서 그대로 검증되고, 표가 코드 여기저기로 흩어지지 않는다.

둘 다 *"표를 **한 벌만** 유지하는 정적 규칙 테이블"*. 같은 파일이 맞다.

> `BasicComboRules`도 같은 성격이지만 **합치지 않는다** —
> `BasicAttackCombo.cs`는 `BasicAttackStage`(struct) · `BasicComboStep`(enum) ·
> `BasicComboRules`가 한 세트로 묶인 파일이라, 규칙만 떼면 나머지가 흩어진다.

### 2-3. 나머지는 그대로

| 파일 | 유지 이유 |
|---|---|
| `CameraFollow.cs` · `CameraAnchor.cs` | **둘 다 프리팹에 물림** (10곳 · 1곳). MonoScript 규칙상 각자 파일 필요 |
| `Enums.cs` | enum 13개가 이미 한 파일에 모여 있다 |
| `BattleRegistry` · `BattleLog` · `TimeControl` · `AttackInputBuffer` · `RoleNames` · `BasicAttackCombo` | 각자 독립된 관심사. 합칠 이웃이 없다 |

---

## 3. 목표 배치

```
Core/
├ Enums.cs             enum 13개 (GameState 삭제)                  187
├ Rules.cs             CombatStateRules + StatusRules              237
├ HitData.cs           DamageData + HitData + IHittable + IDamageable  192
├ BattleRegistry.cs                                                162
├ CameraFollow.cs      (프리팹 10 — 파일명 고정)                     162
├ BasicAttackCombo.cs  BasicAttackStage + BasicComboStep + BasicComboRules  152
├ CameraAnchor.cs      (프리팹 1 — 파일명 고정)                      132
├ BattleLog.cs         LogCategory + BattleLog                       80
├ AttackInputBuffer.cs                                              46
├ TimeControl.cs                                                    31
└ RoleNames.cs                                                      24
                                                        합계     1,405
```

**14 파일 1,438줄 → 11 파일 1,405줄 (-33)**

### 여기에 다른 계획이 들어온다

| 출처 | 신설 파일 | 줄 |
|---|---|---:|
| [UI](UI_Refactor_Plan.md) 1단계 | `Core/UiKit.cs` — `UiFactory` + `yg/SimpleUI` + `UiLayer` | ~230 |
| [Battle](Battle_Refactor_Plan.md) 4단계 | `yg/` 해체분 — `GameManager` · `SceneLoader` · `SceneNames` · `BootStrapper` · `AudioManager` · `UIManager` · `MainMenuController` | ~846 |

**Core는 최종적으로 커진다.** #5 자체의 정리(-33줄, -3파일)보다 유입이 훨씬 크다.
그래서 이 문서의 병합은 **유입 전에** 해 두는 게 낫다 — 나중에 하면 뒤섞인다.

---

## 4. 빌더 삭제와의 연결

[Builder_Refactor_Plan.md](Builder_Refactor_Plan.md) 1단계로 빌더 15개를 지우면
Core 타입의 소비자 수가 줄어든다:

| 타입 | 지금 | 빌더 삭제 후 | 사라지는 참조 |
|---|---:|---:|---|
| `CameraAnchor` | 5 | 3 | `ArenaSceneBuilder` · `BattleInputBuilder` |
| `RoleNames` | 3 | 2 | `AllyPrefabBuilder` |
| `AttackType` | 9 | 7 | `ProjectileBuilder` · (`SkillTableBuilder`는 유지) |
| `TargetPick` | 5 | 4 | (`SkillTableBuilder` 유지) |

특히 `CameraAnchor` 주석이 말하는 문제가 절반 해결된다:

> 지금까지 `CameraFollow.SetTarget`이 몸 트랜스폼을 직접 받았고, 그 배선이
> **세 군데에서 각자** 이뤄졌다 — `ArenaSceneBuilder` · `CameraFollow`의 Awake
> 폴백 · `TagSwapController`.

세 군데 중 하나(`ArenaSceneBuilder`)가 빌더 삭제로 사라진다.
**빌더 삭제를 먼저 하면 이 문서 작업이 조금 가벼워진다.**

---

## 5. 단계

각 단계 = 별도 커밋. 전부 프리팹 무관이라 **지금 바로 가능하다.**

### 1단계 — 죽은 코드 삭제 (33줄)

- `Enums.cs`에서 `enum GameState` 블록 제거 (187-194행)
- `BattleLogSettings.cs` + `.cs.meta` 삭제
- `BattleLog.cs`에 `Mask`를 누가 설정하는지 주석 한 줄 정리
  (설정 주체가 사라지므로 "기본값 `All` 고정"임을 명시)

### 2단계 — `Interfaces.cs` → `HitData.cs`

- `IHittable` · `IDamageable`을 `HitData.cs`의 `namespace` 안으로 이동
- `Interfaces.cs` + `.cs.meta` 삭제
- 네임스페이스 동일 확인 후 이동

### 3단계 — `Rules.cs` 신설

- `CombatStateRules.cs` + `StatusRules.cs` → `Rules.cs`
- 원본 2개 + `.cs.meta` 삭제
- **새 파일이라 새 guid가 생긴다** — 프리팹 참조가 0이므로 문제없다

> 1~3단계는 서로 독립적이다. 순서 무관, 한 커밋으로 묶어도 된다.

### 4단계 이후 — 유입 대기

`UiKit.cs`(UI 1단계) · `yg/` 해체분(Battle 4단계)이 들어오기 전에
1~3단계를 끝내 둔다.

---

## 6. 결과

```
Core/   14 파일 1,438 줄  →  11 파일 1,405 줄   (-33)
```

누적:

| 캔버스 | 그룹 | 전 | 후 | 감소 |
|---|---|---|---|---:|
| — | 빌더 | 26 파일 9,089 | 8 파일 2,578 | **-6,511** |
| #1 | Battle | 17 파일 3,295 | 7 파일 3,009 | -286 |
| #2 | Battle/UI | 12 파일 3,867 | 8 파일 2,735 | **-1,132** |
| #3 | Battle/Tactic | (#1에 흡수) | — | — |
| #4 | Entities | 13 파일 3,898 | 11 파일 3,888 | -10 |
| #5 | Core | 14 파일 1,438 | 11 파일 1,405 | -33 |

---

## 7. 판단이 필요한 것

**`BattleLogSettings`를 지울 것인가, 씬에 붙여 살릴 것인가.**

- 지금: 어디에도 안 붙어 있어 `BattleLog.Mask`가 항상 `All`
- 살리면: 로그 카테고리 40개 소비자를 인스펙터에서 끌 수 있다
- 지우면: 25줄 + 죽은 기능 하나 정리

이 문서는 **삭제**로 잡았다. 로그가 시끄러워서 불편했던 적이 있으면 반대로 간다.

---

## 검증

전부 프리팹 무관이라 씬 재생 검증이 가볍다.

**1단계 후**
- [ ] 컴파일 통과 (`GameState` 참조가 정말 없었는지 확인됨)
- [ ] 전투 로그가 이전과 같이 나오는가 (`BattleLogSettings` 삭제 영향 없음 확인)

**2·3단계 후**
- [ ] 컴파일 통과 — `IHittable` · `IDamageable` 구현체(`Combat`)와
      호출처(`Attack`)가 정상인가
- [ ] 상태 전이가 이전과 같은가 — 피격 → 다운 → 기상 사슬
- [ ] 상태이상(스턴 · 빙결) 적용/해제가 정상인가 (`StatusRules` 이동 확인)
- [ ] `.meta` 고아 경고가 없는가
