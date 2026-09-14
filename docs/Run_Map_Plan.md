# 런 지도 계획 — 일렬 스테이지를 갈림길 지도로

> 상태: **0 · 1 · 2 · 3 · 5단계 완료** — 비전투 칸 씬 · 골드, 지도 모델 · 생성기, 지도 씬 · 화면, 흐름 전환, 정예 보정.
> [시작] → 지도 → 칸 → 지도 → … → 보스로 돈다. 남은 것은 6단계(레시피 조정)와 6항의 정할 것 하나(아레나 건너뛰기).
> 관련 코드: `Assets/Scripts/FlowScene/GameManager.cs`, `BattleSceneController.cs`, `MainMenuController.cs`,
> `SceneNames.cs`, `NodeSceneController.cs`, `NodeWindows.cs`,
> `Assets/Scripts/Progression/RunProgression.cs`(`GoldRules` · `NodeRules` · `PartyState`), `RunEventAsset.cs`,
> `Assets/Scripts/Stage/StageDirector.cs`, `EnemySpawnService.cs`, `WaveAsset.cs`
> 관련: [Stage_Encounter_Unification_Plan.md](Stage_Encounter_Unification_Plan.md) 7.2 — "굴림은 채우는 단계, 디렉터는 채워진 것만 돈다"

---

## 0. 무엇을 바꾸는가

지금 런은 **한 줄**이다. `GameManager.stageScenes`(Stage_01 → 05)를 `CurrentStageIndex`로 한 칸씩 밟고,
이기면 출구선을 넘어 다음 씬으로 바로 간다. 고를 것이 없다.

바꾸고 나면 런은 **층으로 된 지도**다. [시작]을 누르면 그 판의 지도가 시드로 한 번 굴려지고,
전투를 이기면 지도로 돌아와 다음 칸을 고른다.

| 있는 것 | 지금 | 이번 계획에서 |
| --- | --- | --- |
| `GameManager.stageScenes` · `CurrentStageIndex` · `DefaultStages` · `DebugStages` | 일렬 목록과 인덱스 | **지운다.** 지도(`RunMap`)와 진행(`RunMapProgress`)이 대신한다 |
| `GameManager.GoToNextStage` | 다음 씬으로 바로 | **지도로 돌아간다** (`CompleteNodeAndReturnToMap`) |
| `MainMenuController.OnStartClicked` | 첫 스테이지 씬을 연다 | **지도 씬을 연다** |
| `BattleSceneController` 승리 · 출구 | `HasNextStage`면 다음 씬 | `HasNextNode`면 지도로, 마지막 층이면 전체 클리어 |
| `StageDirector` | 보드의 조우를 저작 그대로 | **칸 종류의 보정값을 받아** 소환 순간에만 적용 |
| `PartyState` | 체력 비율 · 생사 기록 | `ChangeHp(delta, roster)` — 몸 없는 씬에서 체력 증감 **(완료)** |
| `RunProgression` | 경험치 · 레벨 · 런 덱 | **골드**(`Gold` · `AddGold` · `TrySpendGold`) **(완료)** |
| 씬 `Node_Rest` · `Node_Shop` · `Node_Event` | 없음 | 비전투 칸. `NodeSceneController` 하나가 들어오자마자 창을 띄운다 **(완료)** |
| 씬 `RunMap` | 없음 | **새로 만든다.** Boot 위에 Additive로 뜨는 지도 화면 |

한 줄로: **지도는 "다음에 어느 씬을 어떤 보정으로 여는가"만 정한다. 씬 안의 내용은 그 씬이 정한다** —
전투 씬은 보드가, 비전투 씬은 `NodeSceneController`가.

---

## 1. 제안받은 설계와 무엇이 다른가

붙여 받은 설계는 `MapGenerator → RunMapController(FSM) → NodeMapView → RuntimeWaveFactory → StageDirector`다.
**시점 설명(부팅이 아니라 [시작] 순간에 굴린다)은 그대로 맞다.** 이 저장소에 붙일 때 바꿔야 하는 곳이 셋이다.

### 1.1 `RuntimeWaveFactory`(웨이브 애셋 복제)는 만들지 않는다 — 보정값을 소환 순간에 적용한다

복제가 이 저장소에서 성립하지 않는 이유가 셋이다.

1. **웨이브는 씬에 묶여 있다.** `WaveAsset`의 스폰 줄은 지점을 **이름**으로 부르고, 그 이름을 실제 자리로
   바꾸는 것은 그 씬의 `StageWaveBoard`다. 복제본을 다른 씬에 들고 가면 `UnknownPointId`로 떨어진다.
   같은 씬에서 복제하는 것이면 원본을 쓰는 것과 차이가 없다.
2. **진실이 둘이 된다.** 디렉터는 보드가 든 디스크 애셋을 읽는다(`WaveAsset` 주석 "디렉터는 읽기만 한다").
   복제본을 밀어 넣으려면 "보드의 목록"과 "주입된 목록" 중 무엇을 쓸지 고르는 분기가 생기고,
   이 저장소는 그 조용한 폴백으로 이미 한 번 사고를 냈다(`c325639f`).
3. **강화는 이미 소환 창구에 있다.** `EnemySpawnService`가 `elite` 표시를 받아 체력 ×2.5 · 공격 ×1.4를 건다.
   난이도 보정은 "애셋을 고친 사본"이 아니라 **"읽을 때 한 칸을 뒤집는 순수 규칙"**이면 충분하다.
   만들고 `Destroy`할 수명도, 딥카피 검증도 필요 없다.

→ `EncounterModifier` 구조체 + `EncounterModifierRules.IsElite(...)` 순수 함수. 디렉터가 `Pending`을 만들 때
`elite` 칸 하나를 이 규칙으로 정한다. **애셋은 한 톨도 안 바뀐다.**

### 1.2 `RunMapController`의 상태 머신은 두지 않는다 — 떠 있는 씬이 곧 상태다

"노드 고르는 중 / 전투 중"은 **지금 어느 씬이 올라와 있는가**와 같은 정보다. 상태 enum을 따로 들면
씬 전환이 거절됐을 때(`SceneLoader.IsBusy`) 둘이 어긋난다. 진행은 값 두 개로 충분하다.

