# 로비 계획 — 걸어다니는 마을 허브와 영구 진행

> 상태: **0 · 1 · 2 · 3 · 4단계 구현 완료 — 씬 재생 확인 대기**(5항 목록). 5단계는 보류.
> 구현 메모: 상호작용 키는 **F**(E는 불릿타임). 로비 씬은 `Stage_Training` 복제에 `LobbyController` + `Station_*` 5개.
> 정산 알림은 마을이 못 띄운 것을 합쳐 띄운다([처음부터] 뒤 지도에도 한 줄).
> 확정: **허브형(걸어다님)** · **영구 진행 넣음(재화: 기억 조각)** · **난이도는 지금 그대로 반복**(승천 · 순환 강화는 뒤로) · 수치는 임시값.
> 관련 코드: `Assets/Scripts/FlowScene/GameManager.cs`, `MainMenuController.cs`, `BattleSceneController.cs`,
> `NodeSceneController.cs`, `RunMapScreen.cs`, `PartySelectUI.cs`, `SettingsPanel.cs`, `SceneNames.cs`, `BootStrapper.cs`,
> `Assets/Scripts/Progression/Support/RunProgression.cs`(`GoldRules` · `NodeRules` · `PartyState`),
> `Assets/Scripts/Party/Support/PartyCatalog.cs`, `Assets/Scenes/Level/Stage_Training.unity`
> 관련: [story.md](story.md) — "심층을 빠져나와 로비 마을로", "마을에서 용병 고용과 재화 사용법", "마법사의 능력으로 마을에 긴급 귀환"

---

## 0. 무엇을 바꾸는가

지금 런과 런 사이에 머무는 곳은 **MainMenu**다. 시작 · 종료 버튼 위에 파티 선택 UI가 얹혀 있고,
런이 어떻게 끝나든 `GameManager.ReturnToMainMenu`로 여기 돌아온다. 런이 남기는 것은 없다.

바꾸고 나면 런과 런 사이는 **마을(Lobby 씬)**이다. 주인공이 걸어다니고, 오브젝트 하나가 기능 하나다.
런이 끝나면 클리어한 층만큼 **영구 재화**가 쌓이고, 마을에서 그걸 써서 다음 런을 조금 편하게 만든다.

```
지금:  Boot → MainMenu(시작 · 종료 + 파티 선택) → RunMap → 칸 … → 보스 → 결과 → MainMenu
이후:  Boot → MainMenu(타이틀) → Lobby → RunMap → 칸 … → 보스 → 결과 → Lobby
                                   ↑___________________________________________|
```

| 있는 것 | 지금 | 이번 계획에서 |
| --- | --- | --- |
| `MainMenuController.OnStartClicked` | `StartNewRun` 후 RunMap으로 | **Lobby로 간다.** 런은 로비의 출발 문이 연다 |
| `MainMenuController.Start` | `PartySelectUI.Create` | **지운다.** 편성은 로비의 게시판으로 옮긴다 |
| `GameManager.ReturnToMainMenu` | 런을 접고 MainMenu로 | **`ReturnToLobby`로 이름과 목적지를 바꾼다.** 부르는 자리 4곳(전투 · 비전투 · 지도 · 결과)은 손대지 않아도 같은 깔때기를 탄다 |
| `GameManager.EndRun` · `RestartRun` | 런 데이터 폐기 | 폐기 **전에** 정산(`SettleRun`) — 클리어한 층만큼 영구 재화 |
| `BattleSceneController.ClearStatics` | private | 로비도 같은 정리가 필요하다(허수아비를 때리면 `BattleRegistry`에 들어간다) → **공용 정적 함수로 뺀다** |
| 영구 저장 | 키 바인딩 `PlayerPrefs`뿐 | **JSON 파일 하나**(`JsonUtility`, `persistentDataPath/meta.json`) |
| 입력 `Interact` | 없음 | `Gameplay` 맵에 추가, 기본 F (E는 불릿타임) |
| 씬 `Lobby` | 없음 | **`Stage_Training` 복제.** 방 · 카메라 · 입력 · 파티 스폰 · 허수아비를 물려받고 `BattleSceneController`만 뺀다 |

