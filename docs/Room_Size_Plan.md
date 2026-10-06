# 방 크기 계획 — 지도 칸의 난이도가 방 크기를 정한다

> 상태: **전 단계 완료(1~9).** 9단계는 8단계까지 놓쳤던 출구선을 방에 붙인 사후 수정이다.
> 남은 것은 6항 "눈으로 확인할 것"(사람이 재생해서)과 8항의 열린 질문 둘이다.
> 관련 코드: `Assets/Scripts/Progression/RunMap.cs`(`RunMapGenerator`), `RunMapRecipe.cs`,
> `Assets/Scripts/FlowScene/GameManager.cs`, `RunMapScreen.cs`(`RunMapViewRules`),
> `Assets/Scripts/Stage/StageEncounter.cs`(`EncounterModifier`), `Wave.cs`(`WaveSpawnPlanner`), `StageWaveBoard.cs`, `StageDirector.cs`
> 선행: [Run_Map_Plan.md](Run_Map_Plan.md) (완료) — 이 계획은 그 5항의 이음매("보정 구조체에 칸 하나 더하기")를 쓴다.
> 관련: [Stage_Encounter_Unification_Plan.md](Stage_Encounter_Unification_Plan.md)
>
> **이전 판과 무엇이 달라졌나.** 이전 판은 "씬마다 방 크기를 손으로 정하고, 벽과 숫자가 맞는지 테스트가 본다"였다.
> 이번 판은 **지도를 굴릴 때 칸마다 방 크기 비율이 정해지고, 씬이 그 비율대로 벽을 옮긴다.**
> 스폰 계산이 방을 인자로 받는 기반 작업(이전 판 1 · 2 · 4단계)은 그대로 살아 있다.
> 적 정착 거리는 이전 판의 "벽 기준 고정"을 뒤집어 **방 비율**로 바꿨고, 하한은 마법사 후퇴 거리 때문에 **80%**다(3항 · 2.5).

---

## 0. 지금 지도는 어떻게 굴러가나

계획의 전제라 먼저 정리한다. 전부 코드에서 확인한 것이다.

### 0.1 레시피 애셋 — 손으로 만든 ScriptableObject, 생성 스크립트는 없다

| 애셋 | 만드는 법 | 쓰는 곳 |
| --- | --- | --- |
| `Assets/Data/Map/RunMap_Main.asset` | 메뉴 `Create > Prototype > 런 지도 레시피` 후 인스펙터에서 직접 입력 | Boot 씬 `GameManager.recipe` |
| `Assets/Data/Map/RunMap_Debug.asset` | 같음. 층마다 한 칸짜리(Stage_Mini → SampleScene → Stage_Boss) | 디버그 경로 |

애셋을 **굽는** 에디터 빌더는 없다(`Assets/Editor`에 `RunMapRecipe`를 만드는 코드 없음). 모양은
`RunMapRecipe.floors[]` → `FloorRule { minNodes, maxNodes, candidates[] }` → `NodeCandidate { scene, kind, weight }`이고,
**층 수는 배열 길이**다. 지금 Main 레시피는 5층: `Stage_01` 고정 → `02/03` → `03/04/01정예/이벤트` → `휴식/상점/04정예/03정예` → `Stage_05` 보스.

### 0.2 시드 — `[시작]`을 누르는 순간 한 번

```
GameManager.StartNewRun()
  seed = debugSeed != 0 ? debugSeed : Environment.TickCount
  recipe.TryGenerate(seed, out map, out issues)       // 실패하면 런을 안 연다 (폴백 없음)
  Progress = new RunMapProgress(map)
  로그: "[GameManager] 새 런 — 시드 N · 5층 (RunMap_Main)" + map.Describe()
```

- 지도 씬 단독 실행은 `RunMapScreen.previewSeed`(0이면 `TickCount`)로 같은 생성기를 부른다.
- [처음부터] = `StartNewRun` 다시 = 새 시드. 패배 후 [재시작]은 **지도를 다시 굴리지 않고** 같은 칸(`Pending`)을 다시 연다.

### 0.3 생성 스크립트 — `RunMapGenerator.TryGenerate` (`RunMap.cs`)

`System.Random(seed)` 하나만 쓰는 순수 함수다(`UnityEngine.Random`은 전역이라 안 쓴다).

```
TryGenerate(floors, seed)
├─ RunMapRules.RecipeIssues(floors)      레시피 검사. 걸리면 즉시 실패
├─ rng = new System.Random(seed)
└─ 최대 32번(MaxAttempts) 반복 — rng는 시도끼리 이어서 소비한다
   └─ Roll
      ├─ ① 칸 수   층마다 rng.Next(minNodes, maxNodes+1)
      ├─ ② 간선    층 사이마다 Connect(n1, n2, rng)
      ├─ ③ 내용    칸마다 TryPick — 부모 칸 전부를 따라올 수 있는 후보 중 비중대로 (같은 층 중복은 가능하면 피함)
      │            하나라도 못 고르면 null → 다음 시도
      └─ ④ Build   Id = 층 순 · 열 순 통산 번호, Next = 다음 층 칸 Id
   └─ RunMapRules.Issues(지도)  불변식 재검사. 걸리면 생성기 버그로 보고 실패
```

순서가 **칸 수 → 간선 → 내용**인 이유는, 내용이 부모 칸(`CanFollow`: 같은 씬 연속 · 같은 비전투 칸 연속 금지)을 봐야 해서다.

### 0.4 간선 연결 — `Connect(n1, n2, rng)`

위층 `n1`칸을 아래층 `n2`칸에 **교차 없이** 잇는다.

1. **단조 기본선.** 위층 `i`열 → 아래층 `round(i·(n2−1)/(n1−1))`열 (`n1 == 1`이면 가운데 `(n2−1)/2`).
   위층 순서대로 아래층 번호가 줄지 않으므로 교차가 생길 수 없다.
2. **고아 채우기.** 아무도 안 들어오는 아래층 `j`칸은 "기본선이 `j`보다 왼쪽을 가리키는 마지막 위층 칸"에 붙인다.
   그 칸의 오른쪽 이웃은 `j`보다 오른쪽을 가리키므로 역시 교차가 없다.
3. **갈림길.** 칸마다 40%(`ExtraEdgeChance`)로 좌/우 이웃 열에 간선 하나 더. 이것만 `CrossesAny`로 교차를 직접 검사하고, 교차하면 버린다.
   **난수 소비 횟수를 칸마다 같게 둔다** — 조건에 따라 건너뛰면 뒤 칸의 결과가 밀린다.

교차 판정은 `RunMapRules.Crosses(a, b, c, d) = (a<c && b>d) || (a>c && b<d)` (열 번호끼리).

### 0.5 난이도 — 지금은 칸 종류 하나뿐

- `GameManager.CurrentNodeModifier` → `EncounterModifierRules.For(kind)` → 정예면 `EliteEvery = 3`, 나머지 `None`.
- `StageDirector.Awake`가 한 번 읽고, 소환 예약 세 군데(벽 · 방 · 증원)에서 `IsElite`로 강화 여부만 뒤집는다. **애셋은 안 바뀐다.**
- 층 번호는 **골드 보상**(`GoldRules.StageClearReward(floor, kind)`)에만 쓰인다. 전투 수치에 층 비례는 아직 없다.

---

## 1. 무엇을 바꾸는가

한 줄로: **방 크기 비율은 지도 칸의 속성이다. 레시피가 층마다 범위를 정하고, 생성기가 칸마다 굴리고, 씬은 받은 비율대로 벽을 옮긴다.**