```
RunMapProgress
├─ current   마지막으로 끝낸 칸 (시작 전 None)
└─ pending   골라서 들어갔지만 아직 안 끝낸 칸 (없으면 None)
```

- 고를 수 있는 칸 = `current`가 None이면 0층 전부, 아니면 `current`의 간선.
- 패배 후 재시작 = `pending`을 다시 연다. 되돌리는 코드가 필요 없다 — `PartyState.Capture`가 클리어 때만 찍는 것과 같은 방식이다.

### 1.3 지도는 `GameManager`에 한 번만 굴린다 — 씬 오브젝트는 결과를 읽기만 한다

`GameManager`는 "씬 오브젝트를 절대 참조하지 않는다"가 규약이다. 지도 데이터(`RunMap`)도 순수 C# 클래스로 두고
`GameManager`가 들고 있는다. 지도 화면(`RunMapScreen`)과 디렉터는 `GameManager.Instance`에 **묻기만** 한다 —
`PartyAssembler`가 `Loadout`을 묻는 것과 같다.

---

## 2. 확정할 결정 일곱

### 2.1 칸 하나 = 씬 하나 + 종류 — 씬을 여는지는 종류가 아니라 씬 칸이 정한다

```csharp
public enum MapNodeKind { Battle, Elite, Rest, Shop, Event, Boss }

public sealed class MapNode
{
    public int Id;          // 지도 안에서 유일. 씬 이름을 키로 쓰지 않는다 — 같은 씬이 여러 칸에 나온다
    public int Floor;       // 0부터
    public int Column;      // 층 안에서 왼→오 순서. 간선 교차 판정이 이 값을 본다
    public MapNodeKind Kind;
    public string Scene;    // 비었으면 지도에서 바로 처리한다 (지금은 Rest만 그게 된다)
    public int[] Next;      // 다음 층 칸 Id
}
```

**종류는 의미(아이콘 · 보정값)만, 씬 칸은 "어디서 도는가"만 말한다.** 처음 계획은 `Rest`면 씬이 없다고
종류로 정했는데, 그러면 휴식방 씬을 만드는 순간 검증이 막고 흐름 코드를 고쳐야 한다. 두 축을 떼어 두면
새 칸 씬은 **씬 파일 + 레시피 한 줄**로 들어온다.

| 씬 칸 | 동작 |
| --- | --- |
| 있음 | 그 씬을 연다. 전투 씬은 출구, 비전투 씬은 `NodeSceneController`의 [나가기]가 칸을 끝낸다 |
| 비었음 | 씬 전환 없이 지도에서 처리한다. **`Rest`만 허용** — 나머지 종류는 레시피 검증이 막는다 |

조우 목록을 칸이 들지 않는 이유는 1.1의 1번이다. 조우를 굴리는 것은 5항 "범위 밖"의 이음매로 남긴다.

### 2.2 지도 모양은 애셋(`RunMapRecipe`)이 정한다 — 층 수는 목록의 길이다

```
RunMapRecipe (ScriptableObject)
└─ floors[]                    ← 층 수 = 이 배열의 길이 (정수 칸 없음, Stage_Encounter 2.2와 같은 이유)
   ├─ minNodes / maxNodes
   └─ candidates[]
      ├─ scene   (씬 이름 문자열)
      ├─ kind
      └─ weight
```

**처음 넣을 값(제안).** 씬이 다섯뿐이라 폭을 넓게 잡을 수 없다. 대신 `StageWaveCatalog`의
**역할군 도입 순서(전사 → 돌진전사·마법사 → 조합 → 총력전 → 보스)를 층 순서로 지킨다.**
앞 층 후보에 뒤 스테이지를 넣지 않는다 — 1층에서 마법사를 처음 만나면 학습 곡선이 무너진다.

| 층 | 칸 수 | 후보 |
| --- | --- | --- |
| 0 | 1 | Stage_01 (Battle) — 입문은 고정 |
| 1 | 2 | Stage_02 (Battle, 아레나) · Stage_03 (Battle) |
| 2 | 2~3 | Stage_03 (Battle) · Stage_04 (Battle) · Stage_01 (Elite) · Node_Event (Event) |
| 3 | 2 | Node_Rest (Rest) · Node_Shop (Shop) · Stage_04 (Elite) · Stage_03 (Elite) |
| 4 | 1 | Stage_05 (Boss) |

보스 직전 층에 휴식 · 상점을 모은다. 골드가 가장 많이 쌓인 때이고, 보스 앞에서 체력을 채울지
카드를 살지가 실제 선택이 된다.

값은 애셋이라 코드 수정 없이 바꾼다. 디버그 경로(`Stage_Mini → SampleScene → Stage_Boss`)는
**폭 1짜리 레시피 애셋**(`RunMap_Debug.asset`)이 된다 — 일렬 목록은 "칸이 하나씩인 지도"일 뿐이라 모델이 둘일 필요가 없다.

### 2.3 생성 규칙은 검사할 수 있는 불변식으로 적는다

`RunMapGenerator.Generate(IReadOnlyList<FloorRule> floors, int seed) → RunMap` — `System.Random(seed)`만 쓰는 순수 함수.

| 불변식 | 깨지면 |
| --- | --- |
| 마지막 층은 정확히 1칸이고 `Boss`다 | 끝이 둘이면 "전체 클리어"를 판정할 칸이 없다 |
| 마지막 층이 아닌 칸은 다음 층으로 간선 ≥ 1 | 막다른 칸에 들어가면 런이 멈춘다 |
| 0층이 아닌 칸은 들어오는 간선 ≥ 1 | 영영 못 가는 칸이 그려진다 — 고장으로 보인다 |
| 간선이 교차하지 않는다 (`a.Column < c.Column`이면 `b.Column ≤ d.Column`) | 선이 엉켜 어느 칸으로 가는지 안 읽힌다 |
| 간선 양끝이 같은 씬이 아니다 | 같은 방을 연달아 두 번 — 갈림길이 선택처럼 안 느껴진다 |
| 비전투 칸(`Rest` · `Shop` · `Event`) 뒤에 같은 종류가 없다 | 휴식 두 번 · 상점 두 번이 선택지를 대신한다 |