한 줄로: **로비는 "적 없는 방 + 오브젝트 몇 개"이고, 영구 진행은 "런이 끝날 때 한 번 쓰고, 런이 시작될 때 한 번 읽는 파일"이다.**

---

## 1. 확정한 결정

### 1.1 허브형 — `Stage_Training`을 복제해서 만든다

훈련장 씬에는 이미 방(`Room` · 벽 · 바닥), 카메라, `BattleInput`, `PartySpawnPoint`, 허수아비(`Enemy_Dummy`)가 있다.
로비에 필요한 것 중 **걸어다니는 몸과 허수아비가 공짜로 따라온다.** 그래서 "허수아비를 로비 안에 둘지,
Stage_Training으로 보낼지"는 고민할 필요가 없다 — 복제하면 로비 안에 있다.

빼야 할 것은 `BattleSceneController` 프리팹 하나다. 남겨 두면 승패를 판정하고 ESC가 런을 접는다.
그 자리를 `LobbyController`가 맡는다(씬 진입 BGM · 전환 시 정리 · 오브젝트 배선).

### 1.2 영구 진행은 넣는다 — 수치는 자리만 잡는다

밸런스는 나중에 잡는다. 그래서 이번 계획의 수치(재화 지급량 · 강화 비용 · 효과량)는 **전부 한 곳(`MetaRules`)의 상수**로 두고,
고칠 때 그 파일만 열면 되게 한다. 난이도(적 체력 · 정예 확률)는 건드리지 않는다 — 매 런이 같은 난이도다.

### 1.3 런 정산은 깔때기 한 곳에서 — 끝난 방식과 상관없이 클리어한 층만큼

런이 끝나는 길은 넷이다: 전체 클리어 · 패배 후 나가기 · ESC 이탈 · [처음부터].
앞의 셋은 전부 `ReturnToMainMenu → EndRun`을 타고, [처음부터]는 `RestartRun → StartNewRun`을 탄다.
**`SettleRun(percent)` 하나를 만들어 `EndRun`과 `RestartRun` 양쪽에서 부른다.**
클리어 · 패배 · ESC 이탈은 구분하지 않는다 — 층마다 전투를 이겨야 쌓이므로 ESC로 빠져나가 이득 볼 길이 없다
(Hades가 죽어도 모은 걸 주는 것과 같다). **[처음부터]만 일부(`MetaRules.RestartPercent`, 임시 50%)를 준다** —
런을 스스로 버리는 버튼이라 전액을 주면 "나쁜 지도면 리셋"이 공짜가 된다.

```
기억 조각 = (클리어한 칸 수 × MetaRules.PerNode + 보스 클리어면 MetaRules.BossBonus) × percent / 100
            percent: EndRun 100 · RestartRun RestartPercent
```

처치 수는 지금 세는 곳이 없어서 기준에서 뺀다. 필요해지면 그때 더한다.

### 1.4 강화는 "이미 있는 이음매"에 거는 것만 1차로

효과를 걸 자리가 이미 있는 것부터 한다. 새 스탯 경로를 파는 강화는 1차에서 뺀다.

| 강화 | 거는 자리 | 비고 |
| --- | --- | --- |
| 시작 골드 +N | `StartNewRun`의 `Run.Reset()` 직후 `Run.AddGold` | 한 줄 |
| 상점 할인 N% | `GoldRules.PriceOf` | 할인율을 인자로 받거나 `MetaProgression.Current`를 읽는다 |
| 휴식 회복량 +N% | `NodeRules.RestHealRatio`를 쓰는 두 자리(`EnterNode` 씬 없는 휴식 · 휴식 창) | 상수 → 함수 |
| 동료 해금 | `PartySelectUI`가 명단을 그릴 때 잠긴 동료를 걸러 낸다 | story의 "용병 고용"이 이것이다 |
| ~~최대 체력 +10%~~ | `PartyAssembler.Start` 근처에 스탯 보정이 필요 | **2차.** 스탯 경로를 새로 파야 한다 |
| ~~불릿타임 게이지 시작치~~ | `TacticGauge` | **2차.** 같은 이유 |

