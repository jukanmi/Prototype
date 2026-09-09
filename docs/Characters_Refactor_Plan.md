# Characters 리팩토링 계획 (캔버스 #8)

작성 2026-09-06 · 대상 `Assets/Scripts/Characters/` — 4 파일 510 줄
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)
전제: **코드량 감소가 목표.** 프리팹·에디터 수작업이 늘어도 상관없다.
프리팹/`.asset`에 붙는 MonoBehaviour·ScriptableObject는 유니티 MonoScript 규칙상
**자기 이름의 파일이 필요하다.**

---

## 0. 현황

| 파일 | 줄 | 종류 | 프리팹 | 상속 |
|---|---:|---|---:|---|
| Ally.cs | 251 | MonoBehaviour | **6** | `: Entity` |
| Player.cs | 105 | MonoBehaviour | **1** | `: Entity` |
| Enemy.cs | 102 | MonoBehaviour | **6** | `: Entity` |
| EnemyData.cs | 52 | **ScriptableObject** | 0 | — |

## 1. 손댈 게 없다

**4개 전부 파일명이 고정된다:**

- `Ally` · `Player` · `Enemy` — 캐릭터 프리팹 13곳에 직접 붙는다
- `EnemyData` — ScriptableObject, `.asset`이 타입을 문다

**병합 가능한 파일 0개. 조립 코드 0줄.**

## 2. #4에서 넘어온 질문 — `Entity.cs`를 여기로 옮길까

[Entities_Refactor_Plan.md](Entities_Refactor_Plan.md) 7절이 넘긴 것:

> `Entity.cs`(626줄, 프리팹 참조 **0**) — 합칠 이웃이 `Entities/` 안에 없다.
> `Player` · `Enemy` · `Ally`가 상속하므로 #8을 볼 때 같이 판단한다.

**결론: 옮기지 않는다.**

| | 이유 |
|---|---|
| 합칠 대상이 없다 | `Ally`·`Player`·`Enemy` 셋 다 프리팹 고정이라 `Entity`를 흡수 못 한다 |
| 이동만 하면 손해 | `Entity`는 `Physics`·`Combat`과 `[RequireComponent]`로 묶여 있다. `Entities/`가 그 이웃들의 집이다 |
| 줄 감소 0 | 폴더 이름만 달라진다 |

`Entity.cs`는 `Entities/`에 그대로 둔다.

## 3. 결과

```
Characters/   4 파일 510 줄  →  4 파일 510 줄   (변동 없음)
```

**이 그룹은 작업 대상이 아니다.** "확인했고 할 게 없다"는 기록으로 남긴다.

## 검증

작업이 없으므로 검증도 없다.