```
RunMapRecipe.floors[f].roomPercent (min~max)          ← 난이도 곡선 = 저작 데이터
      │  RunMapGenerator: 칸마다 전용 난수로 굴림
      ▼
MapNode.RoomPercent (예: 85)                          ← 지도에 박힘. 재시작해도 같은 값
      │  GameManager.CurrentNodeModifier
      ▼
EncounterModifier.RoomPercent                          ← 이미 있는 보정 통로에 칸 하나
      │  StageRoom.Awake (실행 순서 -300)
      ▼
StageRoom.Room = 기준 방 × 85%  → 벽 · 바닥 · 지점 옮김  ← 씬에서 방 크기의 유일한 주인
      │
      ├─ StageWaveBoard  지점 검사 · 기즈모
      └─ StageDirector   WaveSpawnPlanner.PlanAuto/PlanAt(…, room)
```

| 있는 것 | 지금 | 이번 계획에서 |
| --- | --- | --- |
| `FloorRule` | 칸 수 · 후보 | **`roomPercentMin/Max` 추가** |
| `MapNode` | Id · 층 · 열 · 종류 · 씬 · Next | **`RoomPercent` 추가** |
| `RunMapGenerator` | 칸 수 → 간선 → 내용 | **⑤ 방 크기** — 지도 모양용 난수를 건드리지 않는 전용 난수 |
| `EncounterModifier` | `EliteEvery` | **`RoomPercent` 추가** |
| `WaveSpawnPlanner` | `const RoomHalfX = 6`, `RoomHalfZ = 3` | **방을 인자로 받는다. 상수 삭제** |
| (없음) | 벽 · 바닥은 씬에 고정 배치 | **`StageRoom`** — 기준 방을 들고, 비율대로 벽 · 바닥 · 지점을 옮긴다 |
| `RunMapViewRules.Describe` | 종류별 설명 | **방 크기 한 마디 추가** |

---

## 2. 확정할 결정 여섯

### 2.1 방 크기는 굴린 **지도에 박는다** — 씬에 들어갈 때 굴리지 않는다

씬 로드 시점에 굴리면 세 가지가 깨진다.

- **재시작이 다른 방이 된다.** 패배 후 [재시작]은 같은 칸을 다시 연다(0.2). 방이 달라지면 "같은 판을 다시"가 아니다.
  칸에 박아 두면 `Pending`을 다시 여는 기존 방식 그대로 같은 방이 나온다 — 되돌리는 코드가 필요 없다.
- **고르기 전에 못 보여 준다.** 지도 화면이 "좁은 방"을 알려 주려면 값이 이미 있어야 한다.
- **재현이 안 된다.** 지금은 로그의 시드 하나로 지도 전체가 재현된다. 방 크기도 그 시드에서 나와야 한다.

### 2.2 방 크기용 난수는 **따로** 쓴다 — 지도 모양이 한 칸도 안 바뀌어야 한다

생성기의 `rng`에서 방 크기를 뽑으면 그 뒤 모든 난수가 밀린다. 결과는 두 가지다.

1. 이 기능을 넣는 순간 **같은 시드가 다른 지도**가 된다 — 지금까지 로그로 남긴 시드가 전부 무효.
2. 레시피에서 **방 범위만 고쳐도 지도 모양이 바뀐다.** 난이도를 조정하려다 갈림길 구조가 흔들린다.

→ `④ Build`에서 칸이 확정된 뒤, 칸마다 `new System.Random(RoomSeed(seed, nodeId))`로 따로 굴린다.
`RoomSeed`는 `unchecked(seed * 486187739 + nodeId * 16777619)` 같은 섞기. **지도 모양 난수 소비 순서는 그대로다.**
테스트가 이걸 못 박는다: "방 범위만 다른 두 레시피, 같은 시드 → `Describe()`가 같다".

`Describe()` 문자열에는 방 크기를 **넣지 않는다.** 그 문자열은 "모양이 같은가"를 비교하는 용도이고,
`RunMapGeneratorTests`에 리터럴 비교(`"0[Battle:A>1] / …"`)가 있다. 방 크기는 `StartNewRun` 로그에 줄을 따로 찍는다.

### 2.3 단위는 **정수 백분율, 5% 단위** — 0은 "보정 없음"

```csharp
public struct FloorRule { …; [Range(0,200)] public int roomPercentMin, roomPercentMax; }   // 둘 다 0이면 100 고정
public sealed class MapNode { …; public int RoomPercent { get; } }                          // 항상 80~125, 비전투·보스는 100
public readonly struct EncounterModifier { public readonly int EliteEvery; public readonly int RoomPercent; }  // 0이면 100
```

- **정수인 이유:** 부동소수 비교가 로그 · 테스트 · 인스펙터에서 흔들린다. 골드 배율(`EliteClearRewardPercent`)과 같은 규약이다.
- **0 = 보정 없음인 이유:** `EncounterModifier.None = default`라 새 칸의 기본값이 0이다. `EliteEvery`도 0이 "없음"이다.
  같은 이유로 **기존 레시피 애셋은 필드가 없어 0으로 읽히고, 그대로 100% 고정**이 된다 — 애셋 마이그레이션이 필요 없다.
- **5% 단위인 이유:** 97% 방과 100% 방은 눈으로 구분이 안 되고, 로그만 지저분해진다.
  굴림은 `rng.Next(min/5, max/5 + 1) * 5`.
- **X · Z에 같은 비율**을 건다. 축마다 따로 두면 저작 칸이 넷이 되는데, 아래 2.5의 한계를 계산해 보면
  한 비율로 **80~125%가 두 축 모두에 들어맞는다.** 따로 둘 이유가 없다.

### 2.4 보정은 **자리 없는 전투 칸에만** 걸린다 — 보스 · 비전투 · 아레나는 100%

| 칸 | 방 비율 | 이유 |
| --- | --- | --- |
| 전투 · 정예 | 레시피 범위에서 굴림 | 이 계획의 대상 |
| 보스 | **100 고정** | 보스 패턴은 방 크기를 전제로 저작한다. 정예 강화를 보스에 안 거는 것(`IsElite`)과 같은 이유 |
| 휴식 · 상점 · 이벤트 | 100 고정 | 방이 없다 |

**아레나 스테이지(Stage_02 · 05)는 이번 범위가 아니다.** 구간 · 통로 · 문이 X축으로 이어져 있어서 아레나 폭을 바꾸면
뒤에 오는 기하가 전부 밀린다. 그래서:

- 레시피 검사가 "범위가 100이 아닌 층의 **전투 후보 씬은 전부 `StageRoom`을 가진다**"를 본다(에디터 테스트, 8단계).
- 지금 Main 레시피 1층에 `Stage_02`가 있으므로 **1층은 100 고정**이어야 한다. 2 · 3층 전투 후보는 전부 웨이브 방(01 · 03 · 04)이다.
- 런타임에 `StageRoom` 없는 씬이 100이 아닌 값을 받으면 **에러 로그**를 남기고 저작 크기로 돈다.
  테스트가 막는 경로라 정상적으로는 안 온다. 조용히 넘기지 않는다.

### 2.5 한계는 **규칙이 정하고 레시피 검사가 막는다** — 80~125%

기준 방(반폭 6 · 반깊이 3)에 비율을 걸었을 때 버텨야 하는 조건이다.
**하한을 정하는 것은 스폰 계산이 아니라 적 AI 수치다** — 3항에서 정착 거리를 비례로 바꾸면 스폰 계산 쪽 한계는 거의 사라지고,
방이 줄어도 안 줄어드는 월드 단위 값(3항 ③)이 먼저 걸린다.

