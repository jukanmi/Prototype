# FlowScene — 씬 흐름

Boot 씬에 상주하는 매니저가 나머지 씬을 Additive로 갈아 끼운다.
씬 이름은 `Support/SceneNames.cs`, 전환은 `SceneLoader.SwapTo`, 런 단위 진행은 `GameManager`가 맡는다.
런타임은 전부 `Prototype` 네임스페이스다.

## 흐름

```
앱 실행 → Boot (매니저 상주, 언로드 안 됨)
            └→ MainMenu Additive 로드 (BootStrapper)

MainMenu [시작]        → Lobby (마을)
Lobby [출발]           → 지도 굴림(GameManager.StartNewRun) → RunMap
Lobby [타이틀로]       → MainMenu
RunMap 칸 선택         → 그 칸의 씬(Stage_* · Node_*). 휴식만 있는 칸은 씬 없이 그 자리에서 처리
전투 승리             → 바닥에 금화 · 상자(ClearLoot). 줍고 출구 → RunMap   (보스 층이면 런 종료 → Lobby)
                         방이 여럿이면 중간 방은 문만 열리고, 보상 · 출구는 마지막 방에 선다
전투 패배 [재시작]     → 같은 칸을 처음부터
전투 [처음부터]        → 런을 버리고 새 지도 → RunMap
Node_* 오른쪽 벽       → RunMap   (전투처럼 걸어 나간다. 싸움 이벤트에서 지면 [재시작] · [마을로])
ESC 일시정지 [마을로]  → 런 종료 → Lobby
```

런 지도 설계는 `docs/Run_Map_Plan.md`, 마을은 `docs/Lobby_Plan.md`.

"처음부터 재생"은 리셋 코드 없이 **씬을 새로 로드하는 것만으로** 보장된다.
씬 파일에 저장된 초기 배치가 그대로 복원되기 때문이다.
씬 경계를 넘어 살아남는 것(`GameManager` 런 데이터, `TimeControl.Scale`, `BattleRegistry`)만 명시적으로 되돌린다 —
`GameManager.RestoreTime`, `BattleSceneController.ClearSceneStatics`.

## 파일

| 파일 | 역할 |
|---|---|
| `Support/SceneNames.cs` | 씬 이름 상수. `Battle` = `"SampleScene"` |
| `Components/SceneLoader.cs` | 씬의 **존재 자체**. Additive 로드/언로드 + 페이드 + `IsBusy` 잠금 |
| `Components/GameManager.cs` | 런 단위 데이터와 런 지도 위의 진행. 씬 오브젝트는 절대 참조하지 않는다. 마을에서 편성이 바뀌면 시작 덱을 다시 뿌린다(`RunProgression.ReseedDeck`) |
| `Components/AudioManager.cs` | BGM/SFX 골격. 클립이 비어도 예외 없이 넘어간다 |
| `Components/BootStrapper.cs` | Boot 씬에서 최초 1회 MainMenu 를 띄운다 |
| `Components/MainMenuController.cs` | 타이틀 버튼. [시작]은 마을로 간다 |
| `Components/LobbyController.cs` | 마을 흐름. 마을 오브젝트가 부르는 공개 메서드(출발 · 편성 · 설정 · 제단 · 기록판). 총공격 편성 문(`IAssaultEditGate`)을 늘 연다 |
| `Components/Interactable.cs` | 씬에 놓는 상호작용 지점(다가가서 F). 결과는 인스펙터의 `onInteract`로 고른다. 씬 컨트롤러가 찾아다니지 않는다. 누가 불렀는지는 `Current` |
| `Components/Npc.cs` · `DialogueWindow.cs` | Interactable에 붙이는 대사와 화면 아래 대화창(위에 NPC 그림). 대사를 끝까지 읽으면 `onInteract`가 이어진다. `Ask`는 같은 창에 선택지를 띄운다 |
| `Support/KeyMenu.cs` · `Components/KeyMenuHost.cs` | 창 안의 키보드 커서 — 방향키 · WASD 이동, F · Enter 결정, 금색 테두리. 상점 · 이벤트 선택지가 쓴다 |
| `Support/ClearLoot.cs` · `Components/LootPickup.cs` · `SkillTooltip.cs` · `Data/LootArt.cs` | 전투 칸 클리어 보상. 바닥에 금화(골드) · 상자가 떨어지고, 상자를 열면 스킬 두루마리 셋 중 하나를 줍는다. 두루마리에 다가가면 설명 창 |
| `Components/PartySelectUI.cs` | 마을 게시판의 파티 편성 |
| `Components/RunMapScreen.cs` · `Support/RunMapView.cs` | 지도 화면과 그 계산 |
| `Components/BattleSceneController.cs` | 전투 씬 진행 — ESC 일시정지, 승패 판정. 전환은 `GameManager`가 한다. 이긴 뒤에만 총공격 편성 문(`IAssaultEditGate`)을 연다 |
| `Components/NodeSceneController.cs` · `Support/NodeWindows.cs` | 상점 · 이벤트 씬. 걸어다니는 방이고, 상호작용 지점이 `OpenShop`(대화창 위에 뜨는 창, 대화창엔 조작 설명) · `OpenEvent` · `Rest`(대화창)를 부른다. 이벤트 칸은 선택지(`Node_Event`) · 싸움(`Node_Event_Fight`) · 모닥불(`Node_Event_Camp`, 옛 휴식) 세 벌 — 싸움 이벤트의 검객은 정예이고, 예고(멈춤 · "!" · 바닥 원) 뒤 제자리에서 회전 베기 패턴을 쓴다(`Enemy_Duelist` 데이터 · 프리팹). 출구선은 방(`StageRoom`) 오른쪽 벽에서 푼다. 싸움이 없을 때 총공격 편성 문(`IAssaultEditGate`)을 연다 |
| `Components/PauseMenu.cs` | 일시정지 창(ESC). 전투는 [마을로], 마을은 [타이틀로] |