### 1.5 해금 — 처음엔 기본 조합만 열려 있다

`PartyCatalog.Default()`(`Loadout_Default`)에 든 동료는 처음부터 열려 있다. 나머지는 로비에서 재화로 고용한다.
저장은 `PartyMemberData` 애셋 **이름**으로 한다(애셋 참조는 JSON으로 못 넘긴다).

story의 보스 영입(사슬수녀 · 궁수 — "처치 후 아군으로 합류")은 같은 해금 목록에 **다른 열쇠**로 들어가는 것뿐이다.
보스가 생기면 `SettleRun`에서 "이 보스를 이겼으면 이 동료 해금"만 더한다. 이번 계획에는 없다.

### 1.6 저장은 순수 C# 하나 + 파일 입출력 하나

```
MetaSave (직렬화용 [Serializable] 클래스 — 필드만)
├─ version        int     형식이 바뀌면 올린다
├─ currency       int     기억 조각
├─ unlocked       string[]  해금한 동료 애셋 이름
├─ upgrades       UpgradeLevel[]  { id, level }
├─ runs           int     지금까지 끝낸 런 수 (기록판 · NPC 대사 분기용으로 미리 둔다 — 한 줄이다)
└─ bestFloor      int

MetaProgression (GameManager가 소유, RunProgression과 같은 자리)
├─ Load() / Save()        persistentDataPath/meta.json, 실패하면 경고 남기고 새 판
├─ Currency · TrySpend · IsUnlocked · LevelOf(id)
└─ Settle(RunMapProgress) 런 정산 → Save()
```

`GameManager`는 Boot에 상주하므로 여기 두면 씬을 넘어도 산다 — `RunProgression`과 같은 이유다.
**저장 시점은 정산 직후와 구매 직후 둘뿐이다.** 매 프레임 · 씬 전환마다 쓰지 않는다.

파일이 깨졌을 때 조용히 새 판으로 덮으면 진행이 날아간다. **읽기 실패 시 원본을 `meta.json.bak`으로 옮기고** 새 판을 쓴다.

---

## 2. 로비 오브젝트

공통 부품은 **`LobbyInteractable` 하나**다: 플레이어가 반경 안에 들어오면 키 안내("E 편성")가 뜨고,
`Interact`를 누르면 `UnityEvent`(또는 자식 컴포넌트의 메서드)를 부른다. 창이 떠 있는 동안은 `GameplayModal`을 연다 —
전투 씬과 같은 규칙으로 이동 · ESC가 창에 먼저 간다.

| 오브젝트 (story 이름) | 하는 일 | 쓰는 코드 | 단계 |
| --- | --- | --- | --- |
| 미궁 입구 | 런 시작 → RunMap | `GameManager.StartNewRun` + `SceneLoader.SwapTo` (MainMenu에서 옮김) | 1 |
| 용병 게시판 | 동료 편성 + (3단계부터) 고용 | `PartySelectUI` 그대로 | 2 |
| 허수아비 | 콤보 연습 | `Enemy_Dummy` — 복제로 이미 있음 | 1 (공짜) |
| 마법사 | 설정 · 키 바인딩 | `SettingsPanel.Open()` | 2 |
| 유산 제단 | 재화로 영구 강화 | 새로 — `MetaUpgradeWindow` (`NodeWindows`의 상점 창과 같은 방식으로 코드로 짓는다) | 4 |
| 기록판 | 런 수 · 최고 층 | 새로 — 텍스트 한 장 | 4 (작다) |
| ~~카드 서가~~ | 동료별 카드 도감 | `DeckInspectorUI`는 전투 중 런 덱 전용이라 공개 진입점이 없다 | **뺀다.** 필요해지면 따로 |