| 방향 | 조건 | 깨지면 | 한계 |
| --- | --- | --- | --- |
| 작게 X·Z | 마법사가 정착한 구석에서 중앙 플레이어까지 거리 ≥ `Enemy_Ranged.preferredMinRange`(4) | 나오자마자 "너무 가깝다"로 물러나려는데 뒤가 벽이라 **벽에 붙어 떤다** | 80%에서 4.24 ✓ · 75%에서 3.96 ✗ → **80%** |
| 작게 Z | 반깊이 − 0.6 ≥ 마법사 줄 간격 1.5 | 마법사 줄 탐색이 전부 실패한다 | 반깊이 2.1 = 70% |
| 작게 X | 비례 정착 거리(마법사: 반폭 × 0.2) > 등장 거리 0.6 | 걸어 들어오는 모션 없이 벽 앞에 그대로 선다 | 반폭 3.0 = 50% |
| 크게 X | 벽이 고정 카메라 화면 안 (직교 5 · 16:9 → 반폭 8.9, 벽 두께 빼고) | 옆벽이 화면 밖 | 반폭 8.4 = 140% |
| 크게 Z | 뒷벽(높이 4)이 화면 안 (기울기 50° · lift 0.5: `0.766·z + 0.643·4 − 0.5 ≤ 5`) | 뒷벽 윗부분이 잘린다 | 반깊이 3.8 = **127%** |

마법사 거리 계산: 정착 X = 반폭 × (1 − 0.2), 정착 Z = 반깊이 − 0.6.
80% → (3.84, 1.8) → 4.24. 75% → (3.6, 1.65) → 3.96. 100% → (4.8, 2.4) → 5.37.
플레이어가 중앙이 아니면 당연히 더 가까울 수 있다 — 그건 정상적인 물러남이다. 막으려는 것은 **등장 순간 기본 배치부터** 반경 안에 서는 경우다.

→ **`RoomRules.MinPercent = 80`, `MaxPercent = 125`.** 마법사 거리와 카메라 쪽 숫자는 계산 추정이라 4단계 씬 작업 때 눈으로 확인하고 필요하면 조인다.
레시피 검사에 `RunMapProblem.BadRoomRange`(범위 밖 · min > max · 한쪽만 0)를 더한다.

**하한이 애셋 값에 기대므로 테스트가 둘을 묶는다**(2단계): `Enemy_Ranged.asset`의 `preferredMinRange`를 읽어
`MinPercent` 방에서 마법사 기본 배치 거리가 그 값 이상인지 본다. 누가 마법사 후퇴 거리를 5로 올리면 이 테스트가 하한을 올리라고 알려 준다.

**카메라는 방을 따라 줌하지 않는다.** 직교 크기를 방에 맞춰 바꾸면 방이 작아질 때 캐릭터가 커지고 커질 때 작아져서,
난이도가 아니라 **가독성**이 흔들린다. 고정 구도는 그대로 두고 방만 화면 안에서 줄었다 늘었다 한다.

### 2.6 난이도 방향은 **데이터다** — 코드에 "좁을수록 어렵다"를 박지 않는다

방이 작아지면 어려워지는 것과 쉬워지는 것이 섞여 있다.

| 좁아지면 | 효과 |
| --- | --- |
| 돌진전사(X축 직선 돌진)를 **깊이 이동으로 피할 줄**이 줄어든다 | 어려워짐 |
| 벽에서 정착 지점까지 **걸어오는 시간**이 짧아져 준비 시간이 준다 | 어려워짐 |
| 전사 여럿이 **덜 퍼져** 한꺼번에 몰린다 | 어려워짐 |
| 구석의 마법사까지 **거리가 짧아진다** | 쉬워짐 |
| 밀치기로 **벽꽝(WallBound)** 내기가 쉬워진다 | 쉬워짐 |

규칙 코드는 "범위 안에서 굴린다"만 알고, 층마다 범위를 어떻게 줄지는 레시피가 정한다.
**제안값**(8단계 · 6항 "정할 것"에서 확정)은 "뒤층일수록 좁아지고, 흔들림이 커진다"이다.

| 층 | 지금 전투 후보 | 제안 범위 |
| --- | --- | --- |
| 0 | Stage_01 | 100 (입문 고정) |
| 1 | Stage_02(아레나) · 03 | **100 고정** — 2.4 |
| 2 | 03 · 04 · 01정예 | 90 ~ 110 |
| 3 | 04정예 · 03정예 | 80 ~ 100 |
| 4 | Stage_05 보스 | 100 — 2.4 |

---

## 3. 방이 줄면 무엇이 따라 움직이나 — 거리의 뜻으로 셋으로 가른다

"방이 80%면 등장 위치도 80%"로 **전부** 비례시키지 않는다. 거리마다 무엇을 재는 값인지가 다르고, 그 뜻에 따라 방을 따라가는 방식이 갈린다.

| 종류 | 무엇을 재나 | 해당하는 값 | 방이 바뀌면 |
| --- | --- | --- | --- |
| **① 벽에 붙는 자리** | 벽에서 몸통 하나만큼 | 등장 지점 `SpawnInset 0.6` · 벽 예고 지점 · 아레나 진입선 `EntryLineInset 0.4` · 출구선 `ExitInset 1.0`(9단계) | **벽을 따라 옮겨지고 거리는 고정** |
| **② 방 안 배치** | 방 안으로 얼마나 깊이 들어오나 | 정착 거리 전사 2.8 · 돌진전사 1.8 · 마법사 1.2 → **반폭의 비율** / 씬 스폰 지점 · 파티 시작 자리 | **비율대로** |
| **③ 몸 · 사거리** | 두 몸 사이 · 공격이 닿는 거리 | 깊이 줄 간격 1.6 · 2.4 / 돌진전사 줄 1.2 / 마법사 줄 회피 1.5 / 몸통 여유 `DepthInset 0.6` / AI `attackRange` · `preferredMinRange` · `specialRange` / 넉백 / 예고 표식 크기 | **고정** |

### 3.1 ② 정착 거리를 비례로 — 이전 판의 결정을 뒤집는다

이전 판은 정착 거리를 "벽에서 2.8" 고정으로 뒀다. 좌우에서 온 전사 두 무리 사이 간격을 보면 문제가 드러난다.

| 방 | 고정(이전 판) | 비례(이번 판) |
| --- | --- | --- |
| 80% | 4.0 | 5.1 |
| 100% | 6.4 | 6.4 |
| 125% | 9.4 | 8.0 |

고정이면 좁은 방에서 전사가 **나오자마자 플레이어를 양쪽에서 낀다.** 이건 "방이 좁아서 피할 자리가 적다"가 아니라
**진형이 바뀐 것**이다. 난이도 곡선을 레시피 범위 하나로 조정하려는데 진형까지 같이 흔들리면 무엇이 어렵게 만드는지 안 읽힌다.

→ `WaveSpawnPlanner`의 `MeleeInset · ChargerInset · RangedInset`을 **반폭 대비 비율 상수**로 바꾼다
(`2.8/6 ≈ 0.467`, `1.8/6 = 0.3`, `1.2/6 = 0.2`). 기준 반폭 6에서 곱하면 원래 값이라 **100% 방의 답은 한 자리도 안 바뀐다.**
덤으로 이전 판 최소 크기 조건 "반폭 > 2.8"이 필요 없어진다(비율이면 중심을 넘을 수 없다).

씬 스폰 지점과 파티 시작 자리는 이미 `StageRoom.anchors`로 중심 기준 비례 이동한다(4항) — 같은 종류라 같은 규칙이다.
벽 바로 앞에 찍은 굴 지점은 엄밀히는 ①에 가깝지만, 80~125% 안에서 벽과의 거리 차이가 0.2 안쪽이라 ②로 둔다.

### 3.2 ③ 깊이 줄 간격은 고정, 방 밖 줄은 건너뛴다

