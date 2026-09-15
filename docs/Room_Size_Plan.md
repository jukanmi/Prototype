# 방 크기 계획 — 씬마다 깊이와 폭을 다르게

> 상태: **계획.** 아직 코드에 손대지 않았다.
> 관련 코드: `Assets/Scripts/Stage/Wave.cs`(`WaveSpawnPlanner`), `Arena.cs`(`ArenaSpawnPlanner`),
> `StageWaveBoard.cs`, `EncounterSite.cs`, `StageDirector.cs`, `Entrance.cs`
> 관련: [Stage_Encounter_Unification_Plan.md](Stage_Encounter_Unification_Plan.md) — 조우 · 자리 모델. 이 계획은 그 "자리"에 깊이를 더한다.

---

## 0. 무엇을 바꾸는가

벽과 바닥은 씬 오브젝트라 이미 씬마다 다르게 놓을 수 있다. 문제는 **적이 어디서 나올지 계산하는 쪽이
방 크기를 상수로 들고 있다**는 것이다. 벽만 옮기면 벽과 스폰 계산이 서로 다른 방을 믿게 된다.

| 있는 것 | 지금 크기를 어디서 얻나 | 이번 계획에서 |
| --- | --- | --- |
| `WaveSpawnPlanner` (웨이브 방 자동 배치 · 지점 검사) | `const RoomHalfX = 6`, `const RoomHalfZ = 3` | **방을 인자로 받는다.** 상수는 지운다 |
| `ArenaSpawnPlanner` (아레나 벽 스폰) | X는 `EncounterSite`, Z는 `const ArenaHalfZ = RoomHalfZ` | **Z도 자리에서 받는다** |
| `StageWaveBoard` (지점 검사 · 방 기즈모) | 위 상수 | **웨이브 방 크기를 들고 있는 곳이 된다** |
| `EncounterSite` (아레나 자리) | `minX`/`maxX`만 | **`minZ`/`maxZ`를 더한다** |
| `EntranceRules.FallbackHalfWidth` | `RoomHalfX + 2` | 기본 방 값에서 얻는다 (카메라 없는 씬 전용이라 의미 불변) |
| 씬의 `Wall_*` · `Floor` | 씬에 직접 배치 | 그대로. **대신 방 크기와 어긋나면 테스트가 잡는다** |

한 줄로: **방 크기는 씬이 아는 값이고, 스폰 계산은 그 값을 받아 쓴다. 상수는 없다.**

---

## 1. 벽만 옮기면 무엇이 깨지나

계획의 근거라 먼저 적는다. 모두 화면에 원인 단서가 없는 종류다.

- **방을 키우면** — 자동 배치 적이 옛 6×3 경계 기준으로 나와서 방 가운데서 튀어나온다.
  마법사는 "맵 최상단·최하단"에 붙어야 하는데 실제 벽보다 한참 안쪽에 선다.
- **방을 줄이면** — 스폰 지점이 벽 밖이 된다. 방은 사방이 콜라이더라 벽 밖에서 나온 적은
  **영영 못 들어오고**, 증상은 "그 웨이브가 안 끝난다"다(`WaveSpawnPlanner.SpawnInset` 주석).
- **아레나 깊이를 바꾸면** — 앞·뒤 벽 스폰(`SpawnWall.Front/Back`)과 예고 표식이 옛 z=±3 선에 뜬다.
- **보드 검사가 거짓말을 한다** — `StageWaveBoard.IsInsideRoom`이 옛 방으로 판정해서,
  멀쩡한 지점을 "방 밖"이라 경고하거나 벽 밖 지점을 통과시킨다. 기즈모도 옛 방을 그린다.

---

## 2. 확정할 결정 다섯

### 2.1 값의 형태 — 반경 둘이 아니라 사각형(`RoomRect`)

```csharp
public readonly struct RoomRect
{
    public readonly float MinX, MaxX, MinZ, MaxZ;
    public float CenterX, CenterZ, HalfX, HalfZ;   // 파생
    public static readonly RoomRect Default = new RoomRect(-6f, 6f, -3f, 3f);
}
```

