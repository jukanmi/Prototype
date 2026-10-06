# Tactic 리팩토링 계획 (캔버스 #3)

작성 2026-09-06 · 대상 `Assets/Scripts/Battle/Tactic/` — 3 파일 239 줄
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md) · [Battle_Refactor_Plan.md](Battle_Refactor_Plan.md) 2-3절
자매 문서: [UI_Refactor_Plan.md](UI_Refactor_Plan.md)

> **이 단계는 프리팹 의존이 0이다.** 프리팹 재작성을 기다리지 않고
> 지금 바로 실행할 수 있는 유일한 단계.

---

## 0. 현황

| 파일 | 줄 | 내용 | 프리팹/씬 |
|---|---:|---|---|
| `Tactic/TacticStates.cs` | 124 | `RealTimeState` · `FreezeState` · `OrderState` · `ResolveState` | 0 |
| `Tactic/TacticStateMachine.cs` | 68 | `TacticStateMachine` | 0 |
| `Tactic/TacticPhase.cs` | 47 | `enum TacticPhase` + `abstract class TacticState` | 0 |

전부 순수 class/enum. MonoBehaviour 없음.

## 1. live 코드 확인 (죽은 코드 아님)

주석 제외, 실제 호출만 전수 조사:

```
BulletTimeController.cs:92    private TacticStateMachine tactic;         ← 소유 필드
BulletTimeController.cs:179   tactic = new TacticStateMachine(this);     ← 유일한 생성처
BulletTimeController.cs:110   public TacticStateMachine Tactic => tactic;
BulletTimeController.cs:112   public TacticPhase Phase => tactic != null ? tactic.Phase : TacticPhase.RealTime;
BulletTimeController.cs:115   public bool IsActive => Phase == TacticPhase.Freeze || Phase == TacticPhase.Order;
BulletTimeController.cs:309   if (Phase != TacticPhase.RealTime) return false;

TagSwapController.cs:137      bulletTime.Phase == TacticPhase.RealTime
TagSwapController.cs:177/193  bulletTime.Tactic.OnPhaseChanged += / -= HandlePhaseChanged
TagSwapController.cs:356      private void HandlePhaseChanged(TacticPhase prev, TacticPhase next)
TagSwapController.cs:363      if (prev == TacticPhase.Resolve && next == TacticPhase.RealTime)

BattleCommander.cs:41         bulletTime.Tactic.OnBulletTimeKey()

DebugComboHUD.cs:38-44        TacticPhase 스위치  ← Battle 계획 1단계에서 삭제 예정
```

**외부 소비자 2곳(`TagSwapController` · `BattleCommander`)이 전부
`BulletTimeController`를 거쳐서 접근한다.** 직접 `new TacticStateMachine` 하는 곳은
`BulletTimeController:179` 하나뿐 — 소유자가 명확하므로 이관 대상이 확정된다.

## 2. 병합이 공짜인 이유

| 파일 | namespace | using |
|---|---|---|
| `Tactic/TacticPhase.cs` | `Prototype` | 없음 |
| `Tactic/TacticStateMachine.cs` | `Prototype` | `System` |
| `Tactic/TacticStates.cs` | `Prototype` | `System.Collections.Generic` |
| `BulletTimeController.cs` | `Prototype` | `System` · `System.Collections.Generic` · `Prototype.YG` · `UnityEngine` |

**네임스페이스 동일, 필요한 `using`이 이미 전부 있다.** `using` 한 줄도 안 건드린다.
순수 붙여넣기.

---

## 3. 실행

### 3-1. 파일명은 `BulletTimeController.cs` — 고정

`BulletTimeController.cs`는 `Prefabs/CombatManager.prefab` ·
`Prefabs/Player/BattleInput.prefab` 둘 다 물고 있다. 프리팹을 다시 만들어도
이 컴포넌트는 계속 프리팹에 붙으므로 **파일명이 고정된다.**

이름을 바꾸면 클래스명과 어긋나 MonoScript가 사라지고 `Missing (Mono Script)`가
된다. **내용만 흡수한다** — 파일이 살아 있으면 guid도 산다.

### 3-2. `BulletTimeController.cs`에 삽입

현재 꼬리:

```csharp
            return removed;
        }
    }      ← class BulletTimeController 끝
}          ← namespace Prototype 끝
```

`class`의 닫는 `}` **다음**, `namespace`의 닫는 `}` **앞**에 넣는다.
각 원본에서 `using` 줄 · `namespace Prototype {` · 마지막 `}`는 빼고 본문만.

삽입 순서는 의존 순:

1. `TacticPhase.cs` — `enum TacticPhase` + `abstract class TacticState` (47줄)
2. `TacticStateMachine.cs` — `class TacticStateMachine` (68줄)
3. `TacticStates.cs` — `RealTimeState` · `FreezeState` · `OrderState` ·
   `ResolveState` (전부 `TacticState` 상속, 124줄)

### 3-3. `Tactic/` 폴더 통째 삭제

7개를 지운다 — `.cs` 3개 + `.cs.meta` 3개 + 폴더 `.meta` 1개:

```
Assets/Scripts/Battle/Tactic/TacticPhase.cs
Assets/Scripts/Battle/Tactic/TacticPhase.cs.meta
Assets/Scripts/Battle/Tactic/TacticStateMachine.cs
Assets/Scripts/Battle/Tactic/TacticStateMachine.cs.meta
Assets/Scripts/Battle/Tactic/TacticStates.cs
Assets/Scripts/Battle/Tactic/TacticStates.cs.meta
Assets/Scripts/Battle/Tactic.meta
```

> `.meta`를 남기면 유니티가 "메타 파일에 대응하는 에셋 없음" 경고를 뿌린다.

---

## 4. 결과

```
BulletTimeController.cs   897 → ~1121 줄   (239 - namespace/using 오버헤드 ~15)
Battle/ 파일 수            17 → 14
Tactic/ 폴더               삭제
```

**넘침 규칙 체크** — [Battle_Refactor_Plan.md](Battle_Refactor_Plan.md) 2-3:
1,121 < 1,500 → `Battle/System/`을 아직 만들지 않는다.

---

## 5. 같이 처리하면 좋은 것

`Battle_Refactor_Plan.md` **1단계(`DebugComboHUD` 삭제)** 도 프리팹 무관이라
지금 실행 가능하다. `DebugComboHUD.cs:38-44`가 `TacticPhase`를 쓰는 **마지막
외부 참조**이므로, 둘을 같이 하면 커밋 하나로 끝난다.

순서는 무관 — 3번을 먼저 해도, 둘을 한 번에 해도 된다.

---

## 검증 — 씬 재생해서 눈으로 확인할 것

`Tactic` 직접 테스트는 0개다 (`Assets/Editor/Tests/` 전수 확인 —
`InputMapSwitcherTests`가 스치듯 언급만 한다). 컴파일 통과 후 씬 재생으로 본다.

- [ ] 불릿타임 진입 → `Freeze` → 카드 배치 `Order` → 해제 → `Resolve` → `RealTime` 복귀
- [ ] 태그 교대가 `RealTime`에서만 되는가 (`TagSwapController:137`)
- [ ] `Resolve` → `RealTime` 전이 시 태그 후처리가 도는가 (`TagSwapController:363`)
- [ ] `BattleCommander` 불릿타임 키가 먹는가 (`BattleCommander:41`)
- [ ] 프리팹 인스펙터에서 `BulletTimeController`가 `Missing (Mono Script)`가 아닌가
      — 파일명 유지(3-1) 확인