X축 정착 거리는 "방 안 얼마나 깊이"라서 ②지만, Z축 줄 간격은 **"몸 두 개가 얼마나 떨어져 서나"**라서 ③이다.
비슷해 보여도 재는 것이 다르다. 간격을 줄이면 전사끼리 몸이 겹치고, 마법사 줄 회피 1.5를 줄이면 옆걸음질만으로 닿아
"축을 옮겨 잡으러 간다"는 마법사의 존재 이유가 사라진다(`RangedDepth` 주석).

대신 고정 간격을 **물리기만 하면 틀린다.** 80% 방은 소환 가능 깊이가 ±1.8이라, 전사 줄 `{0, ±1.6, ±2.4}` 중
±1.6과 ±2.4가 **0.2 차이로 접힌다.**

→ 전사 줄도 **돌진전사 규칙(`ChargerDepth`)과 같이** 방 밖 줄은 건너뛰고 방 안 줄만 순서대로 돌려 쓴다.
돌려 쓴 줄은 `SideSign(Both)`가 좌우를 번갈아 주므로 같은 점에 겹치지 않는다. 100% 방에서는 다섯 줄이 전부 방 안이라 **지금과 같은 답**이다.

### 3.3 ③ AI 수치는 방을 모른다 — 그래서 하한을 정한다

`EnemyBrainParams`의 거리는 월드 단위 고정값이고 방 크기를 모른다. 이건 **옳다** — 사거리와 후퇴 거리는 적의 정체성이자
플레이어가 읽는 신호라서 방마다 달라지면 안 된다. 대신 방이 그 값들을 **담을 수 있어야** 한다.

- **마법사 후퇴 4** → 좁은 방에서 등장하자마자 벽에 몰린다. 하한 80%의 근거(2.5).
- **돌진전사 돌진 7** → 좁은 방에서 벽에 막혀 짧게 끝나는 일이 늘어난다. `HitOrWall` 조기 종료가 이미 있어 **버그가 아니라 좁은 방의 난이도**로 본다.
- **넉백** → 벽꽝(WallBound)이 잦아진다. 2.6 표의 "쉬워지는 요소"다.

---

## 4. `StageRoom` — 씬에서 방 크기의 유일한 주인

```csharp
[DefaultExecutionOrder(-300)]           // PartyAssembler(-200)보다 먼저 — 파티가 옮겨진 스폰 자리에 서야 한다
public class StageRoom : MonoBehaviour
{
    [SerializeField] RoomRect baseRoom = RoomRect.Default;   // 100%일 때. 씬의 벽과 일치해야 한다(테스트)
    [SerializeField] Transform wallLeft, wallRight, wallNear, wallFar;   // 이름 말고 역할로 받는다 — 아래 함정
    [SerializeField] Transform[] stretch;      // 바닥 · 뒷벽 그림 · 테두리 선: 중심 기준 스케일
    [SerializeField] Transform[] anchors;      // 스폰 지점 · 파티 스폰 자리 · 소품: 중심 기준 위치만
    [SerializeField, Range(0,125)] int debugRoomPercent;     // GameManager 없는 단독 실행 전용. 0이면 100

    public RoomRect Room { get; private set; }  // 적용 후 실제 방
    public int Percent { get; private set; }
    public static StageRoom Instance { get; }
}
```

- **계산은 순수 함수**로 뺀다: `RoomRules.Scale(in RoomRect base, int percent)`, `RoomRules.MovePoint(point, base, scaled)`,
  `RoomRules.WallPose(side, scaled, thickness)`. `StageRoom.Awake`는 그 결과를 트랜스폼에 쓰기만 한다.
- **Awake에서 한 번 적용하고 판 내내 안 바꾼다.** `GroundPlate`는 `Renderer.bounds`를 프레임 캐시하므로
  같은 프레임에 누가 먼저 물으면 옛 크기가 남는다 — 실행 순서 -300이 그걸 막는다.
- **`StageWaveBoard`는 크기를 들지 않는다.** `IsInsideRoom` 검사와 방 기즈모가 `StageRoom.Room`을 읽는다.
  이전 판의 "보드에 `room` 필드"는 버린다 — 기준 방과 실제 방이 둘 다 보드에 있으면 무엇을 읽을지 고르는 분기가 생긴다.
- 에디트 모드(기즈모 · 보드 검사)에서는 적용 전이므로 `baseRoom`을 읽는다.

**함정: 벽 이름과 코드의 앞뒤가 반대다.** Stage_01의 `Wall_Front`는 z = −3.25(화면 아래),
`Wall_Back`은 z = +3.25인데 `SpawnWall.Front`는 +Z다. 그래서 필드를 `Front/Back`이 아니라 `Near/Far`로 두고,
배선 테스트는 **위치로** 맞는지 본다.

---

## 5. 단계

1~4단계는 비율이 전부 100이라 **게임 화면이 달라지지 않는다.** 실제로 방이 변하는 것은 8단계에서 레시피 값을 넣을 때다.

### ~~1단계 — `RoomRect` · `RoomRules` (순수)~~ 완료

- `Wave.cs`의 `SpawnEntry`와 `WaveSpawnPlanner` 사이 섹션. `RoomRect(minX, maxX, minZ, maxZ)`(직렬화 가능, 읽을 때 정렬) ·
  `Default`(지금은 `WaveSpawnPlanner.RoomHalfX/Z`에서 만든다 — 2단계에서 상수가 사라지면 숫자가 여기로) · 중심/반경 파생.
- `RoomSide { Left, Right, Near, Far }` — 벽 이름 함정(4항) 때문에 앞/뒤가 아니라 가까움/멂.
- `WallPose { Center, Length, Thickness, AlongX, ColliderSize(height) }`.
- `RoomRules`
  - 비율: `FullPercent 100` · `MinPercent 80` · `MaxPercent 125` · `Step 5` · `Normalize`(0 이하 → 100) ·
    `IsValidPercent`(레시피 검사용: 0 또는 범위 안 5 배수) · `ClampPercent`(런타임용: 반올림 후 범위로)
  - 기하: `Scale` · `MovePoint` · `WallFor(side, room, thickness)`
  - 크기: `IsLargeEnough`(마법사 줄 회피 자리) · `FitsCamera`(`MaxHalfX 8.4` · `MaxHalfZ 3.8`, 추정) · `Fits`
  - **비율 검사와 기하 검사를 나눴다.** 75%는 기하로는 멀쩡하고 마법사 후퇴 거리에서만 걸리는데, 그 조건은 정착 거리가 비율로 바뀌는 2단계에서야 계산할 수 있다.

테스트 `RoomRulesTests`
- `RoomRect` 정렬 · 파생값 · `Default`가 6×3
- `Normalize` / `IsValidPercent`(0 · 80 · 100 · 125 참, 75 · 130 · 83 · −5 거짓) / `ClampPercent` / 비율 상수끼리 결속
- 원점이 아닌 방에서 `Scale` · `MovePoint`가 중심 기준, 높이 유지, 벽 위 점은 벽 위에 남음
- `WallFor`: 기준 방에서 Stage_01 벽 규격(±6.25 · (0.5, 4, 6) / ±3.25 · (13, 4, 0.5))이 그대로 나온다 / 80 · 100 · 125%에서 안쪽 면이 방 변
- `Fits`: 80~125% 기준 방 전부 참, 130%는 카메라에서 거짓, 반깊이 2는 크기에서 거짓, 경계값 통과

### ~~2단계 — `WaveSpawnPlanner`가 방을 받는다~~ 완료

- `PlanAuto` · `PlanAt` · `IsInsideRoom` · `ClampIntoRoom` · `DepthFor` · `ChargerDepth` · `RangedDepth` · `Clamp`에 `in RoomRect room`(마지막 인자).
  **기본값 오버로드는 남기지 않았다** — 하나라도 빠뜨리면 그 경로만 옛 방으로 돈다. 증원(`TickReinforcements`)이 그런 두 번째 호출 자리다.