**간선은 단조 연결로 만든다.** 위층 `i`번 칸을 아래층 `round(i·(n₂−1)/(n₁−1))`에 잇고, 들어오는 간선이 없는
아래층 칸은 가장 가까운 위층 칸에 잇는다. 그 뒤 시드로 이웃 칸(`j+1`)에 간선을 하나 더 얹되
교차하면 버린다. 단조로 이으면 교차 불변식이 구성상 성립한다.

**씬 배정이 제약을 못 맞추면 조용히 물리지 않는다.** 정해진 횟수만큼 다시 뽑고, 그래도 안 되면
`RunMapIssue`를 돌려준다. 실제 레시피가 이 경우에 걸리는지는 **시드 1,000개를 굴리는 테스트**가 본다.

### 2.4 시드는 로그에 찍고, 고정할 수 있다

- `GameManager`에 `[SerializeField] int debugSeed` — 0이면 `Environment.TickCount`, 아니면 그 값.
- `StartNewRun`이 `"[GameManager] 새 런 — 시드 {seed}"`를 찍는다. 이상한 지도가 나오면 그 숫자로 재현한다.
- **[처음부터]는 새 런이다** → 새 시드. `debugSeed`가 있으면 같은 지도.

### 2.5 보정값은 "몇 번째마다 강화"이고, 보스에는 걸지 않는다

```csharp
public readonly struct EncounterModifier
{
    public readonly int EliteEvery;              // 0이면 보정 없음
    public static readonly EncounterModifier None = default;
}

public static class EncounterModifierRules
{
    public static EncounterModifier For(MapNodeKind kind);   // Elite → EliteEvery = 3, 나머지 → None
    public static bool IsElite(bool authored, EnemyRole role, int ordinal, in EncounterModifier m);
}
```

- `IsElite` = `authored || (role != Boss && m.EliteEvery > 0 && ordinal % m.EliteEvery == 0)`.
- **보스 제외가 핵심이다.** `ApplyEliteScale`은 역할을 가리지 않아서, 규칙이 안 막으면 Stage_05를 Elite 후보에 넣는 순간 체력 2.5배 보스가 나온다.
  지금 레시피엔 없지만 애셋 값 하나로 생기는 실수라 규칙이 막는다.
- `ordinal`은 조우 안에서 줄 번호, 증원은 `reinforcedSoFar`.
- **동시 공격 수(`attackTokens`)는 안 건드린다.** `StageWaveCatalog.LateTokens` 주석대로 3을 넘기면 회피가 성립하지 않는다.
- 씬 단독 실행(GameManager 없음)은 `None` — "저작 그대로"라서 다른 표로 도는 폴백이 아니다. `RunProgression.Current`의 단독 런과 같은 성격이다.

### 2.6 지도는 별도 씬이다 — Boot 오버레이가 아니다

- `SceneLoader.SwapTo`가 이미 "전투 씬 내리기 → 새 씬 올리기 → 활성 씬 지정 → 페이드"를 한다. 지도를 씬으로 두면 **전환 코드가 한 줄도 안 는다.**
- Boot에 UI를 두면 전투 씬 위에 겹치는 순간이 생기고, `BattleRegistry` · `TimeControl` 정리 순서를 새로 따져야 한다.
- 씬 칸이 빈 휴식 칸은 지도에서 바로 회복하고 끝낸다(2.1). 씬이 있는 휴식 칸은 `Node_Rest`로 간다 — 둘 다 `PartyState.ChangeHp` 하나를 부른다.

### 2.7 비전투 칸은 "들어오자마자 창" 씬이고, 골드는 스테이지 클리어 때 들어온다 — 0단계에서 완료

**씬.** `Node_Rest` · `Node_Shop` · `Node_Event`에는 카메라와 `NodeSceneController` 하나뿐이다.
그림이 아직 없어서 `Start`에서 창을 코드로 짓는다(`NodeWindows.cs`). 그림이 생기면 씬에 배경을 깔고
창은 그대로 그 위에 뜬다 — 흐름은 안 바뀐다.

| 칸 | 들어오면 | 끝내는 버튼 |
| --- | --- | --- |
| 휴식 | **즉시** 파티 전원 체력 +30%(`NodeRules.RestHealRatio`), 결과 창 | [계속] |
| 상점 | 카드 세 장(황금 20%) · 일반 50 / 황금 100 골드. 산 칸은 "구매함"으로 잠김 | [나가기] |
| 이벤트 | `RunEventAsset` 후보 중 하나를 뽑아 글 + 선택지. 고르면 결과 글 | [계속] |

- **칸을 끝내는 곳은 `NodeSceneController.Leave` 한 군데다.** 지금은 일렬 진행이라 `GoToNextStage`(마지막이면 메인 메뉴)를 부르고, 3단계에서 이 한 줄이 `CompleteNodeAndReturnToMap`이 된다.
- **이벤트 결과는 칸 셋(골드 · 체력 · 카드)의 조합이다.** 이벤트마다 스크립트를 짜지 않는다. 애셋 셋: 버려진 야영지 · 떠돌이 상인 · 낡은 샘(`Assets/Data/Events/`).
- **골드가 모자란 선택지는 잠긴다.** 선택지가 전부 잠기면 [계속]을 낸다 — 빈털터리가 갇히지 않게. 저작 검사도 "골드 0으로 고를 수 있는 선택지가 하나는 있다"를 본다.
- **상점 · 이벤트 카드는 죽은 동료의 직업을 뺀다**(`NodeRules.AvailableRoles`). 전투 씬은 몸을 보고 거르지만 여기엔 몸이 없어 편성 + 사망 기록으로 같은 답을 낸다.
- 체력을 깎는 이벤트도 **산 몸을 0으로 떨어뜨리지 않는다.** 이벤트가 동료를 죽이는 통로가 되면 안 된다.

**골드.** `RunProgression.Gold` — 경험치와 **따로** 둔다. 한 주머니면 상점에서 산 만큼 레벨업 황금 확률이 줄어드는 교환이 생긴다.