이름(마법사 · 유산 제단)은 story에 맞춘 가칭이다. 코드 이름은 기능으로 짓는다(`SettingsTerminal`이 아니라 그냥 `LobbyInteractable`에 `SettingsPanel.Open` 연결).

---

## 3. 단계

### ~~0단계 — 공용 부품~~ 완료

1. `BattleSceneController.ClearStatics`의 몸통을 `SceneStatics.ClearBattle()`(정적)로 옮기고 원래 자리는 그걸 부른다.
   로비가 같은 정리를 쓴다. **동작은 바뀌지 않는다.**
2. 입력 `Gameplay/Interact`(F) 추가 — E는 불릿타임이라 못 쓴다. `RebindUI` 목록에 뜨는지 확인.
3. `LobbyInteractable` — 반경 판정 · 안내 문구 · `Interact` 시 이벤트. 안내 문구는 `UiKit`으로 월드 위에 짓는다.

### ~~1단계 — 껍데기 로비와 흐름 전환~~ 완료

1. `Stage_Training` 복제 → `Assets/Scenes/Lobby.unity`. `BattleSceneController` 프리팹 제거. Build Settings에 추가.
2. `SceneNames.Lobby` 추가.
3. `LobbyController` — 씬 진입 시 메뉴 BGM, 나갈 때 `SceneStatics.ClearBattle()`. 입구 오브젝트의 `Interact`에서
   기존 `OnStartClicked` 몸통(파티 검사 → `StartNewRun` → RunMap으로 `SwapTo`)을 그대로 옮겨 실행.
4. `GameManager.ReturnToMainMenu` → `ReturnToLobby`, 목적지 `SceneNames.Lobby`. 부르는 자리는 이름만 바뀐다.
   `BattleSceneController.ExitToMainMenu` · `RunMapScreen.ReturnToMainMenu`도 이름을 맞춘다.
5. `MainMenuController.OnStartClicked` → `StartNewRun` 없이 Lobby로 `SwapTo`. `PartySelectUI.Create` 줄 삭제.
6. 로비에서 타이틀로 가는 길: ESC → `SceneLoader.SwapTo(MainMenu, Lobby)`. `GameplayModal`이 열려 있으면 창이 먼저 먹는다.

**결과:** 타이틀 → 로비(걸어다님 · 허수아비) → 입구 → 지도 → … → 로비로 돈다. 편성은 아직 기본 조합.

### ~~2단계 — 게시판 · 마법사 연결~~ 완료

1. 게시판 `Interact` → `PartySelectUI` 띄우기. 지금은 `Create(this)`로 생성 즉시 떠 있는 방식이라
   **열기 · 닫기(`Show`/`Hide`)를 붙인다.** 닫을 때 `GameplayModal` 해제.
2. 마법사 `Interact` → `SettingsPanel.Open()`. 로비 씬에 `SettingsPanel`을 두거나 MainMenu의 것을 프리팹으로 뺀다 — 둘 중 씬 배선이 적은 쪽.

**결과:** MainMenu에 있던 기능 전부가 로비 오브젝트로 옮겨졌다.

### ~~3단계 — 영구 저장과 런 정산~~ 완료

1. `MetaSave` · `MetaProgression`(1.6) · `MetaRules`(재화 지급 · 강화 비용 · 효과량 상수).
2. `GameManager`가 `Awake`에서 `Meta.Load()`. `MetaProgression.Current`는 `RunProgression.Current`와 같은 방식(단독 실행이면 빈 판, 저장 안 함).
3. `SettleRun(percent)` — `EndRun`(100)과 `RestartRun`(`RestartPercent`) 앞에서 호출. `Progress`가 null이면(이미 정산했거나 런이 없음) 아무것도 안 한다 → 두 번 정산 방지.
4. 정산 결과를 로비에 들어올 때 한 줄 띄운다("기억 조각 +32"). [처음부터]는 전환 없이 지도로 가므로 지도 화면 상단에 같은 문구. 값은 `MetaProgression.LastSettlement`에 남겨 두고 로비가 읽고 지운다.