- `RoomHalfX` · `RoomHalfZ` 상수 삭제. 방 크기 숫자는 `RoomRect.Default` 한 군데에만 남는다.
  `MaxDepth`는 상수 프로퍼티를 지우고 **`MaxDepth(in RoomRect)` 메서드**로 바꿨다(중심 기준 거리 — 보드 기즈모 · 테스트가 쓴다).
- `ArenaSpawnPlanner.ArenaHalfZ`는 자기 상수 `3f`로 떼어 뒀다(아레나는 범위 밖, 주석으로 이유).
- **정착 거리 비례화(3.1).** `MeleeInset · ChargerInset · RangedInset` → `MeleeSettle = 2.8f/6f` 식의 반폭 비율 상수 + `SettleFor(role)`.
  등장 지점 `SpawnInset 0.6`은 벽 기준 고정 그대로(①).
- 전사 줄 건너뛰기(3.2) — `MeleeDepth(index, room)`.
- **계획에 없던 것: 한쪽 등장 전사의 줄 돌려 쓰기.** 방 안 줄이 셋(80%)이면 `SpawnSide.Right`만으로 전사 넷이 나올 때 0번과 3번이 같은 점에 선다.
  `SettleOffset(role, side, index, room)`이 **한쪽 등장 전사만** 바퀴(`MeleeCycle`)마다 몸 하나(`MeleeWrapStep 1.2`)씩 안쪽으로 당긴다.
  - 양쪽 등장(`Both`)은 방 안 줄 수가 항상 홀수(1 · 3 · 5)라 다음 바퀴가 반대편 벽에서 나와 안 겹친다 — 안 당긴다.
    증원이 `Both`로 번호를 계속 올리므로, 당기면 늦은 증원이 플레이어 코앞에 서고 100% 방의 답까지 바뀐다.
  - 100% 방의 0~4번은 그대로다. 바뀌는 것은 한쪽 등장 전사 6기 이상(예전엔 겹쳤다)뿐이고, 지금 웨이브 애셋의 자동 배치 전사는 한 웨이브에 최대 3기다.
  - 남은 한계: 양쪽 등장은 `2 × 방 안 줄 수`번째에 같은 점이 돌아온다(80%: 0번과 6번, 100%: 0번과 10번). 증원은 시간차가 커서 둔다.
- `EntranceRules.FallbackHalfWidth` → `RoomRect.Default.HalfX + 2f` (`static readonly`).
- 런타임 호출자는 아직 기준 방을 넘긴다: `StageDirector.Room` · `StageWaveBoard.Room` 프로퍼티 한 곳씩(3단계에서 `StageRoom`으로 교체).

테스트
- 기존 `WaveSpawnPlannerTests` · `WaveLayoutTests` · `SpawnPlacementTests` · `SpawnRouteRulesTests`: `RoomRect.Default`를 넘기도록 기계적 수정.
  **기대값 불변** — 정착 비율이 기준 반폭에서 원래 값과 같다는 것이 이 불변으로 확인된다.
- 새로 (`WaveSpawnPlannerTests` "방 크기" 절)
  - 80% 방에서 전사 5기가 줄 위(0 · ±1.6)에만 서고 (x, z)가 전부 다르다 / 80 · 125% 방에서 모든 역할의 spawn · entry가 방 안이다
  - 80% · 125% 방에서 **등장 지점의 벽까지 거리는 0.6 그대로**(①), **정착 지점의 반폭 대비 위치는 100%와 같다**(②), **깊이 줄 간격은 1.6 그대로**(③)
  - 100% 방의 정착 거리가 옛 벽 기준 값 2.8 · 1.8 · 1.2와 같다
  - 한쪽 등장 전사가 줄을 돌려 써도 안 겹치고 중심을 안 넘는다(80% 6기 · 100% 8기) / 양쪽 등장 전사는 12기까지 안 당겨진다
  - 125% 방에서 마법사가 새 구석에 선다 / 중심이 원점이 아닌 방(돌진전사 줄 · 전사 중심 줄 · 마법사 구석 · `IsInsideRoom` · `ClampIntoRoom`)
  - `RangedSettle_AtMinPercent_IsOutsideRetreatRange` — `Enemy_Ranged.asset`을 읽어, `MinPercent` 방 · 플레이어 중앙에서
    마법사 기본 배치 거리 ≥ `preferredMinRange`(2.5). 애셋 값이 바뀌면 하한을 다시 보라고 실패한다.

### ~~3단계 — `StageRoom` 컴포넌트, 보드 · 디렉터가 읽는다~~ 완료

> 3단계만 끝난 상태에서는 Stage_01 · 03 · 04에서 적이 안 나왔다(디렉터가 방 없는 웨이브 방을 거절). 4단계 배선으로 해소됐다.

- **`StageRoom.cs`**(4항대로). 비율은 아직 `debugRoomPercent`만 읽는다(GameManager 연결은 7단계).
  - `Apply(percent)` public — 에디트 모드 테스트가 부른다. 한 번만 적용, 범위 밖은 물리고 경고, 규칙을 못 지키는 방은 경고, **100%면 아무것도 안 건드린다**.
  - 벽: `WallFor` 자리로 옮기고 `BoxCollider.size`를 로컬 스케일로 나눠 맞춘다. 높이(위치 y · 콜라이더 높이)는 그대로.
  - `Find()`(플레이 중 `Instance`, 아니면 씬 전체) · **`FindIn(scene)`**(그 씬 안에서만) · `Configure(...)` 테스트 이음매.
- **계획에 없던 것: `RoomRules.StretchScale(localScale, rotation, percent)`.** "바닥과 나란한 축만" 줄이려면 그림마다 어느 로컬 축이 바닥과 나란한지
  알아야 한다. 이름으로 가르지 않고 **축의 월드 방향이 바닥에 비친 길이**로 가른다 — 눕힌 바닥은 두 축, 세운 뒷벽 그림은 폭만 준다.
- `StageWaveBoard`
  - `NeedsRoom` — 자리 없는 조우나 스폰 지점이 하나라도 있으면 참. 아레나 보드(02 · 05: 자리 있는 조우뿐, 지점 없음)는 거짓.
  - 보드 검사 맨 앞에 방: **`NoStageRoom`**(계획에 없던 것 — 필요한데 없음) · `RoomTooSmall`. 방이 없으면 줄마다 `OutsideRoom`을 또 적지 않는다.
  - 지점 검사 · 기즈모가 `StageRoom.FindIn(gameObject.scene)`의 `Room`을 읽는다. **씬 전체가 아니라 자기 씬** — 스테이지 씬을 열어 둔 채
    테스트를 돌리면, 테스트가 만든 보드가 열린 씬의 방을 자기 방으로 읽기 때문이다. 방이 없으면 기즈모는 방 경계를 안 그리고 지점을 전부 빨강으로 그린다.
- `StageDirector`
  - `Awake`에서 `StageRoom.Find()`. **`board.NeedsRoom`인데 방이 없으면 조우 목록을 통째로 비운다**(보드가 없을 때와 같은 처리).
    계획은 "그 조우만 안 돌린다"였는데, 섞인 스테이지가 없고 보드 없음과 같은 규약이 읽기 쉬워서 바꿨다.
  - `QueueInRoom` · `TickReinforcements`가 `stageRoom.Room`을 넘긴다.
  - 방 없는 씬의 아레나 조우에 증원이 붙어 있으면 에러 한 번 + 증원을 다 쓴 것으로 접는다(안 접으면 조우가 영영 안 끝난다). 지금 증원은 `Wave_4_3`에만 있다.