```
클리어 보상(n, 칸) = (40 + 10 × (n − 1)) × 배율     n = 층 번호, BattleSceneController.HandleVictory 에서 한 번
배율               = 정예 150% · 나머지 100%          (6항에서 정예 칸의 대가로 정했다)
카드 값            = 일반 50 · 황금 100
```

`GoldRules.StageClearReward(floorNumber, kind)` — **칸 종류를 기본값 없이 받는다.** 번호만 받는 오버로드를 남기면
정예 칸에서 그걸 부르는 자리가 조용히 일반 보상으로 돈다.

1스테이지 보상으로는 카드 한 장을 못 산다 — 매 판 한 장씩 사지면 상점이 레벨업 화면의 복사본이 된다.
판정이 난 순간 한 번 주므로, 지고 재시작해서 이긴 판도 한 번만 받는다. [처음부터]는 `Run.Reset`이 0으로 되돌린다.

---

## 3. 단계

각 단계는 컴파일되고 게임이 도는 상태로 끝난다. **게임 흐름이 실제로 바뀌는 것은 3단계뿐이다.**

### ~~0단계 — 비전투 칸 씬과 골드~~ 완료

지도보다 먼저 했다. 지도 없이도 도는 부분이라 순서를 당겨도 되돌릴 것이 없다.

- 코드: `RunProgression.Gold` · `GoldRules` · `NodeRules` · `PartyState.ChangeHp` · `RunEventAsset`(+ `RunEventChoice` · `RunEventRules`) ·
  `NodeSceneController`(+ `NodeSceneKind`) · `NodeWindows`(휴식 · `ShopWindow` · `EventWindow`) · `SceneNames.Node*` · `UiLayer.NodeWindow` ·
  `BattleSceneController.HandleVictory`의 골드 지급.
- 씬: `Assets/Scenes/Node/Node_Rest|Shop|Event.unity` — Build Settings 등록.
- 데이터: `Assets/Data/Events/Event_Camp|Merchant|Spring.asset` — 이벤트 씬 후보로 꽂힘.
- 테스트: `NodeRulesTests`(골드 수치 · 지출 · 직업 거르기 · 몸 없는 체력 · 이벤트 적용),
  `NodeSceneWiringTests`(빌드 목록 · 씬마다 컨트롤러 하나와 종류 · 리스너 하나 · 이벤트 후보가 고를 수 있는 상태).

**지금 흐름에서 해 보는 법.** Boot 씬 `GameManager.stageScenes`에 `Node_Shop` 같은 이름을 스테이지 사이에 끼워 넣는다.
단독 실행도 된다 — 씬만 열고 Play하면 창이 뜨고, 나가기를 누르면 같은 씬이 다시 열린다(골드가 0이라 상점은 전부 잠겨 보인다).

### ~~1단계 — 지도 모델 · 생성기 · 진행~~ 완료 (순수 코드, 게임 변화 없음)

**계획과 달라진 점.**
- **레시피 애셋 둘(`Assets/Data/Map/RunMap_Main` · `RunMap_Debug`)을 2단계가 아니라 여기서 만들었다.** 1,000시드 테스트가
  코드로 지은 가짜 레시피가 아니라 실제로 들어갈 값을 봐야 의미가 있어서다.
- 생성 입구는 `RunMapGenerator.TryGenerate(floors, seed, out map, out issues)` — 레시피 검사 → 굴림(최대 `MaxAttempts` 32번) → 지도 검사 순.
  굴린 결과도 `RunMapRules.Issues`로 한 번 더 본다. **후보를 거르는 함수(`CanFollow`)와 검사하는 함수가 같아** 둘이 어긋날 수 없다.
- 레시피 문제와 지도 문제를 enum 하나(`RunMapProblem`)로 묶었다. `StageWaveBoard`의 `BoardProblem`이 지점 표와 웨이브 목록을 한 enum에 둔 것과 같다.
- 같은 층에 같은 씬이 두 번 서는 것은 **될 수 있으면 피하되 불변식은 아니다** — 막히면 물러선다.
- 이웃 간선 확률 `ExtraEdgeChance = 0.4`. 에디터에서 굴려 본 실제 레시피: 1,000시드 실패 0 · 서로 다른 지도 715개 · 전부 갈림길이 하나 이상.
- `EncounterModifier` · `EncounterModifierRules`는 계획대로 `StageEncounter.cs` 끝에 있다. 디렉터는 아직 안 읽는다(5단계).

테스트: `RunMapGeneratorTests`(**검사기가 손으로 틀리게 만든 지도를 잡는지 먼저** · 시드 결정성 · 넓은 레시피와 실제 레시피 둘 다 1,000시드 ·
일렬 레시피 · 불가능한 레시피의 실패) · `RunMapProgressTests` · `RunMapRecipeTests`(틀린 레시피 하나 = 문제 하나) · `EncounterModifierRulesTests`.

아래는 원래 계획이다.

- `Assets/Scripts/Progression/RunMap.cs` — `MapNodeKind` · `MapNode` · `RunMap` · `FloorRule` · `NodeCandidate` ·
  `RunMapGenerator` · `RunMapRules`(불변식 검사) · `RunMapProgress`. MonoBehaviour가 없어 한 파일에 둔다(파일 수 줄이기 방침).
- `Assets/Scripts/Progression/RunMapRecipe.cs` — ScriptableObject라 **자기 이름의 파일이 필요하다**(Refactor_Master_Plan 0항).
  `Floors` → `FloorRule[]`, `Issues()` — 빈 층 · **씬 없는데 `Rest`가 아닌 칸**(2.1) · 마지막 층이 보스 한 칸이 아님 · 가중치 0.
  씬 달린 휴식 칸은 정상이다.
- `RunMapProgress`: `Choices()` · `TrySelect(id)` · `CompletePending()` · `IsFinished` · `FloorNumber` · `Pending` · `Current`.
  `TrySelect`는 `Choices`에 없는 칸과 `pending`이 이미 있는 상태를 거절한다.

테스트 `Assets/Editor/Tests/`
- `RunMapGeneratorTests` — 같은 시드 → 같은 지도 / 다른 시드 → 다른 지도(적어도 한 쌍) /
  2.3 불변식 여섯을 시드 0~999 전부에서 / 층별 칸 수가 `[min, max]` 안 / 폭 1 레시피는 일렬이 된다.
