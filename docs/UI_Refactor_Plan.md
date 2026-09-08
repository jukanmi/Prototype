# UI 리팩토링 계획 (캔버스 #2)

작성 2026-09-06 · 대상 `Assets/Scripts/Battle/UI/` — 12 파일 3,867 줄
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)
전제: **코드량 감소가 목표.** 프리팹·에디터 수작업이 늘어도 상관없다.
프리팹/`.asset`에 붙는 MonoBehaviour·ScriptableObject는 유니티 MonoScript 규칙상
**자기 이름의 파일이 필요하다.**

---

> **이 문서의 핵심은 조립 코드 1,106줄(전체의 28.5%)을 프리팹으로 옮기는 것이다.**
> 파일 병합은 부수적이며, 프리팹에 붙는 7개는 파일명이 고정돼 합칠 수 없다.

## 0. 현황

| 파일 | 줄 | 종류 | 프리팹/씬 | **UI 조립** |
|---|---:|---|---|---:|
| ComboBoardUI.cs | 1,227 | MonoBehaviour | `CombatManager.prefab` | **250** |
| DeckInspectorUI.cs | 569 | MonoBehaviour | `CombatManager.prefab` | **276** |
| RebindUI.cs | 534 | MonoBehaviour | `Boot.unity` (씬 전용) | **225** |
| SkillCutinUI.cs | 326 | MonoBehaviour | `CombatManager.prefab` | 71 |
| PartyHealthHUD.cs | 285 | MonoBehaviour | `BattleInput.prefab` | 110 |
| ComboDamageHUD.cs | 224 | MonoBehaviour | `Boot.unity` (씬 전용) | 47 |
| HandFanLayout.cs | 203 | static | 0 | 0 |
| RecentHitEnemyHUD.cs | 182 | MonoBehaviour | `CombatManager.prefab` | 68 |
| BulletTimeGaugeWidget.cs | 142 | 일반 class | 0 | 27 |
| HandBoardLayout.cs | 64 | static | 0 | 0 |
| UiFactory.cs | 58 | internal static | 0 | 32 |
| CardPoseAnimator.cs | 53 | static | 0 | 0 |
| **합계** | **3,867** | | | **1,106** |

조립 API 호출(`new GameObject` · `AddComponent` · `anchorMin/Max` · `sizeDelta` ·
`anchoredPosition` · `SetParent` · `UiFactory.*`) **293곳**. 전부 프리팹
인스펙터에서 하는 일이다.

---

## 1. 파일명이 고정되는 7개 vs 합칠 수 있는 5개

UI를 프리팹으로 되돌리면 MonoBehaviour 7개가 프리팹에 물린다 → **각자 파일 필요.**

**고정** — `ComboBoardUI` · `DeckInspectorUI` · `RebindUI` · `SkillCutinUI` ·
`PartyHealthHUD` · `ComboDamageHUD` · `RecentHitEnemyHUD`

**합칠 수 있음** (프리팹에 안 붙음) — `HandFanLayout` · `HandBoardLayout` ·
`BulletTimeGaugeWidget` · `CardPoseAnimator` · `UiFactory`

## 2. 중복 3건 (~70줄) — 프리팹화와 독립

먼저 넣을 수 있다.

### 2-1. `EnsureEventSystem` — 4벌

| 위치 | 상태 |
|---|---|
| `yg/SimpleUI.cs:41` | 원본. 이미 4곳이 호출 중 |
| `Battle/UI/ComboBoardUI.cs:489` | 복사본 |
| `Battle/UI/DeckInspectorUI.cs:128` | 복사본 |
| `Battle/UI/RebindUI.cs:481` | 복사본 |

`DeckBuilderUI:132` · `LevelUpSession:115` · `BattleRestartUI:61` ·
`StageResultUI:62`는 이미 `SimpleUI.EnsureEventSystem()`을 부른다.
Battle/UI 3벌만 지우면 된다.

### 2-2. 캔버스 생성 보일러플레이트 — 10곳

`referenceResolution = new Vector2(1920, 1080)` 기준:

```
Battle/UI/ComboBoardUI.cs:512        Battle/UI/RecentHitEnemyHUD.cs:123
Battle/UI/ComboDamageHUD.cs:156      Battle/UI/SkillCutinUI.cs:257
Battle/UI/DeckInspectorUI.cs:152     yg/PartySelectUI.cs:152
Battle/UI/PartyHealthHUD.cs:115      yg/SimpleUI.cs:30          ← 원본
Battle/UI/RebindUI.cs:330            yg/Editor/FlowSceneBuilder.cs:478  ← 빌더, 삭제 예정
```

`SimpleUI.BuildCanvas(host, sortingOrder)`가 **이미 존재하는데** 7개 파일이
같은 10줄을 손으로 다시 쓴다. 차이는 두 가지뿐:

- `matchWidthOrHeight` — SimpleUI 0.5f / ComboDamageHUD·RecentHitEnemyHUD 1f
- `GraphicRaycaster` — SimpleUI는 항상 추가 / 비대화형 HUD는 불필요

기본값 파라미터로 흡수:

```csharp
public static Canvas BuildCanvas(GameObject host, int sortingOrder,
                                 float match = 0.5f, bool raycaster = true)
```

### 2-3. `sortingOrder` 매직넘버 — 13개, 3폴더 산재

```
ComboBoardUI        0     PartySelectUI       50
RecentHitEnemyHUD   2     BattleRestartUI    100
ComboDamageHUD      5     StageResultUI      200
SkillCutinUI       10     RebindUI           200   ← 값 충돌
PartyHealthHUD     15     LevelUpSession     210
DeckInspectorUI    20     DeckBuilderUI      220
                          SceneLoader 페이드  999
```

주석으로 사다리를 손추적 중이다:

- `yg/BattleRestartUI.cs:18` — `"ComboBoardUI 캔버스(0)보다 위, StageResultUI(200)보다 아래"`
- `Progression/LevelUpSession.cs:23` — `"StageResultUI · RebindUI(200)보다 위, SceneLoader 페이드(999)보다 아래"`
- `Battle/UI/ComboDamageHUD.cs:151` — `"RecentHitEnemyHUD(2) 위, SkillCutinUI(10) 아래"`

`UiLayer` const 테이블로 회수하면 이 주석들도 사라진다.

> **미결**: `RebindUI`와 `StageResultUI`가 둘 다 200. 동시에 안 떠서 지금은
> 문제없지만 테이블로 모으면 드러난다. 회수 시 한쪽을 옮길지 정할 것.

---

## 3. 이동 — Battle/UI 밖으로

| 파일 | → | 이유 |
|---|---|---|
| `Battle/UI/UiFactory.cs` | `Core/UiKit.cs` | `yg/PartySelectUI.cs:338`이 쓴다. 전투 전용 아님 |
| `yg/SimpleUI.cs` | `Core/UiKit.cs` | UiFactory와 목적 동일 |
| `yg/BattleRestartUI.cs` | `Battle/UI/` | 전투 UI 맞음 ([yg](yg_Refactor_Plan.md) 소관) |

> `yg/UIManager.cs`는 `Battle/UI/`가 아니라 **`Core/`로 간다.** 내용이 오디오
> 슬라이더 3개 + 설정 패널 토글이라 전투 UI가 아니다.
> [yg_Refactor_Plan.md](yg_Refactor_Plan.md) 3절.

---

## 4. 단계

각 단계 = 별도 커밋.

### 1단계 — `Core/UiKit.cs` 통합 (-70줄)

- `Battle/UI/UiFactory.cs` + `yg/SimpleUI.cs` 통합
- `UiLayer` const 테이블 신설, sortingOrder 13개 회수
- `BuildCanvas`에 `match` · `raycaster` 기본값 파라미터 추가
- 캔버스 생성 10곳 교체, `EnsureEventSystem` 복사본 3벌 삭제
- 사다리 주석 3건 제거

**프리팹 무관.** 지금 바로 가능하다.

### 2단계 — `HandLayout.cs` 병합 (0줄)

`HandFanLayout` + `HandBoardLayout` + `BulletTimeGaugeWidget` +
`CardPoseAnimator` → `HandLayout.cs`.

**근거** — `HandBoardLayout`의 거의 모든 멤버가 `HandFanLayout`을 부르고
(`RowCenterY`·`RowSize`·`PanelHeight`), `GaugeCenterY`가
`BulletTimeGaugeWidget.Height`를, `BulletTimeGaugeWidget:105`가
`CardPoseAnimator.Step`을 부른다. 서로 못 떨어진다. 넷 다 프리팹 참조 0.

**프리팹 무관.** 1단계와 순서 무관.

### 3단계 — UI 프리팹화 (-1,036줄) ← 핵심

큰 것부터. **각 파일이 별도 커밋**, 하나 끝날 때마다 씬 재생 확인.

| 순 | 파일 | 회수 | 화면 |
|---:|---|---:|---|
| 1 | DeckInspectorUI | 276 | 덱 조회 팝업 (F1) |
| 2 | ComboBoardUI | 250 | 손패 조작 판 |
| 3 | RebindUI | 225 | 키 설정 모달 |
| 4 | PartyHealthHUD | 110 | 아군 체력 HUD |
| 5 | SkillCutinUI | 71 | 스킬 컷인 |
| 6 | RecentHitEnemyHUD | 68 | 최근 타격 적 체력바 |
| 7 | ComboDamageHUD | 47 | 콤보 누적 대미지 |

(`UiFactory` 32 + `BulletTimeGaugeWidget` 27 = 59줄은 1·2단계에 포함)