테스트
- `StageRoomTests`(새 파일) — 적용 전 방 = 기준 방 / 100% · 0%는 아무것도 안 건드림 / 80%에서 벽 자리 · 콜라이더 · 바닥 두 축 · 뒷벽 그림 폭만 ·
  지점 상대 위치 · 높이 유지 / 스케일된 벽의 로컬 콜라이더 / 범위 밖 물림 / 두 번째 적용 무시 / 빠진 벽이 나머지를 안 막음 / 원점 밖 기준 방 /
  실행 순서가 `PartyAssembler`보다 앞(4단계에 있던 항목을 앞당김)
- `RoomRulesTests` — `StretchScale`: 눕힌 바닥 · 세운 그림 · Y 회전 무관 · 기울어진 축 부분 비율 · 100%
- `StageWaveBoardTests` — 테스트마다 **미리보기 씬**에 보드와 기준 방을 세운다(일반 씬 추가는 제목 없는 씬이 열려 있으면 에디터가 거절한다).
  새로: 방 없음 + 방 조우 → `NoStageRoom` 하나 / 방 없음 + 지점 → `NoStageRoom` 하나뿐 / 아레나 보드는 방 없어도 깨끗 / 빈 보드 깨끗 /
  원점 밖 방으로 지점 판정 / 얕은 방 → `RoomTooSmall` / **다른 씬의 방은 이 보드의 방이 아니다**

### ~~4단계 — 웨이브 방 씬에 `StageRoom` 배선~~ 완료 (Unity CLI로 열린 에디터에서)

대상: 디렉터가 도는 웨이브 방 — `Stage_01` · `03` · `04`. (`StageWaveBoard`가 있는 씬은 01~05뿐이고, 02 · 05는 아레나다.
`Stage_Mini` · `Stage_Boss` · `SampleScene`에는 보드가 없어 디렉터 스폰 규칙을 안 탄다 — 디버그 레시피는 전부 100%로 둔다.)

세 씬의 구조가 똑같았다: 루트 `Room`(원점) 아래 `Wall_Left/Right/Front/Back`(Wall 레이어, 회전 없음) · `Floor`(X 90° 눕힘, 12×6) ·
`BackWall`(z 3, 12×4) · `Horizon`(z 3, 12×0.06), 루트에 `PartySpawnPoint`(−3, 0, 0). **세 보드 모두 스폰 지점이 0개다.**

- `StageRoom`을 **새 오브젝트가 아니라 기존 `Room`에** 붙였다 — 벽 · 그림을 이미 자식으로 들고 있고, `Room` 자신은 옮겨지지 않는다.
- 벽은 이름이 아니라 **위치로** 분류해 꽂았고, 꽂기 전에 네 벽이 기준 방의 `WallFor` 자리 · 콜라이더와 0.01 안에서 맞는지 확인했다(세 씬 모두 일치).
- `stretch = [Floor, BackWall, Horizon]`, `anchors = [PartySpawnPoint]`. 꽂은 직후 보드 검사 0건, 저장.
- **함정: 스크립트로 스테이지 씬을 열고 저장하면 `BattleInput` 프리팹 인스턴스에 의도 안 한 오버라이드가 붙었다** —
  `DeckButton` · `DiscardButton`의 앵커(프리팹 0.5 → 0), `CounterBar`의 `sizeDelta`(100 → 0). 실제로 값이 다른 UI 변경이라
  `PrefabUtility.RevertPropertyOverride`로 되돌리고 다시 저장했다. 다시 열어도 안 돌아왔다. 원인은 못 짚었다 —
  **스테이지 씬을 도구로 저장한 뒤에는 `git diff`로 `BattleInput` 오버라이드가 늘지 않았는지 볼 것.**
- 남은 사람 작업: `debugRoomPercent`를 80 · 125로 바꿔 단독 재생해 본다(6항 체크리스트). 저장하지 말고 0으로 되돌린다.

테스트 — **`StageRoomWiringTests`(새 파일)**. 계획은 `SceneCompositionTests`였지만 파티 규약 테스트와 섞지 않고 `*WiringTests` 옆에 뒀다.
- `RoomStages_HaveExactlyOneStageRoom` — `board.NeedsRoom`인 스테이지는 방이 정확히 하나, 보드가 `FindIn`으로 찾는다. 확인한 스테이지가 3개인지도 본다(빈 통과 방지)
- `BaseRoom_MatchesTheWalls` — 벽 넷이 꽂혀 있고 자리 · 콜라이더 길이가 `WallFor(baseRoom)`와 0.05 이내. **위치로 판정.**
- `Walls_HaveNoRotation_AndCenteredColliders` — 벽 길이 계산의 전제
- `StageRoom_OwnsEveryWall` — 씬의 Wall 레이어 콜라이더(파티 호스트 제외)가 전부 방에 꽂혀 있다
- `StageRoom_AnchorsEverySpawnPoint` — `PartySpawnPoint` · 보드 스폰 지점이 전부 `anchors`에
- `Targets_DoNotNest` — 대상끼리 부모-자식 · 중복 · 빈 칸 없음, 방 자신은 대상이 아님
- ~~`RunsBeforePartyAssembler`~~ — 3단계 `StageRoomTests`로 앞당겼다
- 배선은 `SerializedObject`로 읽는다 — `StageRoom`에 테스트용 읽기 API를 늘리지 않았다

### ~~5단계 — 레시피 · 지도 모델에 방 비율~~ 완료

- `FloorRule.roomPercentMin/Max`(`[Range(0, 125)]`, 툴팁에 "둘 다 0이면 100% 고정 · 아레나 씬이 섞인 층은 0") + `HasRoomRange`. `NodeCandidate`는 안 바꿨다.
  기존 두 레시피 애셋은 필드가 없어 0 · 0으로 읽힌다 — 애셋은 안 건드렸다.
- `MapNode.RoomPercent` — 생성자 마지막 인자, **기본값 100**(0 이하는 100으로 읽음).
  2.3의 "기본값 오버로드 금지"와 다른 판단이다: 이 생성자를 부르는 실제 경로는 생성기 하나이고, 나머지 약 40곳은 모양만 짜는 테스트다.
  생성기가 넘기는지는 생성기 테스트가 본다.
- `RunMapGenerator.RollRoomPercent(floor, kind, seed, nodeId)` — `Build`에서 칸마다 부른다. 지도 모양 `rng`를 안 쓰고 `new System.Random(RoomSeed(seed, nodeId))`에서 한 번 뽑는다(2.2).
  - **`RoomSeed`는 섞기 함수(murmur3 끝단)를 거친다.** `System.Random`은 이웃한 시드의 첫 값이 닮아서, 시드와 Id를 곱해 더하기만 하면 한 지도 안에서 칸마다 비율이 줄지어 나올 수 있다.
- `RunMapRules`: `GetsRoomModifier(kind)`(전투 · 정예) · `IsValidRoomRange(min, max)` · `IsValidRoomPercent(kind, percent)`.
  - 레시피 검사에 `BadRoomRange`, **굴린 지도 검사에도 `BadRoomPercent`**(계획에 없던 것 — "거르는 쪽과 보는 쪽이 같은 규칙" 원칙대로 지도 불변식에 넣었다).
  - `RunMapProblem`은 끝에 붙였고, `Describe`의 `default:`를 `GenerationFailed` 명시 `case`로 꺼낸 뒤 새 문구 둘을 더했다. `default:`는 이제 enum 이름을 낸다.
- `RunMap.DescribeRooms()` — `방: #3 90% · #4 105%`(100이 아닌 칸만, 전부 100이면 빈 문자열). `Describe()`에는 안 섞었다.
  `GameManager.StartNewRun` 로그가 모양 줄 다음 줄에 붙인다.