- `RunMapProgressTests` — 시작 전 선택지는 0층 / 간선 밖 칸 선택 거절 / `pending` 중 재선택 거절 /
  `CompletePending` 후 선택지가 그 칸의 `Next` / 마지막 층 완료 → `IsFinished`.
- `RunMapRecipeTests` — 잘못된 레시피가 각 문제를 정확히 하나씩 낸다.
- `EncounterModifierRulesTests` — `None`은 저작 그대로 / Elite는 0·3·6번째 / **보스는 절대 안 뒤집힌다** / 저작 elite는 보정과 무관하게 유지.
  (`EncounterModifier` · `EncounterModifierRules`는 `Stage/StageEncounter.cs` 끝에 둔다 — 조우에 걸리는 속성이다.)

### ~~2단계 — 지도 씬과 화면~~ 완료 (게임 변화 없음)

**계획과 달라진 점.**
- **칸 버튼 프리팹을 안 만들고 코드로 짓는다.** 아직 칸 그림이 없고, 0단계 창들(`NodeWindows`)도 같은 방침이다. 아이콘이 생기면 `RunMapScreen.DrawNode` 한 곳만 바꾼다.
- **가로 지도다 — 왼쪽이 0층, 오른쪽이 보스.** 전투 씬도 오른쪽 벽으로 걸어 나가 다음으로 가므로 방향을 맞췄다. 한 층 안에서는 0열이 위, 방향키는 **위/아래**로 선택지를 오간다.
- **판단은 순수 규칙으로 뗐다** — `RunMapViewRules`(칸 상태 6 · 선 상태 4 · 방향키 순서 · 설명 글) · `RunMapLayout`(좌표). 화면은 방향키 순서를 직접 정렬하지 않고 `NavigationOrder`를 그대로 쓴다.
- **미리보기는 걸어갈 수 있다.** 계획은 "클릭은 로그만"이었는데, 누르면 그 칸을 끝낸 것으로 치고 다음 층이 열린다. [다시 굴리기]가 새 시드로 바꾼다 — 6단계 레시피 조정이 이 화면 하나로 된다.
- 칸을 고르면 아래 설명줄에 수치가 뜬다(휴식 30% · 정예 3기마다 1기). 옛 4단계의 남은 일이 여기서 끝났다.
- **배치 한계: 9층 · 한 층 6칸.** 넘으면 칸이 겹친다(`RunMapLayout.MaxFloors/MaxColumns`, 테스트가 이 값으로 겹침을 본다).

**3단계가 붙일 자리.** `RunMapScreen.Start`가 지금은 늘 `StartPreview`를 부른다. 3단계에서 `GameManager`가 진행을 들고 있으면
`Show(gm.Progress, node => gm.EnterNode(node.Id, SceneNames.RunMap))`를 부르고, 없을 때만 미리보기로 간다.

테스트: `RunMapViewRulesTests`(갈림길을 고르면 반대편이 닫힌다 · 들어가 있으면 아무것도 못 고른다 · 선 넷 · 방향키 순서 · 배치 방향과 겹침 · 설명 수치) ·
`RunMapSceneWiringTests`(파일명 = `SceneNames.RunMap` · 빌드 목록 · 화면 하나와 쓸 수 있는 레시피 · 리스너 하나).

아래는 원래 계획이다.

- `SceneNames.RunMap = "RunMap"`.
- `Assets/Scripts/FlowScene/RunMapScreen.cs` — 씬 캔버스에 붙는다. 칸은 버튼 프리팹 하나를 층 · 열 좌표로 늘어놓고,
  간선은 늘인 `Image`로 긋는다. 지나온 칸 · 고를 수 있는 칸 · 못 가는 칸을 색으로 가른다.
- **씬 단독 실행이면 `debugSeed` 레시피로 미리보기 지도를 그린다.** 레시피를 조정할 때 게임을 처음부터 안 켜도 되게 —
  스테이지 씬 단독 실행과 같은 목적이다. 클릭은 로그만 찍는다.
- 입력은 `MainMenu`와 같은 `Button` + EventSystem. 키보드는 버튼 `Selectable.navigation`을 **간선에서 계산해 명시로** 넣는다
  (자동 내비게이션은 화면 좌표로 이웃을 골라 간선이 아닌 칸으로 튄다).
  `onClick` 콜백 안에서는 `this`를 안 만진다 — 전환 완료 시점엔 이 컴포넌트가 이미 파괴돼 있다(`MainMenuController` 주석과 같은 함정).

**씬 작업 (사람이 에디터에서)**
1. ~~`Assets/Scenes/RunMap.unity` 생성, 캔버스 + `RunMapScreen` + 칸 버튼 프리팹 연결.~~ 카메라 + 리스너 + `RunMapScreen`(미리보기 레시피 `RunMap_Main`)로 만들었다. 캔버스는 코드가 짓는다.
2. ~~Build Settings에 `RunMap` 추가.~~ 완료.
3. ~~`Assets/Data/Map/RunMap_Main.asset`(2.2 표), `RunMap_Debug.asset`(폭 1) 생성.~~ 1단계에서 만들었다.

### ~~3단계 — 흐름 전환: GameManager가 지도를 든다~~ 완료

**계획과 달라진 점 · 더한 것.**
- `StartNewRun`이 **`bool`을 돌려준다.** 레시피가 없거나 굴리지 못하면 에러를 남기고 거짓 — `MainMenuController`는 거짓이면 전환을 안 한다.
  로그에 시드와 `Map.Describe()`가 한 줄로 찍힌다.
