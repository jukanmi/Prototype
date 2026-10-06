# StateMachine & Stats 리팩토링 계획 (캔버스 #15)

작성 2026-09-06 · 대상 `Assets/Scripts/StateMachine/` + `Stats/` — 4 파일 777 줄
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)
전제: **코드량 감소가 목표.** 프리팹·에디터 수작업이 늘어도 상관없다.
프리팹/`.asset`에 붙는 MonoBehaviour·ScriptableObject는 유니티 MonoScript 규칙상
**자기 이름의 파일이 필요하다.**

---

## 0. 현황

| 파일 | 줄 | 종류 | 프리팹 | 조립 |
|---|---:|---|---:|---:|
| StateMachine/States/EntityStates.cs | 544 | `abstract EntityState` + 상태 8종 | 0 | 0 |
| Stats/Stats.cs | 142 | 순수 class | 0 | 0 |
| StateMachine/StateMachine.cs | 72 | 순수 class | 0 | 0 |
| StateMachine/IState.cs | 19 | interface | 0 | 0 |

**프리팹 0 · 조립 0.** 순수 로직.

`EntityStates.cs` 안의 상태 8종(`MoveState` · `JumpState` · `HitState` ·
`AerialHitState` · `DownState` · `GetupState` 등)은 전부 **소비자가 `Entity.cs`
하나**다. 상태머신 설계상 정상이며 죽은 코드가 아니다.

## 1. 병합 — 4 → 2, 폴더 3개 → 1개

현재 폴더가 셋으로 갈려 있다: `StateMachine/` · `StateMachine/States/` · `Stats/`.

```
StateMachine/
├ StateMachine.cs   IState + StateMachine                  19+72 → ~85
└ EntityStates.cs   abstract EntityState + 상태 8종                544
                    (States/ 하위 폴더 해체)

Stats/Stats.cs      → Core/Stats.cs 로 이동, Stats/ 폴더 삭제        142
```

- `IState`(19줄)는 `StateMachine`이 다루는 계약이다. 19줄짜리 독립 파일 유지 이유 없음.
- `States/` 하위 폴더는 파일 1개뿐이다. 해체.

## 2. `Stats/` 폴더 해체 — 파일 1개짜리 폴더

`Stats/Stats.cs` 하나뿐이다. **`Core/`로 옮긴다.**

근거: `Core/Enums.cs`가 `StatType`(소비자 9) enum을 들고 있고, `Stats`는 그
`StatType`을 키로 쓰는 값 묶음이다. 같은 자리가 맞다.

> [Core_Refactor_Plan.md](Core_Refactor_Plan.md) 3절의 "유입" 목록에 추가할 것.
> `Core/`는 최종적으로 `UiKit.cs` · `yg/` 해체분 · `Stats.cs`를 받는다.

## 3. 결과

```
StateMachine/   3 파일 635 줄  →  2 파일 ~629 줄  (폴더 2개 → 1개)
Stats/          1 파일 142 줄  →  Core/ 로 이동, 폴더 삭제
──────────────────────────────────────────────────────
                4 파일 777 줄  →  2 파일 + Core 유입 1개
```

**줄은 안 준다.** 폴더 두 개가 사라지는 게 전부다.

## 검증

- [ ] 상태 전이 전부 — 이동 · 점프 · 피격 · 공중피격 · 다운 · 기상
- [ ] 스탯 값(체력 · 공격력 등)이 인스펙터/데이터에서 정상 반영되는가
- [ ] `.meta` 고아 경고 없음 (`Stats/` · `States/` 폴더 `.meta` 삭제 확인)