**결과:** 런이 끝날 때마다 재화가 쌓이고 게임을 껐다 켜도 남는다. 쓸 곳은 아직 없다.

### ~~4단계 — 유산 제단 · 해금 · 기록판~~ 완료

1. `MetaUpgradeWindow` — 강화 목록, 다음 단계 비용, [구매]. `TrySpend` 성공 시 `Save()`.
2. 효과 연결(1.4의 1차 넷): 시작 골드 · 상점 할인 · 휴식 회복 · 동료 해금.
3. `PartySelectUI` — 잠긴 동료는 회색 줄 + 비용 표시, 누르면 고용(`TrySpend` → `Unlock` → `Save`).
   `GameManager.Loadout`에 잠긴 동료가 남아 있으면(저장 파일을 지운 경우) 기본 조합으로 되돌린다.
4. 기록판 — `runs` · `bestFloor` 텍스트.

**결과:** 1회차 루프 완성. 런 → 재화 → 강화 · 고용 → 다음 런.

### 5단계 — 보류 (이번 계획 밖)

story에 있지만 지금 하지 않는 것. 자리만 적어 둔다.

| 항목 | 붙는 자리 |
| --- | --- |
| 튜토리얼(심층 탈출 → 마을) | 첫 실행(`runs == 0`)이면 로비 대신 튜토리얼 씬. `MetaSave`에 플래그 하나 |
| 보스 영입(사슬수녀 · 궁수) | `SettleRun`에서 보스 처치 → 해금 (1.5) |
| 긴급 귀환 대사 · NPC 대사 분기 | 로비 진입 시 `MetaSave` 플래그 · `runs`로 분기 |
| 순환 모드(유산 계승 · 회차별 강화) | `EncounterModifier`에 회차 보정. **지금은 난이도 고정이라 하지 않는다** |
| 최대 체력 · 게이지 강화 | 스탯 보정 경로가 생기면 (1.4) |

---

## 4. 정한 것

1. **재화 이름: 기억 조각.** 코드 이름은 `MetaCurrency`, 화면 문구만 "기억 조각".
2. **수치는 임시값으로 간다** — 밸런스는 나중에 같이 잡는다. 전부 `MetaRules` 상수:
   `PerNode` 10 · `BossBonus` 50 · `RestartPercent` 50 · 고용 100 · 강화 단계당 50 · 최대 3단계.
3. **[처음부터]는 일부만 준다**(1.3, `RestartPercent`).

---

## 5. 눈으로 확인할 것 (씬 재생)

- 타이틀 [시작] → 로비에 주인공이 서 있고 걸어다닌다. 허수아비를 때리면 대미지가 뜬다.
- 각 오브젝트 근처에서만 키 안내가 뜨고, 멀어지면 사라진다.
- 창(편성 · 설정 · 제단)이 열려 있는 동안 주인공이 안 움직이고, ESC는 창만 닫는다.
- 입구 → 지도 → 전투 승리 / 패배 / ESC / 전체 클리어 → **매번 로비로 돌아온다.** MainMenu로 새는 길이 없다.
- 로비에 돌아온 직후 정산 문구가 뜨고, 다시 들어가도 재화가 그대로다. 게임 재시작 후에도 남는다.
- 같은 층까지 가서 [처음부터]를 누르면 로비로 나갔을 때의 절반만 들어온다.
- 허수아비를 때린 뒤 입구로 나가 첫 전투에서 타게팅이 정상이다(정적 정리 확인).
- 잠긴 동료는 편성에 못 넣고, 고용하면 바로 넣을 수 있다.
- 시작 골드 강화를 산 뒤 첫 상점에서 골드가 그만큼 더 있다.