- **지도에는 ESC가 없어서 좌상단에 [메인 메뉴] 버튼**을 뒀다(미리보기에서는 같은 자리가 [다시 굴리기]).
- **BGM:** 지도 · 비전투 칸은 메뉴 곡, 전투 · 보스 칸은 전투 곡. 칸 종류로 가른다(`RunMapRules.IsBattle`).
- `EndRun`이 지도도 비운다. 메인 메뉴로 나간 뒤 지난 런의 진행이 남지 않는다.
- 지도를 안 거치고 전투 · 비전투 씬이 뜨면(들어가 있는 칸 없음) 경고를 남긴다. 전투는 이기면 전체 클리어로, 비전투는 [나가기]가 메인 메뉴로 — 갇히지 않게.
- `GameManager.Configure(recipe, seed)` — 테스트 이음매.
- Boot 씬 `GameManager.recipe` = `RunMap_Main`, `debugSeed` = 0. 옛 `stageScenes` 칸은 씬 파일에서 사라졌다.
- `BattleRestartUI` 확인 문구 "첫 스테이지로 돌아가며" → "새 지도로 다시 시작하며". `FlowScene/README.md` 흐름도 갱신.
- `CurrentNodeModifier`는 들어왔지만 **디렉터는 아직 안 읽는다** — 정예 칸은 지금 일반 전투와 똑같이 돈다(5단계).

테스트: `GameManagerStageTests` 삭제 → `GameManagerRunMapTests`(런 열기 · 레시피 없음/깨짐 거절 · 고정 시드 · 새 런 초기화 ·
전환 거절 시 진행 불변 넷 · 씬 없는 휴식이 제자리에서 끝남 · 보스 칸에서 `HasNextNode` 거짓 · 칸 종류 보정 · `EndRun`).
`RunMapSceneWiringTests`에 Boot 레시피 배선 · 레시피 씬 이름이 전부 빌드 목록에 있는지 추가.

아래는 원래 계획이다.

`GameManager`
- 지운다: `stageScenes` · `DefaultStages` · `DebugStages` · `CurrentStageIndex` · `StageAt` · `NextStageScene` · `AdvanceStage` · `GoToNextStage`.
- 더한다: `[SerializeField] RunMapRecipe recipe` · `debugSeed` · `Map` · `Progress` ·
  `HasNextNode`(=`!Progress.IsFinished` 이고 pending이 마지막 층이 아님) · `FloorNumber` · `CurrentNodeModifier`.
- `StartNewRun` — 레시피가 없거나 `Issues()`가 있으면 **에러를 내고 런을 안 연다.** 빈 목록을 기본값으로 채우던 옛 폴백은 되살리지 않는다.
- `EnterNode(int id, string fromScene)` — **`CanSwap()`을 먼저 보고 나서** `TrySelect`. 순서를 뒤집으면 전환이 거절됐을 때
  `pending`만 박힌 채 지도에 남아 아무 칸도 못 고른다(`BattleSceneController.GoToNextStage` 주석의 "접수 먼저, 치우기 나중"과 같은 규칙).
  **씬 칸이 비었으면**(2.1 — 휴식만) 씬을 안 열고 `PartyState.ChangeHp` 후 `CompletePending`. 종류로 가르지 않는다.
- `CompleteNodeAndReturnToMap(string fromScene)` — `CanSwap` → `CompletePending` → `Swap(RunMap, fromScene)`.
- `RestartCurrentStage` — `Progress.Pending`의 씬을 다시 연다.
- `RestartRun` — `StartNewRun` 후 **지도 씬으로** 간다(첫 전투로 바로 가지 않는다).

`MainMenuController.OnStartClicked` — 첫 스테이지 대신 `SceneNames.RunMap`을 연다. 메뉴 BGM 유지.

`BattleSceneController`
- `HasNextStage` → `HasNextNode`, 출구 안내 문구 `"다음: 지도"`.
- 출구 도달 → `CapturePartyState()` → `CompleteNodeAndReturnToMap`. 체력 기록 시점은 지금과 같다.
- 마지막 층(보스) 승리 → 지금처럼 `ShowAllClear`.
- 로그의 `CurrentStageNumber/StageCount` → `FloorNumber/FloorCount`.
- 골드 보상의 번호도 `FloorNumber`로 — 층이 깊을수록 많이 받는다.

`NodeSceneController.Leave` — `GoToNextStage` / `ReturnToMainMenu` 갈래가 **`CompleteNodeAndReturnToMap` 한 줄**이 된다.
마지막 층은 보스 전투라 비전투 칸에서 "갈 곳이 없는" 경우가 사라진다.

테스트
- `GameManagerStageTests` → **`GameManagerRunMapTests`로 다시 쓴다.** 옛 테스트는 지워진 API를 부른다.
  새 런 → 0층 선택 가능 / SceneLoader 없으면 `EnterNode` 거절 **및 pending 불변** / `RestartRun`이 새 지도 /
  `debugSeed` 고정이면 같은 지도 / 레시피 없으면 런이 안 열린다.
- `SceneCompositionTests`에 추가 — Boot의 `GameManager`에 `recipe`가 꽂혀 있다 /
  레시피 두 개의 모든 씬 이름이 Build Settings에 있다 / `RunMap` 씬이 Build Settings에 있다.
  **씬 이름은 문자열이라 컴파일에 안 걸린다** — 지점 이름을 보드가 검사하는 것과 같은 이유다.

### 4단계 — ~~휴식 칸~~ 0단계로 흡수

`PartyState.ChangeHp`와 `NodeRules.RestHealRatio`가 0단계에서 들어왔다. 남은 일은 지도 화면이 칸 설명에
회복 수치를 보여 주는 것뿐이라 2단계에 붙인다.

### ~~5단계 — 정예 칸: 디렉터가 보정을 읽는다~~ 완료

계획 그대로다. `StageDirector.modifier`를 `Awake`에서 한 번 읽고, `QueueFromWalls` · `QueueInRoom` · `TickReinforcements` 세 곳이
`EncounterModifierRules.IsElite`를 부른다. `EnemySpawnService.Spawn`을 부르는 곳이 디렉터 한 군데뿐인 것도 확인했다 — 다른 소환 경로는 없다.

- 줄 번호는 **웨이브마다 0부터** 센다. 적 세 기짜리 웨이브면 웨이브마다 한 기가 강화다.
- 증원은 **증원 번호(1부터)** 로 센다 — 3번째 · 6번째 … 증원이 강화. 첫 증원이 곧장 강화로 나오지 않게 했다.
- 시작 로그에 보정이 찍힌다: `(웨이브 3개, 3기마다 강화)` / `(웨이브 3개, 보정 없음)`.

