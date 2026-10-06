# yg 리팩토링 계획 (캔버스 #14)

작성 2026-09-06 · 대상 `Assets/Scripts/yg/` + `yg/Editor/` — 14 파일 2,836 줄
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)
전제: **코드량 감소가 목표.** 프리팹·에디터 수작업이 늘어도 상관없다.
프리팹/`.asset`에 붙는 MonoBehaviour·ScriptableObject는 유니티 MonoScript 규칙상
**자기 이름의 파일이 필요하다.**

---

> ## 이 그룹은 **해체**된다
>
> `yg/`는 사람 이니셜 폴더다. 파일을 도메인별로 흩어 보내고 폴더를 없앤다.
> 동시에 **UI 조립 377줄**을 프리팹으로 회수한다.

## 0. 현황

| 파일 | 줄 | 종류 | 프리팹 | **UI 조립** | 행선지 |
|---|---:|---|---:|---:|---|
| Editor/FlowSceneBuilder.cs | 526 | 에디터 빌더 | 0 | 205 | **삭제** (빌더 계획) |
| PartySelectUI.cs | 515 | MonoBehaviour | 0 | **226** | `Party/` |
| BattleSceneController.cs | 375 | MonoBehaviour | **1** | 0 | `Battle/` |
| GameManager.cs | 277 | MonoBehaviour | **1** | 0 | `Core/` |
| StageResultUI.cs | 215 | MonoBehaviour | 0 | **45** | `Stage/` |
| BattleRestartUI.cs | 168 | MonoBehaviour | 0 | **55** | `Battle/UI/` |
| AudioManager.cs | 155 | MonoBehaviour | **1** | 0 | `Core/` |
| SceneLoader.cs | 124 | MonoBehaviour | **1** | 0 | `Core/` |
| SimpleUI.cs | 116 | 순수 static | 0 | **51** | `Core/UiKit.cs` 흡수 |
| UIManager.cs | 116 | MonoBehaviour | **1** | 0 | `Core/` |
| MainMenuController.cs | 90 | MonoBehaviour | **1** | 0 | `Core/` |
| StageOutcomeRules.cs | 68 | 순수 static | 0 | 0 | `Stage/` |
| SceneNames.cs | 66 | 순수 static | 0 | 0 | `Core/` |
| BootStrapper.cs | 25 | MonoBehaviour | **1** | 0 | `Core/` |

**UI 조립 합계 377줄** (FlowSceneBuilder 205는 빌더라 별도 — 통째로 삭제).

## 1. `FlowSceneBuilder.cs` — 빌더 계획에서 이미 삭제 대상

[Builder_Refactor_Plan.md](Builder_Refactor_Plan.md) 3-1의 14개 중 하나다.
526줄 통째로 사라진다. **이 문서에서는 다루지 않는다.**

## 2. UI 조립 377줄 → 프리팹

| 파일 | 회수 | 화면 |
|---|---:|---|
| `PartySelectUI` | **226** | 파티 선택 화면 |
| `BattleRestartUI` | 55 | 전투 재시작 모달 |
| `SimpleUI` | 51 | 공용 조립 헬퍼 → `Core/UiKit.cs`로 흡수 |
| `StageResultUI` | 45 | 스테이지 결과 화면 |

**`PartySelectUI` 226줄이 프로젝트 전체에서 두 번째로 큰 UI 조립 덩어리다**
(1위는 `DeckInspectorUI` 276). 프리팹 배선이 0이라 잃을 인스펙터 값도 없다.

### 부수 효과 — `PartyCatalog`의 `Resources` 우회 제거

`Party/PartyCatalog.cs:10`:

> **왜 Resources 인가.** 파티 선택 화면은 코드로 스스로 지어지므로 (…)
> 씬 배선을 0으로 유지하려면 이 통로가 유일하다.

`PartySelectUI`가 프리팹이 되면 **인스펙터에 파티 에셋을 직접 물릴 수 있다.**
`Resources` 우회가 불필요해진다 — [Party_Refactor_Plan.md](Party_Refactor_Plan.md)
4절과 연결된다.

## 3. 해체 — 파일 13개 행선지

`FlowSceneBuilder` 삭제 후 남는 13개.