- 아레나는 이미 `minX`/`maxX`로 저작한다. 웨이브 방만 반경으로 두면 **같은 개념이 두 모양**이 된다.
- 지금 웨이브 방이 원점 중심이라 `Mathf.Abs(point.x) <= RoomHalfX` 식의 원점 가정이 박혀 있다.
  사각형으로 바꾸는 김에 그 가정도 같이 뺀다. 계산이 "중심 + 상대값"으로 바뀔 뿐 늘지 않는다.
- `GroundRect`를 재사용하지 않는다. 그쪽은 **발판**(높이 Y를 가진 밟는 면)이고, 이쪽은 **소환 가능 구역**이다.
  발판이 여러 장인 방에서 둘이 같을 이유가 없다.

### 2.2 크기를 들고 있는 자리 — 웨이브 방은 보드, 아레나는 자리

| 조우 종류 | 크기를 드는 곳 | 이유 |
| --- | --- | --- |
| 방 전체가 자리인 조우 | `StageWaveBoard.room` | 디렉터가 도는 씬에는 보드가 **정확히 하나** 있고, 없으면 디렉터가 에러로 멈춘다. 이미 "방 밖 지점" 검사와 방 기즈모를 맡고 있다 — 그 둘이 읽던 상수를 자기 필드로 바꾸는 것뿐이다 |
| 자리에 묶인 조우(아레나) | `EncounterSite.minZ`/`maxZ` | X를 이미 여기서 든다. 아레나마다 깊이가 다를 수 있다 |

**`StageBounds`에 두지 않는다.** 그쪽은 카메라용 X 경계이고 보간된다. 스폰은 보간되지 않은 값이 필요하고
(`UnitMin` 주석이 같은 이유), 깊이는 카메라가 읽지 않는다. 새 컴포넌트도 만들지 않는다 —
`EncounterSite` · `PartySpawnPoint` · `StageWaveBoard`가 이미 "방마다 다른 값, 방이 아는 값" 규약이다.

### 2.3 기본값 폴백을 두지 않는다 — 인자는 필수

`PlanAuto(entry, lane, playerZ)` 같은 기존 서명을 기본값 오버로드로 남기지 **않는다.**

- 남기면 "방을 넘기는 호출"과 "상수 방을 쓰는 호출"이 공존하고, 하나라도 빠뜨리면 그 경로만
  옛 방으로 돈다. 증원(`TickReinforcements`)이 정확히 그런 두 번째 호출 자리다.
- 이 저장소에서 조용한 폴백은 이미 한 번 사고를 냈다(`c325639f`, 3 · 4스테이지가 1번 표로 돌던 버그).
- 비용은 테스트 호출 약 40곳에 `RoomRect.Default`를 넣는 기계적 수정이다. 컴파일러가 전부 찾아 준다.

`RoomRect.Default`는 **직렬화 필드의 초기값과 테스트에서만** 쓴다. 런타임 경로에서 기본값을 꺼내 쓰는 곳은
카메라가 없는 씬용 `EntranceRules.FallbackHalfWidth` 하나다.

### 2.4 줄 간격은 방 크기에 비례시키지 않는다

| 역할 | 지금 규칙 | 방이 바뀌면 |
| --- | --- | --- |
| 전사 | 깊이 줄 `{0, ±1.6, ±2.4}` | **중심 기준 상대값으로, 간격 그대로.** 방 밖이면 물린다 |
| 돌진전사 | 플레이어 줄 ± 1.2 | 그대로 — 이미 플레이어 기준이다 |
| 마법사 | 최상단·최하단 구석부터 | **자연히 따라간다** — 구석이 `MaxDepth`에서 나오므로 |
| 좌우 정착 지점 | 벽에서 2.8 / 1.8 / 1.2 | 그대로 — 벽 기준 거리다 |

간격은 **몸통 크기와 공격 사거리**에서 나온 값이지 방 크기에서 나온 값이 아니다. 비례시키면 큰 방에서
전사끼리 서로 사거리 밖으로 흩어지고, 작은 방에서 겹친다. 큰 방에서 전사가 가운데 띠에 모이는 것은
의도된 결과로 본다 — 기획이 다르게 원하면 그때 `MeleeLanes`만 따로 손본다.