**선행 — `CombatManager` 분할이 필요하다**

UI 4개(`ComboBoardUI` · `DeckInspectorUI` · `SkillCutinUI` · `RecentHitEnemyHUD`)가
지금은 `CombatManager.prefab`의 **GameObject 하나에 전부 얹혀 있다.**
각자 코드로 캔버스를 세우니 가능했던 구조다.

프리팹으로 되돌리면 UI마다 자기 캔버스 계층이 필요하므로
**`CombatManager` 아래 자식 오브젝트 4개로 쪼개야 한다:**

```
CombatManager                    [BulletTimeController, ComboExecutor, TargetSelector]
├─ ComboBoardCanvas              [ComboBoardUI]      + 캔버스 계층
├─ DeckInspectorCanvas           [DeckInspectorUI]   + 캔버스 계층
├─ SkillCutinCanvas              [SkillCutinUI]      + 캔버스 계층
└─ RecentHitEnemyCanvas          [RecentHitEnemyUI]  + 캔버스 계층
```

`CombatManager.prefab`은 `BattleInput.prefab`에 **중첩**돼 있으므로
분할은 `CombatManager.prefab` 안에서 한다 — 부모는 안 건드린다.
([Prefab_Snapshot.md](Prefab_Snapshot.md) 1절)

`RebindUI` · `ComboDamageHUD`는 `Boot.unity` 씬 직접 배치라 프리팹 분할과 무관하다.

**작업 방식** — 파일마다:

1. 에디터에서 UI 계층을 프리팹으로 짓는다 (`Build*`가 만들던 구조 그대로)
2. `[SerializeField]`로 참조를 받도록 필드 추가
3. `Build*` 메서드 삭제
4. 씬/프리팹에 새 프리팹 물리기
5. 씬 재생 확인

> **파일명은 절대 바꾸지 않는다.** 프리팹이 guid로 문다.

> 인스펙터 튜닝값은 [Prefab_Snapshot.md](Prefab_Snapshot.md) 6절에 떠 두었다.
> `SkillCutinUI`는 타이밍 값만 8개다 — 재작성 시 반드시 대조할 것.

---

## 5. 결과

```
Battle/UI/   12 파일 3,867 줄  →  8 파일 2,735 줄
  조립 회수                                          -1,106
  UiFactory.cs → Core/UiKit.cs 이동                     -26
```

**파일별 프리팹화 후 줄 수**

```
ComboBoardUI.cs      1,227 - 250 =  977   (프리팹 고정)
DeckInspectorUI.cs     569 - 276 =  293   (프리팹 고정)
RebindUI.cs            534 - 225 =  309   (프리팹 고정)
SkillCutinUI.cs        326 -  71 =  255   (프리팹 고정)
PartyHealthHUD.cs      285 - 110 =  175   (프리팹 고정)
ComboDamageHUD.cs      224 -  47 =  177   (프리팹 고정)
RecentHitEnemyHUD.cs   182 -  68 =  114   (프리팹 고정)
HandLayout.cs          462 -  27 =  435   (신설, 프리팹 0)
                                  ─────
                                   2,735   8 파일
```

그 외:

```
Core/UiKit.cs 신설 (UiFactory + SimpleUI + UiLayer)      ~230
sortingOrder      13곳 산재 → 1곳
EnsureEventSystem  4벌 → 1벌
캔버스 보일러플레이트 10곳 → 1곳
```

> `Progression`(309) · `yg`(377)의 UI 조립까지 합치면 프로젝트 전체
> **1,792줄**이다. [Refactor_Master_Plan.md](Refactor_Master_Plan.md) 3절.

---

## 검증 — 씬 재생해서 눈으로 확인할 것

**1단계 후 (`UiKit` 통합)**
- [ ] UI 겹침 순서가 이전과 같은가 — 콤보 숫자가 컷인에 가려지는가
- [ ] 덱 인스펙터(F1) · 리바인드에서 마우스 클릭이 먹는가 (`GraphicRaycaster`)
- [ ] 배틀 씬 단독 Play 시 UI 입력 (`EnsureEventSystem` 통합)
- [ ] 해상도 변경 시 손패 판 위치 (`matchWidthOrHeight` 0.5/1 구분)

**2단계 후 (`HandLayout` 병합)**
- [ ] 손패 부채꼴 배치 · 게이지 위치 · 카드 드래그 스왑

**3단계 후 (프리팹화 — 파일마다)**
- [ ] 해당 화면이 이전과 **픽셀 단위로** 같은가
- [ ] `[SerializeField]` 배선 누락으로 `NullReferenceException`이 안 나는가
- [ ] 프리팹 인스펙터에서 `Missing (Mono Script)`가 아닌가
- [ ] Boot 씬 경유 / 배틀 씬 단독 Play 양쪽