```
Core/        GameManager(277) · AudioManager(155) · SceneLoader(124)
             UIManager(116) · MainMenuController(90) · SceneNames(66)
             BootStrapper(25)                                          853
Core/UiKit.cs  ← SimpleUI(116) 흡수 (UI 계획 1단계)
Battle/      BattleSceneController(375)                                375
Battle/UI/   BattleRestartUI(168 → 프리팹화 후 ~113)                    113
Stage/       StageResultUI(215 → ~170) · StageOutcomeRules(68)         238
Party/       PartySelectUI(515 → 프리팹화 후 ~289)                      289
```

> **`UIManager.cs`(116)는 `Core/`로 간다 — `Battle/UI/`가 아니다.**
> 내용이 오디오 슬라이더 3개 + 설정 패널 토글이다. 전투 UI가 아니며
> "중앙 총괄" 역할도 하지 않는다. ([UI_Refactor_Plan.md](UI_Refactor_Plan.md) 3절)

## 4. 네임스페이스 `Prototype.YG` 삭제

현황: `Prototype` 167파일 / `Prototype.YG` 13파일 / `Prototype.YG.EditorTools` 1.
`.asmdef`가 0개라 **네임스페이스는 컴파일에 영향이 없다** — 순수 이름표이고,
프로젝트 관례는 이미 flat `Prototype`이다.

비용:

- `using Prototype.YG;` **6줄** — `Battle/BulletTimeController.cs` ·
  `Party/PartyAssembler.cs` · `Progression/CardOfferView.cs` ·
  `Progression/DeckBuilderUI.cs` · `Progression/LevelUpSession.cs` ·
  `Progression/RunProgression.cs`
- `<see cref="Prototype.YG.*">` **5곳** — 접두어만 제거

기능별 네임스페이스(`Prototype.Battle` 등)를 새로 만드는 안은 **하지 않는다**:
167파일 수정에 단일 어셈블리라 얻는 게 없다. asmdef를 쪼갤 때 다시 판단.

## 5. 단계

### 1단계 — `FlowSceneBuilder` 삭제 (526줄)

[Builder_Refactor_Plan.md](Builder_Refactor_Plan.md) 1단계에 포함. 이 문서는 대기.

### 2단계 — UI 프리팹화 (377줄 회수)

`PartySelectUI`(226) → `BattleRestartUI`(55) → `StageResultUI`(45) 순.
`SimpleUI`(51)는 `Core/UiKit.cs` 통합 때 같이 처리
([UI_Refactor_Plan.md](UI_Refactor_Plan.md) 1단계).

### 3단계 — 파일 이동

3절 표대로. **파일명은 전부 유지한다** — `BattleSceneController` · `GameManager` ·
`AudioManager` · `SceneLoader` · `UIManager` · `MainMenuController` ·
`BootStrapper`가 프리팹/씬에 물려 있다(각 1곳). 폴더만 바뀐다.

### 4단계 — `Prototype.YG` 삭제 + `yg/` 폴더 제거

`using` 6줄 + 주석 5곳. 폴더 `.meta`까지 삭제.

## 6. 결과

```
yg/   14 파일 2,836 줄  →  0 파일 (폴더 삭제)
  FlowSceneBuilder 삭제                    -526
  UI 조립 → 프리팹                          -377
  나머지 13개 → Core/Battle/Stage/Party 이동
────────────────────────────────────────────────
                                     실제 감소 -903
네임스페이스   2개 → 1개
```

## 검증

**2단계 후 (UI 프리팹화)**
- [ ] 파티 선택 화면 — 슬롯 4칸, 직업 색 띠, 선택/해제
- [ ] 전투 재시작 모달이 뜨고 동작하는가
- [ ] 스테이지 결과 화면 (승/패 분기)
- [ ] 겹침 순서 — 재시작(100) < 결과(200) < 레벨업(210)
- [ ] `PartyCatalog`의 `Resources` 경로를 인스펙터 참조로 바꿨다면 파티 목록이 뜨는가

**3·4단계 후 (이동 + 네임스페이스)**
- [ ] 컴파일 통과 (`using Prototype.YG` 6곳 정리 확인)
- [ ] Boot → 메인메뉴 → 파티선택 → 전투 → 결과 전체 흐름
- [ ] 오디오 볼륨 슬라이더 · 설정 패널 (`UIManager`)
- [ ] 프리팹/씬에 물린 7개가 `Missing (Mono Script)`가 아닌가
- [ ] `.meta` 고아 경고 없음 (`yg/` · `yg/Editor/` 폴더 `.meta`)