### 2.5 방에는 최소 크기가 있다 — 규칙이 정하고 보드가 경고한다

지금 규칙은 방이 6×3이라는 전제에서만 성립하는 곳이 있다. 그 전제를 코드로 끌어올린다.

| 조건 | 깨지면 | 출처 |
| --- | --- | --- |
| `HalfX > MeleeInset(2.8)` | 전사 정착 X가 중심을 넘어 **반대편 벽 쪽**에 선다 | `PlanAuto`의 `entryX` |
| `HalfZ − DepthInset(0.6) ≥ RangedLaneGap(1.5)` | 마법사 줄 탐색이 전부 실패해 폴백으로 떨어진다 | `RangedDepth` 끝 주석 |
| 아레나 `HalfZ > EntryDepth(2.2)` | 앞·뒤 벽 적의 목표 셀이 중심을 넘는다 | `ArenaSpawnPlanner.Place` |

→ `RoomRules.IsLargeEnough(in RoomRect)` / `ArenaRules`쪽 같은 질문 하나로 두고,
보드 검사에 `BoardProblem.RoomTooSmall`을 더한다. 조용히 물리지 않고 **경고한다** — 저작 실수를 저작자가 알아야 한다.

---

## 3. 단계

각 단계는 컴파일되고 기존 동작(6×3 방)이 그대로인 상태로 끝난다. 5단계까지 씬 값은 전부 기본값이므로
**게임 화면은 달라지지 않는다.**

### 1단계 — `RoomRect`와 최소 크기 규칙

- 새 파일 없이 `Wave.cs`의 `WaveSpawnPlanner` 위에 `RoomRect` 섹션을 둔다(파일 수 줄이기 방침,
  [Stage_Refactor_Plan.md](Stage_Refactor_Plan.md)). `[Serializable]`로 인스펙터에 나오게 하려면
  readonly 필드를 못 쓰므로, 직렬화용은 `minX/maxX/minZ/maxZ` public 필드 + 생성자에서 min/max 정렬로 한다.
- `Contains(x, z, inset)`, `ClampInside(point, insetX, insetZ)`, `MaxDepthFromCenter(inset)`.
- 최소 크기 규칙(2.5).

테스트 `Assets/Editor/Tests/RoomRectTests.cs`
- 뒤집힌 min/max가 정렬된다
- 원점이 아닌 방에서 `Contains` · `ClampInside`가 중심 기준으로 동작한다
- `Default`가 6×3이고 최소 크기를 통과한다
- 경계값: 최소 조건 바로 위/아래

### 2단계 — `WaveSpawnPlanner`가 방을 받는다

서명 변경 (전부 `in RoomRect room` 추가):
`PlanAuto` · `PlanAt` · `IsInsideRoom` · `ClampIntoRoom` · `DepthFor` · `ChargerDepth` · `RangedDepth` · `Clamp`

- `RoomHalfX` · `RoomHalfZ` · `MaxDepth` **상수/프로퍼티를 지운다.** 남겨 두면 누군가 다시 읽는다.
- 모든 좌표를 `room.CenterX/CenterZ` 기준 상대값으로. `side * (RoomHalfX - inset)` →
  `room.CenterX + side * (room.HalfX - inset)`.
- `playerZ`는 월드 좌표로 들어오므로 상대값으로 바꿔 계산하고 월드로 돌려준다.

테스트
- 기존 `WaveSpawnPlannerTests` · `WaveLayoutTests` · `SpawnPlacementTests` · `SpawnRouteRulesTests`:
  `RoomRect.Default`를 넘기도록 기계적 수정. **기대값은 하나도 안 바뀌어야 한다** — 바뀌면 리팩터링 실수다.
- 새 테스트: 깊이 ±5 방에서 마법사가 ±4.4 구석에 선다 / 깊이 ±2.2 방에서 전사 줄이 방 안으로 물린다 /
  중심이 (20, 0, 1)인 방에서 모든 역할의 spawn · entry가 방 안이다 / 폭 ±9 방에서 좌우 스폰이 ±8.4다.

### 3단계 — 아레나 깊이를 자리에서 받는다