테스트
- `RunMapRecipeTests` — 유효: 0·0 / 80·125 / 100·100 / 90·110. 문제 하나(`BadRoomRange`): 75·100 / 80·130 / 110·90 / 0·100 / 100·0 / 83·100. 새 문구가 기본 문구로 안 떨어진다
- `RunMapGeneratorTests`
  - **방 범위만 다른 레시피, 같은 시드 200개 → `Describe()` 같음** (2.2의 핵심)
  - 같은 시드 → 칸마다 `RoomPercent` · `DescribeRooms()` 같음
  - 1,000시드: 전투 · 정예 칸은 층 범위 안 5 단위, 보스(보스 층에 범위를 얹어도) · 비전투 · 범위 없는 층은 100
  - 90·110 범위에서 **90 · 95 · 100 · 105 · 110이 전부** 나온다(끝값 포함)
  - 한 지도 안 40칸(80·125)에서 6가지 이상 값 — 줄무늬 방지
  - 보스 · 휴식 · 상점 · 이벤트는 범위가 있어도 100
  - 범위 없는 레시피는 전부 100, `DescribeRooms()` 빈 문자열 / `DescribeRooms()` 문자열 형식
  - 검사기가 보스 90% · 전투 93% · 전투 70%를 `BadRoomPercent`로 잡는다
  - 기존 리터럴 `"0[Battle:A>1] / 1[Battle:B>2] / 2[Boss:C]"` 테스트는 손대지 않았다

### ~~6단계 — 보정 통로~~ 완료

- `EncounterModifier`: 저장값 `roomPercent`(0 = 보정 없음) + 읽기 `RoomPercent`(100으로 폄) · `HasElite` · `HasRoom` · `IsNone = !HasElite && !HasRoom`.
  생성자는 **`(eliteEvery, roomPercent)` 하나뿐** — 정예 간격만 받는 생성자도 지웠다(같은 이유).
- `EncounterModifierRules.For(MapNodeKind)` → **`For(MapNode)`**, 옛 서명 삭제. `null` 칸이면 `None`.
  방 크기는 `RunMapRules.GetsRoomModifier(kind)`인 칸만 싣는다 — 손으로 만든 지도에서 보스 칸에 비율이 박혀 있어도 여기서 한 번 더 막는다.
- **계획에 없던 수정: `IsElite`가 `IsNone` 대신 `HasElite`를 본다.** 방 크기만 있는 보정은 `IsNone`이 거짓인데 `EliteEvery`가 0이라,
  예전 조건 그대로면 `ordinal % EliteEvery`가 **0으로 나눈다** — 방 크기 보정이 걸린 첫 일반 전투 칸에서 소환 예약이 예외로 멈췄을 것이다.
- `GameManager.CurrentNodeModifier => EncounterModifierRules.For(CurrentNode)`.
- `ToString`: `보정 없음` / `3기마다 강화` / `방 90%` / `3기마다 강화 · 방 110%`. `StageDirector` · `BattleSceneController` 시작 로그에 그대로 찍힌다.

테스트
- `EncounterModifierRulesTests` — 기존 테스트를 `For(MapNode)`로 옮김(기대값 불변) + `default`의 방 크기 100 / `null` 칸 `None` /
  전투 칸은 방만 · 정예 칸은 둘 다 / **방 크기만 있는 보정에서 강화 판정이 0으로 안 나눈다** / 보스 · 비전투 칸은 비율이 박혀 있어도 100 / `ToString` 네 가지
- `GameManagerRunMapTests.CurrentNodeModifier_CarriesTheNodesRoomPercent` — 1층 90% 고정 레시피로 런을 열고, 칸 밖에서는 방 보정 없음, 칸에 들어가면 90

### ~~7단계 — `StageRoom`이 보정을 읽는다~~ 완료 (**게임 흐름 변경** — 단, 레시피가 전부 0이라 아직 전부 100%)

- `StageRoom.ResolveRequest(gm, debugPercent, out source)` — 순수 판단, `Awake`가 부른다.
  - **기준을 "GameManager가 있는가"가 아니라 "들어가 있는 지도 칸이 있는가"로 바꿨다.** 계획대로면 Boot를 겹쳐 연 채 스테이지 씬을 재생할 때
    (GameManager는 있지만 칸은 없음) 디버그 값이 안 먹어서 크기를 확인할 방법이 없다.
  - 칸이 있으면 **칸이 이긴다 — 칸이 100%여도.** 이때 디버그 값이 0이 아니면 경고한다(저장된 채 커밋되면 모든 런에서 그 크기로 돈다).
  - 칸이 없으면 디버그 값, 그것도 없으면 100.
  - 출처(`RoomRequestSource`: `None` · `RunNode` · `RunNodeOverDebug` · `DebugValue`)를 돌려주고, 100%가 아니면 `[StageRoom] 비율 출처: …`를 찍는다.
- `StageDirector.Awake`: **칸이 방 크기를 요청했는데(`modifier.HasRoom`) 씬에 `StageRoom`이 없으면 에러**(2.4) — 저작 크기로 돈다. 8단계 배선 테스트가 막는 경로다.

테스트 — `StageRoomTests` "비율은 어디서 오는가" 절(실제 `GameManager`로 런을 열어서)
- 런도 디버그도 없음 → 100 / 런 없음 → 디버그 값 / **GameManager는 있지만 칸 없음 → 디버그 값**
- 칸 90% → 90, 그 값으로 `Apply`하면 옆벽이 90% 자리(−5.65)로 옮겨진다
- 칸 90% + 디버그 110 → 90 / **칸 100% + 디버그 110 → 100** (둘 다 `RunNodeOverDebug`)

### ~~8단계 — 지도 화면 · 레시피 값~~ 완료

- `RunMapViewRules.Describe`: 설명 끝에 ` · 방 크기 90%`(100이면 안 붙임). 문구는 로그의 `방 90%`보다 풀어 썼다 — 플레이어가 읽는 글이다.
  비율은 칸에서 바로 읽지 않고 **`EncounterModifierRules.For(node)`에서** 읽는다 — 전투가 받는 값과 지도가 말하는 값이 한 규칙에서 나온다(보스 칸은 비율이 박혀 있어도 안 붙음).
  가장 긴 글: `정예 — 강화된 적이 3기마다 1기 섞여 나온다. 클리어 골드 x1.5 · 방 크기 90%` — 설명줄 안에 드는지는 눈으로 본다(6항).
- `RunMapScreen` 미리보기 로그에도 `DescribeRooms()` 줄(`GameManager`와 같은 형식).
- **`RunMap_Main.asset`에 2.6 제안값 입력** — 에디터에서 층별 후보가 표와 같은지 먼저 확인하고 넣었다: 2층(03 · 04 · 01정예 · 이벤트) 90~110, 3층(휴식 · 상점 · 04정예 · 03정예) 80~100.
  나머지 층은 에디터가 새 필드를 `0 · 0`으로 직렬화했다. `RunMap_Debug`는 안 건드렸다.
  샘플 굴림: 시드 1 `방: #3 90% · #4 95% · #5 85%` / 시드 2 `#3 90% · #4 105% · #5 90%` / 시드 3 `#4 105% · #5 110%` — 전부 2 · 3층 칸.