**강화 개체 표시 — 뒤이어 완료.** 처음엔 이름(`_강화`)과 수치만 달라 "왜 이 전사만 안 죽지"로만 느껴졌다.

| 채널 | 무엇 | 왜 여기 |
| --- | --- | --- |
| **발밑 고리** (`EliteAura`, `[BattleVfx]`에 붙음) | 보라(#B359FF) 고리, 테두리 진하고 안은 옅게, 느리게 맥동(1.1Hz) | 몸 색은 전투 상태 · 예고 · 아머 · 디버프가, 머리 위는 상태 글자 · 차지 게이지 · 상태 막대가 이미 쓴다. **바닥만 비어 있다.** 바닥의 기존 색(하늘 · 빨강 · 주황 · 연두)과도 안 겹친다 |
| **적 체력 HUD 이름** | `강화 · Warrior`, 이름 글자도 같은 보라 | 고리는 모양 신호라 "강화"라는 뜻은 글자가 전한다(색약 대비 이중 부호화 — 상태 표시 스펙과 같은 원칙) |

- **몸집은 안 키웠다.** `BeltScrollView.MultiplyBodyScale`로 키울 수는 있지만 판정(루트 콜라이더)은 그대로라, 커 보이는 가장자리를 때렸는데 안 맞는다.
- 벽 뒤에 숨겨 둔 채 걸어 나오는 동안(정렬 오프셋이 `HiddenSortingOffset`)은 고리를 안 그린다 — 경계 밖 허공에 원만 뜬다.
- 불릿타임에도 맥동한다(unscaled 시간). 점프해도 그림자처럼 바닥에 남는다.
- `Enemy.IsElite` 추가(`ApplyEliteScale`이 켠다). 오브젝트 이름 규칙은 `EliteMarkRules.ObjectName`/`HudName` 한 곳 — 소환 창구와 HUD가 같은 접미사를 읽는다.
- 테스트 `EliteMarkRulesTests` — 그리는 조건(시체 · 꺼짐 · 벽 뒤) · 맥동 범위 · 이름 왕복 · 바닥 빨강과 다른 보라 대역.

아래는 원래 계획이다.

- `StageDirector.Awake`에서 `modifier = GameManager.Instance != null ? GameManager.Instance.CurrentNodeModifier : EncounterModifier.None`.
- `QueueFromWalls` · `QueueInRoom` · `TickReinforcements`에서 `elite` 칸을 `EncounterModifierRules.IsElite(...)`로. 세 곳 **전부**다 —
  증원 하나를 빠뜨리면 그 경로만 저작 그대로 돈다(Room_Size_Plan 2.3이 경고한 그 두 번째 호출 자리).
- `Start` 로그에 보정값을 같이 찍는다.

테스트는 1단계 `EncounterModifierRulesTests`가 규칙을 이미 본다. 디렉터는 씬이 필요해 눈으로 본다(4항).

### 6단계 — 레시피 조정 (사람이)

지도 씬 단독 실행으로 시드를 바꿔 가며 모양을 본다. 1단계 1,000시드 테스트가 초록인 한 값은 자유롭게 바꾼다.

---

## 4. 눈으로 확인할 것

테스트로 못 잡는 것만.

2단계(지도 씬 단독 실행 미리보기)

- [ ] 왼쪽 1층 칸 하나만 밝고 테두리가 둘러져 있다. 나머지는 어둡다
- [ ] 칸을 누르면 그 칸이 회색(지나옴)이 되고, 이어진 다음 칸들만 밝아진다. 안 고른 갈래의 칸 · 선은 거의 안 보이게 닫힌다
- [ ] 지나온 선은 금색, 지금 고를 수 있는 칸으로 가는 선은 흰색이다
- [ ] 방향키 위/아래가 **고를 수 있는 칸끼리만** 오가고, 테두리와 아래 설명줄이 따라 움직인다. Enter로 고른다
- [ ] 빈 곳을 마우스로 눌러도 방향키가 계속 먹는다
- [ ] 칸 글자(종류 + 씬 이름)가 칸 밖으로 안 넘친다. 층 이름(1층 … 보스)이 맨 위 제목과 겹치지 않는다
- [ ] [다시 굴리기]로 모양이 바뀌고, 제목의 시드 숫자가 바뀐다. 보스까지 가면 "미리보기 끝" 글이 뜬다

3단계 이후 (Boot 씬부터 Play)

- [ ] [시작] → 페이드 → 지도가 뜨고, 0층 칸 하나만 고를 수 있게 보인다. 제목에 "1층 / 5층 · 골드 0", 좌상단에 [메인 메뉴]
- [ ] 콘솔에 `[GameManager] 새 런 — 시드 N` 과 지도 한 줄이 찍힌다
- [ ] 지도 BGM은 메뉴 곡, 전투 칸에 들어가면 전투 곡으로 바뀐다
- [ ] 지도 [메인 메뉴] → 메인 메뉴. 다시 [시작]하면 다른 지도다
- [ ] 선이 칸 가운데에서 가운데로 이어지고, 교차 없이 읽힌다
- [ ] 방향키로 칸을 옮기면 **간선을 따라** 움직인다 (화면상 옆 칸으로 튀지 않는다)
- [ ] 전투를 이기고 출구를 넘으면 지도로 돌아오고, 방금 칸이 "지나옴"으로, 다음 칸들이 "고를 수 있음"으로 바뀐다
- [ ] 지도에서 다음 전투에 들어가도 체력 · 동료 사망 · 런 덱 · 레벨이 이어진다
- [ ] 패배 → [이 스테이지 재시작] 이 같은 칸의 같은 씬을 연다
- [ ] 휴식 칸을 고르면 회복되고 다음 층이 열린다. 다음 전투에서 체력바가 오른 값으로 시작한다

0단계(지금 흐름에서 `stageScenes`에 끼워서, 또는 씬 단독 실행)

- [ ] 스테이지 클리어 안내에 "골드 +40"이 뜨고, 2층은 +50이다
- [ ] 정예 칸 클리어 안내는 `골드 +90 (정예 x1.5) · 지도로`(3층 기준)이고, 글자가 안내 칸 밖으로 잘리지 않는다
- [ ] 지도에서 정예 칸에 커서를 두면 설명줄 끝에 `클리어 골드 x1.5`가 보인다
- [ ] 상점: 카드 세 장이 가운데 한 줄로 서고, 값 글자가 카드 아래에 겹치지 않게 붙는다. 모자라면 값이 붉고 카드가 안 눌린다
- [ ] 상점: 사면 골드가 줄고 그 칸이 "구매함"이 된다. 다음 전투 손패 · 덱 상황판에 산 카드가 들어 있다
- [ ] 이벤트: 글이 패널 안에서 줄바꿈되고, 선택지 버튼과 겹치지 않는다. 골드가 모자란 선택지는 흐리고 "(골드 N 필요)"가 붙는다
- [ ] 이벤트: 고르면 선택지가 사라지고 결과 글 + [계속]만 남는다
- [ ] 휴식: 체력이 깎인 채 들어갔다 나오면 다음 전투 체력바가 30%p 올라 있다. 죽은 동료는 여전히 없다
- [ ] 세 창 모두 마우스 없이 방향키 + Enter로 고르고 나갈 수 있다
- [ ] 비전투 칸에서 나가면 페이드 후 다음 전투 씬이 뜨고 BGM이 끊기지 않는다
- [ ] 정예 칸 전투에서 대략 세 기 중 한 기가 `_강화` 이름으로 나오고 더 단단하다 (콘솔 `[StageDirector] … 3기마다 강화`, 하이어라키 이름으로 확인)
- [ ] 강화 개체 발밑에 보라 고리가 **바닥에 납작하게** 깔리고 천천히 숨 쉬듯 맥동한다. 그림자보다 크고, 발 · 그림자를 가리지 않는다
- [ ] 적이 겹쳐 선 난전에서도 어느 적이 강화인지 고리로 가려진다. 뒤에 선 강화 적의 고리가 앞 캐릭터 몸 위로 올라오지 않는다
- [ ] 강화 적을 때리면 하단 HUD 이름이 보라 `강화 · …`, 일반 적으로 바꿔 때리면 원래 색 · 이름으로 돌아온다
- [ ] 벽에서 걸어 나오는 강화 적(아레나)은 벽을 벗어난 뒤에 고리가 켜진다. 점프 · 공중 피격 중에도 고리는 바닥에 남는다
- [ ] 강화 적이 죽으면 고리가 바로 꺼진다
- [ ] 같은 씬을 일반 전투 칸으로 들어가면 `보정 없음`이고 `_강화`가 저작한 것 말고는 없다
- [ ] 보스 칸(Stage_05)의 보스는 강화로 안 나온다
- [ ] 증원이 붙은 웨이브에서 3번째 증원이 `(강화)`로 찍힌다
- [ ] 보스 칸 승리 → 전체 클리어 화면 → 메인 메뉴
- [ ] 우상단 [처음부터] → 새 지도(다른 모양)가 뜬다
- [ ] 스테이지 씬 단독 실행이 전과 똑같다 (승패 기능 없음, 강화 보정 없음)

---

## 5. 범위 밖 — 그리고 이음매

- **칸마다 조우 목록을 굴리기.** Stage_Encounter 7.2의 방향 그대로, 굴림은 "조우 목록을 채우는 단계"여야 한다.
  전제가 둘이다. ① 씬 지점 이름을 안 부르는 웨이브(`PointIds()`가 빈 것)만 씬을 넘나들 수 있다 — 이걸 검사하는 테스트가 먼저다.
  ② 보드 목록과 주입 목록 중 무엇이 이기는지 **폴백이 아니라 명시 규칙**으로 정해야 한다. 이번엔 칸 = 씬 + 보정으로 멈춘다.
- **층 깊이에 따른 수치 비례**(체력 +10%/층 등). 보정 구조체에 칸 하나 더하는 일이라 이음매는 있다. 기획이 원하면 그때.
- **상점 · 이벤트 그림.** 지금은 창만 뜬다. 배경 · 상인 스프라이트가 생기면 씬에 깔기만 한다(2.7).
- **상점의 카드 제거 · 새로고침, 이어지는 이벤트(선택 결과가 다음 이벤트를 연다).** 칸 셋 조합으로 안 되는 첫 결과라, 필요해지면 그때 `RunEventChoice`를 넓힌다.
- **골드 HUD.** 지금 골드는 클리어 안내와 상점 · 이벤트 창에서만 보인다. 전투 중에 볼 이유가 생기면 `PartyHealthHUD` 옆에 붙인다.
- **런 저장 · 이어하기.** 지금도 런은 메모리에만 있다. 지도는 시드 + 진행 두 값이라 저장이 쉬운 모양으로 뒀다.
- **정예 칸 추가 보상.** 아래 6항.

---

## 6. 정할 것

- **2층에서 아레나(Stage_02)를 건너뛸 수 있게 둘 것인가.** 2.2 표대로면 Stage_03을 고른 플레이어는
  "벽에서 나오는 적"을 보스 층에서 처음 본다. 막으려면 1층을 Stage_02 고정으로 두고 갈림길을 2층부터 연다.
- ~~**정예 칸의 대가.**~~ **정했다 — 클리어 골드 150%.** 판정 한 곳에서 한 번 주는 값이라 경험치 배율보다 건드릴 곳이 적다.
  - 3층 정예 90 vs 일반 60 — 차이가 카드 반 장 값(25) 이상이어야 "골라 볼 만한 칸"이다. `EliteBonus_IsWorthAtLeastHalfACard`가 지킨다.
  - 배율은 정수 백분율(`EliteClearRewardPercent`). 기본 보상이 10 단위라 소수 없이 떨어진다 — 바꿔서 잘림이 생기면 테스트가 잡는다.
  - 받는 순간이 보인다: 클리어 안내 `골드 +90 (정예 x1.5) · 지도로`. 고르기 전에도 보인다: 지도 설명줄 `… 클리어 골드 x1.5`.
  - `×` 대신 `x` — 기본 폰트에 글리프가 있다는 보장이 없다(`CardOfferView`의 "데미지 x1.5"와 같은 이유).