- `ArenaSpawnPlanner.PlanFromWall(in entry, float minX, float maxX)` → `PlanFromWall(in entry, in RoomRect arena)`.
  `const ArenaHalfZ` 삭제.
- `EncounterSite`에 `[SerializeField] float minZ = -3f, maxZ = 3f` 추가, `Rect` 프로퍼티로 넷을 묶어 낸다.
  `Configure`에 z 인자 추가. 기즈모가 `ArenaHalfZ` 대신 자기 z를 그리고, 앞·뒤 선도 그린다.
- 기존 직렬화 씬은 새 필드가 기본값(-3, 3)으로 들어오므로 **씬 수정 없이 동작이 같다.**
- `StageDirector.QueueFromWalls`가 `site.Rect`를 넘긴다.

테스트
- `ArenaSpawnPlannerTests` · `RoundAssetContentTests` · `SpawnPlacementTests`: `new RoomRect(MinX, MaxX, -3, 3)`로 수정, 기대값 불변.
- 새 테스트: 깊이 ±5 아레나에서 Front 스폰이 z > 5, 예고 지점이 z == 5 / Back 진입선 판정 부호 유지.

### 4단계 — 보드가 방 크기를 들고, 디렉터가 넘긴다

- `StageWaveBoard`에 `[SerializeField] RoomRect room = RoomRect.Default` + `public RoomRect Room`.
- `CollectPointIssues`의 `IsInsideRoom`이 `room`을 쓴다. `BoardProblem.RoomTooSmall` 추가(2.5).
  **enum은 끝에 붙인다** — 직렬화 여부와 무관하게 기존 순서 의존을 피한다.
- `DrawRoomBounds`가 `room` 중심 · 크기로 그린다(지금은 원점 고정). `static` 해제.
- `StageDirector.QueueInRoom` · `TickReinforcements`가 `board.Room`을 넘긴다.
  보드가 없는 경로는 이미 에러로 멈추므로 새 폴백을 만들지 않는다.
- `EntranceRules.FallbackHalfWidth` → `RoomRect.Default.HalfX + 2f` (`const`가 안 되므로 `static readonly`).

테스트 `StageWaveBoardTests`
- 원점이 아닌 방에서 방 안 지점이 경고 없음 / 방 밖 지점이 `OutsideRoom`
- 너무 작은 방이 `RoomTooSmall` 하나를 낸다

### 5단계 — 씬의 벽이 방 크기와 맞는지 테스트가 본다

벽(콜라이더)과 숫자(보드 · 자리)가 **진실 두 개**가 되는 것을 이 단계가 막는다.
`SceneCompositionTests`에 추가:

- `WaveStages_WallsMatchRoom` — 자리 없는 조우만 가진 스테이지에서, 벽 콜라이더 네 개의 **안쪽 면**이
  `board.Room`의 네 변과 0.05 이내로 맞는다.
- `ArenaStages_WallsMatchSiteDepth` — 아레나 스테이지에서 앞·뒤 벽 안쪽 면이 각 `EncounterSite`의 `minZ`/`maxZ`와 맞는다.

**함정: 벽은 이름이 아니라 위치로 판정한다.** Stage_01에서 `Wall_Front`는 z = −3.25(화면 아래),
`Wall_Back`은 z = +3.25에 있다. 반면 `SpawnWall.Front`는 +Z(화면 위 뒷벽)다. **이름과 코드의 앞뒤가 반대다.**
이름으로 짝지으면 테스트가 처음부터 틀린다. 레이어가 벽 마스크인 콜라이더를 모아 중심에서 어느 쪽에 있는지로 가른다.

아레나 스테이지(02, 05)는 앞·뒤 벽이 스테이지 전체를 가로지르는 한 장일 수 있다. 그러면 모든 아레나가
같은 깊이를 가져야 하고, 테스트도 그렇게 요구한다. 아레나마다 깊이를 다르게 하려면 앞·뒤 벽을 아레나별로
끊어야 한다 — 이건 씬 작업 범위라 이 계획에서는 **"한 스테이지 = 한 깊이"를 전제로 두고** 필요해지면 푼다.

### 6단계 — 씬 작업 (사람이 에디터에서)

바꾸고 싶은 스테이지마다:

1. `StageWaveBoard.room`(아레나면 각 `EncounterSite`의 z)을 원하는 값으로.
2. `Wall_Left/Right/Front/Back` 위치와 `BoxCollider.size`를 안쪽 면이 그 값에 오도록.
   (지금 규격: 벽 두께 0.5, 좌우 벽 x = ±6.25 / size z = 6, 앞뒤 벽 z = ±3.25 / size x = 13)
3. `Floor` 스케일(지금 12 × 6), 뒷벽 그림(지금 z = 3, 스케일 12 × 4)을 맞춘다.
   `Floor`의 `GroundPlate`가 `halfExtents = 0`이면 스프라이트를 따라가므로 스케일만 바꾸면 된다.
4. 스폰 지점 트랜스폼이 새 방 안인지 — 보드 기즈모가 빨갛게 표시한다.
5. 5단계 테스트가 초록이 되는지 (Test Runner가 알아서 돈다).

#### 카메라 — 웨이브 방은 여전히 "방 하나 = 화면 하나"

웨이브 방 카메라는 `CameraFollow`가 꺼진 고정 구도다(`CameraAnchor` 주석). 방이 화면보다 커지면
벽이 화면 밖으로 나간다. 지금 설정(직교 size 5, 기울기 50°, framingLift 0.5, 16:9) 기준 **추정** 한계:

| 방향 | 한계 | 근거 |
| --- | --- | --- |
| 폭 | 반폭 ≈ 8.4 | 화면 반폭 5 × 16/9 ≈ 8.9에서 벽 두께만큼 |
| 깊이 (뒷벽 높이 4까지 다 보이게) | 반깊이 ≈ 3.8 | 0.766·z + 0.643·4 − 0.5 ≤ 5 |
| 깊이 (앞쪽 바닥 끝) | 반깊이 ≈ 5.8 | 0.766·z + 0.5 ≤ 5 |

**깊이를 3.8보다 키우면 뒷벽 윗부분이 잘린다.** 그 이상이 필요하면 해당 씬의 Main Camera `orthographic size`를
올린다(화면 전체가 같이 작아진다). 방이 중심에서 벗어나면 고정 카메라 위치도 옮겨야 한다.
위 숫자는 계산 추정이라 씬에서 확인한다.

---

## 4. 눈으로 확인할 것

테스트로 못 잡는 것만. 크기를 바꾼 스테이지를 재생해서:

- [ ] 네 벽이 전부 화면 안에 보이고, 뒷벽 윗부분이 잘리지 않는다
- [ ] 좌우에서 걸어 들어오는 적이 **벽 바로 앞**에서 나타난다 (방 가운데서 튀어나오지 않는다)
- [ ] 마법사가 새 방의 위·아래 끝에 붙는다
- [ ] 발밑에서 솟는 적의 예고 표식이 방 안에 뜬다
- [ ] 아레나: 앞·뒤 벽 예고 표식이 **실제 벽 위치**에 뜨고, 적이 그 벽에서 걸어 나온다
- [ ] 캐릭터가 바닥 그림 끝과 벽 콜라이더 사이에 떠 있는 틈이 없다
- [ ] 크기를 안 바꾼 스테이지(한 곳 이상)가 전과 똑같다

---

## 5. 범위 밖

- **방보다 큰 웨이브 방(스크롤 웨이브 방).** `CameraFollow` + `StageBounds`를 켜야 하고,
  "화면 밖에서 등장" 규칙(`Entrance`)이 방 가장자리와 화면 가장자리가 다르다는 걸 알아야 한다. 따로 계획한다.
- **아레나마다 다른 깊이.** 5단계 끝 참고 — 앞·뒤 벽을 아레나별로 끊는 씬 작업이 먼저다.
- **벽 · 바닥을 방 크기에서 자동 배치하는 에디터 메뉴.** 5단계 테스트가 어긋남을 잡으므로 급하지 않다.
  스테이지를 자주 만들게 되면 그때 `Prototype/스테이지 - 방 크기대로 벽 맞추기`로 추가한다.
- **줄 간격 비례.** 2.4의 결정. 기획이 원하면 `MeleeLanes`만 따로 다룬다.