- 테스트
  - `RunMapSceneWiringTests.ResizableFloors_OnlyOfferScenesWithStageRoom(recipe, expectRanges)` — 범위 있는 층의 전투 · 정예 후보 씬을 Build Settings에서 찾아 열고,
    `StageRoom`이 있는지 + **기준 방 × 범위 양끝이 `RoomRules.Fits`인지**(계획의 `ShippedRecipes_RoomRangesWithinRules`를 여기 합쳤다).
    Main은 범위 있는 층 · 확인한 씬이 0이 아니어야 하고(빈 통과 방지), Debug는 범위 있는 층이 0이어야 한다.
  - `RunMapGeneratorTests.MainRecipe_RoomRanges_AreTheAgreedCurve` — Main 레시피 층별 범위가 2.6 표 그대로(값을 바꾸면 같이 바꾸는 변경 감지기)
  - `RunMapViewRulesTests` — 전투 90% · 정예 110%는 끝에 붙고 정예 설명이 안 잘림, 100%는 안 붙음, 보스는 비율이 박혀 있어도 안 붙음

### 9단계 — 출구선이 오른쪽 벽을 따라간다 (사후 수정)

8단계까지 놓친 ① 자리가 하나 있었다. **이긴 뒤 다음 칸으로 넘어가는 문턱**이다.

`BattleSceneController.exitX`가 `5`로 박혀 있었다 — 100% 방의 오른쪽 벽 `x = 6`에서 몸통 반지름만큼 뺀 값이다.
그런데 `StageRoom`이 벽을 옮기고 나면 이 `5`는 방에 대해 아무 뜻이 없다.

- **80% 방**: 벽이 `x = 4.8`, 캐릭터는 `4.3`에서 막힌다 → 문턱 `5`에 **영영 못 닿는다.**
  적을 다 잡고 "오른쪽 벽으로 이동" 안내까지 떠 있는데 벽에 붙어도 아무 일이 안 일어난다.
  화면만 봐서는 클리어 판정이 안 난 것처럼 보여서 원인이 여기라고는 짐작할 수가 없다.
- **125% 방**: 벽이 `x = 7.5` → 문턱이 `2.5`나 일찍 열린다.

Main 레시피 2층 `90~110`, 3층 `80~100`이므로 **그 층의 전투 · 정예 칸 상당수가 굴리는 순간 막힌 칸이 된다.**

고친 자리:
- `StageOutcomeRules.ExitInset = 1f` · `ExitLine(roomMaxX) => roomMaxX - ExitInset`.
  **여유는 비율을 안 탄다** — 몸통 반지름이 정하는 값이고 몸은 방이 줄어도 안 줄어든다(80%에 곱하면 여유가 0.3으로 다시 아슬아슬해진다).
- `BattleSceneController`가 `Start`에서 `StageRoom.Find()`로 문턱을 푼다. 방이 없는 씬(`SampleScene` · 훈련장)만 저작한 `exitX`를 쓴다.
  `StageRoom`은 실행 순서 -300이라 이 `Start`보다 먼저 방을 적용해 둔다.
- 테스트 `StageOutcomeRulesTests` — 100%가 기존 값 `5`와 같고, 80%에서 **고정 `5`가 안 열린다는 사실**을 못으로 박고,
  `MinPercent~MaxPercent` 전 구간에서 벽에 막힌 자리가 문턱을 넘는다.

---

## 6. 눈으로 확인할 것

테스트로 못 잡는 것만.

**4단계 — 씬 단독 실행, `debugRoomPercent` 80 / 125**
- [ ] 네 벽이 전부 화면 안이고, 125%에서 뒷벽 윗부분이 잘리지 않는다 (잘리면 2.5 한계를 조인다)
- [ ] 80%에서 벽 바깥에 빈 배경(카메라 클리어 색)이 드러나지 않는다 — 드러나면 "벽 밖 바닥" 그림을 `stretch`에서 빼 고정 크기로 둔다
- [ ] 캐릭터가 바닥 그림 끝과 벽 콜라이더 사이 틈에 서지 않는다
- [ ] 파티가 옮겨진 스폰 자리에서 시작한다 (원래 자리에서 한 프레임 튀지 않는다)
- [ ] 좌우에서 걸어 들어오는 적이 **새 벽 바로 앞**에서 나타나고, 100%일 때와 비슷한 **방 안 비율 위치**까지 걸어와 선다 (좁은 방에서 플레이어 코앞까지 오지 않는다)
- [ ] 마법사가 새 방 위 · 아래 끝에 붙는다. 80%에서 전사끼리 한 점에 겹치지 않는다
- [ ] **80%에서 마법사가 등장 직후 벽에 붙어 떨지 않는다** — 떨면 2.5 하한 계산이 틀린 것이다. 85%로 올리고 테스트 기준도 같이 고친다
- [ ] 발밑에서 솟는 적의 예고 표식이 방 안(옮겨진 지점)에 뜬다
- [ ] 80%에서 돌진전사를 깊이 이동으로 여전히 피할 수 있다 (못 피하면 하한을 올린다)
- [ ] 100%가 이전과 똑같다

**8단계 — Boot부터 Play, 시드 고정**
- [ ] 콘솔 새 런 로그에 `방: …` 줄이 찍히고, 지도에서 그 칸 설명줄에 같은 비율이 보인다
- [ ] 그 칸에 들어가면 `[Battle] 전투 시작 … 방 90%`가 찍히고 실제 방이 작다
- [ ] 패배 → [이 스테이지 재시작]이 **같은 비율**의 방을 연다
- [ ] [처음부터] → 다른 시드 → 다른 비율 배치
- [ ] 1층 Stage_02(아레나)와 보스 칸은 항상 원래 크기다

**9단계 — 출구**
- [ ] 80% 칸을 클리어하고 오른쪽 벽까지 걸어가면 지도로 돌아간다 (콘솔에 `[Battle] 출구선 x = 3.8 (방 80% …)`)
- [ ] 125% 칸에서 벽 한참 앞을 지나는 동안 지도로 튀지 않는다

---

## 7. 범위 밖

- **아레나 스테이지의 방 크기.** 폭을 바꾸면 뒤 구간이 밀린다. 깊이만이라면 가능하지만 앞 · 뒤 벽이 스테이지 전체를 가로지르는 한 장이라
  모든 아레나가 같이 변한다. 필요해지면 `EncounterSite`에 z를 더하고 `StageRoom`이 구간 전체 깊이만 다루는 형태로 따로 계획한다.
- **카메라 줌으로 125%를 넘는 방.** 2.5의 가독성 이유로 안 한다.
- **방 크기 외 층 비례 수치**(체력 +10%/층 등). 같은 통로(`EncounterModifier`)에 칸 하나 더하는 일이다 — Run_Map_Plan 5항.
- **레시피를 굽는 에디터 빌더.** 지금도 손으로 만들고 있고, 이 계획이 더하는 칸은 층마다 숫자 둘이다.

---

## 8. 정할 것

- ~~**난이도 방향.**~~ **정했다 — 2.6 제안값 그대로("뒤층일수록 좁아지고 흔들림이 커진다").** 플레이해 보고 쉬워지는 요소(마법사 거리 · 벽꽝)가
  더 크게 느껴지면 레시피 값만 뒤집는다 — 코드는 그대로고, `MainRecipe_RoomRanges_AreTheAgreedCurve` 기대값만 같이 고친다.
- **정예 칸에 추가 보정을 걸 것인가.** 지금 계획은 정예도 같은 층 범위를 쓴다. "정예는 항상 범위 하단" 같은 규칙을 원하면
  `EncounterModifierRules.For(MapNode)` 한 곳에서 처리한다. 레시피에 칸을 더 늘리는 방식은 권하지 않는다.
- **1층을 100% 고정으로 둘 것인가.** 아레나(Stage_02) 때문에 강제된 값이다. 1층을 흔들고 싶으면 Stage_02를 2층 이후로 옮기거나
  1층을 웨이브 방만으로 구성해야 한다 — Run_Map_Plan 6항의 "아레나를 건너뛸 수 있게 둘 것인가" 질문과 같이 정한다.