이 폴더 밖에 있지만 흐름의 일부인 것들:

| 파일 | 역할 |
|---|---|
| `Stage/Support/StageOutcomeRules.cs` | 승패 판정 규칙 |
| `Battle/Components/UI/StageResultUI.cs` | 스테이지 종료 화면 (출구 화살표 / 전부 클리어 / 패배) |
| `Core/Support/UiKit.cs` | 코드로 캔버스 짓는 공용 조각 + `UiLayer` 정렬 순서표 |

## 지켜야 할 제약

**Boot 씬에 화면을 그리는 Camera 를 두지 않는다.** 다른 씬이 각자 카메라를 갖고 있어
Boot 에도 있으면 Additive 상태에서 화면이 겹친다. `FadeCanvas` 는 Screen Space - Overlay 라
카메라 없는 순간에도 정상 렌더링된다. 예외는 `FallbackCamera` 하나다 — 아래 참조.

**EventSystem 은 Boot 씬에 하나만.** 중복되면 콘솔 경고와 함께 UI 입력이 불안정해진다.

**Boot 씬은 언로드되지 않으므로 `DontDestroyOnLoad` 를 호출하지 않는다.**

**`SetActiveScene` 은 필수.** Additive 로드 후 활성 씬을 지정하지 않으면 `Instantiate` 로
만든 오브젝트가 Boot 씬에 생성된다. 그러면 전투 씬을 언로드해도 몬스터 · 이펙트가 남아
다음 판에 유령처럼 나타난다. `SceneLoader` 가 맡는다.

**`GameManager` 에 전투 로직이나 씬 오브젝트 참조를 넣지 않는다.** 씬이 언로드되는 순간
그 참조는 전부 무효가 되고, 원인 찾기 어려운 null 에러로 돌아온다.

## FallbackCamera

`SceneLoader` 는 이전 씬을 먼저 내리고 새 씬을 나중에 얹는다(메모리 피크를 낮추려는 순서).
그 사이 로드된 씬의 카메라가 0개가 되어 Game 뷰에 `No cameras rendering` 이 뜨므로,
Boot 에 아무것도 그리지 않는 카메라를 하나 둬서 없앴다. 화면 겹침을 일으킬 수 없는 카메라다.

| 설정 | 값 | 이유 |
|---|---|---|
| `cullingMask` | `Nothing` (0) | 어떤 오브젝트도 그리지 않는다 → 겹칠 대상이 없다 |
| `depth` | `-100` | 씬 카메라가 항상 그 위를 덮는다 |
| `clearFlags` | Solid Color | 카메라 없는 순간을 단색으로 채운다 |
| 태그 | **Untagged** | `MainCamera` 로 바꾸면 `Camera.main`(BeltScroll · FlashSlashVFX)이 이 카메라를 잡을 수 있다. Boot 는 언로드되지 않으므로 전투 중에도 살아 있다 |
| `AudioListener` | **없음** | 씬 카메라 것과 중복되면 경고 |

"새 씬을 먼저 얹고 이전 씬을 나중에 내리는" 순서도 검토했지만, 그러면 그 순간
카메라와 AudioListener 가 둘씩 존재해 경고가 새로 생긴다. 폴백 카메라 쪽이 부작용이 적다.

## 시간 제어

전투 안의 시간 정지(스킬 컷인 등)는 `Time.timeScale` 이 아니라 **`TimeControl.Scale`** 만 쓴다
(결정 로그 ⑥ — 시간이 멈춘 동안에도 UI 와 연출은 돌아야 한다). 일시정지 창(`PauseMenu`)만 `Time.timeScale = 0` 을 쓴다.

`TimeControl.Scale` 은 static 이라 씬을 넘어 살아남는다. 시간이 멈춘 채 전투를 떠나면 다음 씬이 멈춘 채로 시작하므로,
전환할 때 `GameManager.RestoreTime` 이 `TimeControl.Reset()` 과 `Time.timeScale = 1` 을 함께 되돌린다.
