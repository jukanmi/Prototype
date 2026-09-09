# FlowScene — 메인화면 ↔ 배틀 씬 전환 구조

`inbox/프로토타입 진행/메인화면-배틀씬-전환구조.md` 명세를 코드로 옮긴 결과물.
런타임은 전부 `Prototype`, 에디터 툴은 `Prototype.EditorTools` 네임스페이스다.

## 흐름

```
앱 실행 → Boot (매니저 상주, 언로드 안 됨)
            └→ MainMenu Additive 로드

MainMenu [시작]  → MainMenu 언로드 → SampleScene Additive 로드
SampleScene [ESC] → SampleScene 언로드 → MainMenu Additive 로드
```

"처음부터 재생"은 리셋 코드 없이 **씬을 새로 로드하는 것만으로** 보장된다.
씬 파일에 저장된 초기 배치가 그대로 복원되기 때문이다.
씬 경계를 넘어 살아남는 것(`GameManager` 런 데이터, `TimeControl.Scale`,
`BattleRegistry`)만 명시적으로 초기화한다.

## 파일

| 파일 | 역할 |
|---|---|
| `SceneNames.cs` | 씬 이름 상수. `Battle` = `"SampleScene"` |
| `SceneLoader.cs` | 씬의 **존재 자체**. Additive 로드/언로드 + 페이드 + `IsBusy` 잠금 |
| `GameManager.cs` | 런 단위 데이터. 씬 오브젝트는 절대 참조하지 않는다 |
| `Core/AudioManager.cs` | BGM/SFX 골격. 클립이 비어도 예외 없이 넘어간다 |
| `BootStrapper.cs` | Boot 씬에서 최초 1회 MainMenu 를 띄운다 |
| `MainMenuController.cs` | 시작/종료 버튼 배선 |
| `BattleSceneController.cs` | 배틀 씬 진행. 현재는 ESC 이탈 처리 |
| `PartySelectUI.cs` | 메인화면 파티 편성. 고른 순서가 곧 F키 교대 순환 순서 |
| `UIManager.cs` | 볼륨 슬라이더 · 설정 패널. Boot 에 상주 |
| `Assets/Editor/FlowSceneBuilder.cs` | Boot · MainMenu 씬 생성 + Build Settings 등록 |

이 폴더 밖에 있지만 흐름의 일부인 것들 — 도메인이 명확해서 각자 자리로 보냈다.

| 파일 | 역할 |
|---|---|
| `Stage/StageOutcomeRules.cs` | `StageOutcome` 판정. 스테이지 규칙이라 `Stage/` |
| `Battle/UI/BattleRestartUI.cs` | 인게임 [처음부터] 버튼 |
| `Battle/UI/StageResultUI.cs` | 스테이지 종료 화면 (출구 화살표 / 전부 클리어 / 패배) |
| `Core/UiKit.cs` | 코드로 캔버스 짓는 공용 조각 + `UiLayer` 정렬 순서표 |

## 씬 조립 — 메뉴 한 번

**`Prototype ▸ 씬 흐름 - 메인화면 흐름 씬 만들기`** 를 누르면 아래가 전부 만들어진다.

- `Assets/Scenes/Boot.unity` — GameManager(+BootStrapper) / SceneLoader / AudioManager
  (AudioSource ×2) / EventSystem / FadeCanvas(Sort Order 999). 참조 슬롯까지 연결된다.
- `Assets/Scenes/MainMenu.unity` — Orthographic 카메라, MenuCanvas, 타이틀,
  `Btn_Start` · `Btn_Quit`, `MainMenuController` 배선
- `SampleScene` 에 `BattleSceneController` 오브젝트 추가 (**다른 오브젝트는 손대지 않는다**)
- Build Settings: `Boot`(0) → `MainMenu`(1) → `SampleScene`(2)

## 지켜야 할 제약

**Boot 씬에 화면을 그리는 Camera 를 두지 않는다.** MainMenu / SampleScene 이 각자 카메라를
갖고 있어 Boot 에도 있으면 Additive 상태에서 화면이 겹친다. `FadeCanvas` 는
Screen Space - Overlay 라 카메라 없는 순간에도 정상 렌더링된다.

예외는 `FallbackCamera` 하나다 — 아래 참조.

**EventSystem 은 Boot 씬에 하나만.** 중복되면 콘솔 경고와 함께 UI 입력이 불안정해진다.

**Boot 씬은 언로드되지 않으므로 `DontDestroyOnLoad` 를 호출하지 않는다.**

**`SetActiveScene` 은 필수.** Additive 로드 후 활성 씬을 지정하지 않으면 `Instantiate` 로
만든 오브젝트가 Boot 씬에 생성된다. 그러면 배틀 씬을 언로드해도 몬스터 · 이펙트가 남아
다음 판에 유령처럼 나타난다.

**`GameManager` 에 전투 로직이나 씬 오브젝트 참조를 넣지 않는다.** 씬이 언로드되는 순간
그 참조는 전부 무효가 되고, 원인 찾기 어려운 null 에러로 돌아온다.

## FallbackCamera — 명세와 다르게 간 부분 ①

명세는 "Boot 씬에 Camera 를 두지 않는다"고 못박았지만, 그러면 **전환 순간에 로드된 씬의
카메라가 0개가 되어** Game 뷰에 `No cameras rendering` 이 뜬다. 이전 씬을 먼저 내리고
새 씬을 나중에 얹기 때문이다(메모리 피크를 낮추려는 의도적 순서).

에디터 전용 메시지라 빌드에는 나오지 않지만, Boot 에 `FallbackCamera` 를 하나 두어 없앴다.
명세가 카메라를 금지한 이유는 **화면 겹침**이었는데, 이 카메라는 그 문제를 일으킬 수 없다.

| 설정 | 값 | 이유 |
|---|---|---|
| `cullingMask` | `Nothing` (0) | 어떤 오브젝트도 그리지 않는다 → 겹칠 대상이 없다 |
| `depth` | `-100` | 씬 카메라가 항상 그 위를 덮는다 |
| `clearFlags` | Solid Color / 검정 | 카메라 없는 순간을 검은 화면으로 채운다 |
| 태그 | **Untagged** | 아래 참조 |
| `AudioListener` | **없음** | 씬 카메라 것과 중복되면 경고 |

**태그를 `MainCamera` 로 바꾸면 안 된다.** `TargetSelector.cs:47` 이 `Camera.main` 을
쓰는데, 이 카메라가 잡히면 조준 좌표 변환이 전부 깨진다. Boot 씬은 언로드되지 않으므로 전투 중에도 계속 살아 있다는 점을 기억할 것.

대안으로 "새 씬을 먼저 얹고 이전 씬을 나중에 내리는" 순서도 검토했지만, 그러면 그 순간
카메라와 AudioListener 가 둘씩 존재해 경고가 새로 생긴다. 폴백 카메라 쪽이 부작용이 적다.

## 시간 제어 — 명세와 다르게 간 부분 ②

명세는 `Time.timeScale = 1f` 로 복구하라고 되어 있지만, 이 프로젝트의 일시정지는
`Time.timeScale` 이 아니라 **`TimeControl.Scale`** 이다
(불릿타임 중 UI · 조준은 계속 돌아야 하므로 — `Scripts/README.md` 참조).

그래서 `TimeControl.Reset()` 을 함께 호출한다. `Time.timeScale` 도 같이 1 로 되돌리지만
그건 외부 코드가 건드렸을 경우를 위한 보험일 뿐이다.

리셋 지점은 **배틀 씬이 완전히 언로드된 뒤**(`BattleSceneController.OnReturnedToMenu`)다.
씬이 살아 있는 동안 리셋하면 `BulletTimeController` 가 다시 0 으로 되돌릴 수 있다.
`BattleRegistry.Clear()` 도 같은 자리에서 부른다 — `Entity` 가 `OnDestroy` 에서 스스로
`Unregister` 하지만, Domain Reload 가 꺼져 있어 static 이 살아남는 만큼 안전망을 둔다.

## 검증 체크리스트

| # | 확인 항목 | 기대 결과 |
|---|---|---|
| 1 | Boot 씬에서 Play | 페이드 후 메인화면 |
| 2 | Hierarchy | `Boot` + `MainMenu` 두 씬 동시 표시 |
| 3 | [시작] 클릭 | 콘솔에 `[Battle] 스테이지 시작` |
| 4 | Hierarchy | `MainMenu` 사라지고 `SampleScene` 존재 |
| 5 | 배틀에서 ESC | 메인화면 복귀, `[GameManager] 런 종료` |
| 6 | 다시 [시작] | 몬스터 · 플레이어가 **초기 위치**에서 재시작 |
| 7 | 시작 버튼 연타 | 씬 중복 로드 없음 (`IsBusy` 잠금) |
| 8 | 전환 중 ESC 연타 | 무시, 에러 없음 |
| 9 | 콘솔 | EventSystem 중복 경고 없음 |
| 10 | 콘솔 | 카메라 중복/누락 에러 없음 |
| 11 | 3회 왕복 후 | 씬 목록에 잔여 씬이 쌓이지 않음 |
| 12 | 불릿타임 중 ESC | 복귀 후 다음 런이 멈춘 채 시작하지 않음 |

## 알려진 제약

- **TMP Essential Resources 미임포트 상태**다. `FlowSceneBuilder` 는 이를 감지해
  레거시 `UnityEngine.UI.Text` 로 폴백한다. `Window ▸ TextMeshPro ▸ Import TMP Essential
  Resources` 를 실행한 뒤 메뉴를 다시 돌리면 `TextMeshProUGUI` 로 만들어진다.
- ESC 는 `#if ENABLE_INPUT_SYSTEM` 으로 분기한다. 현재 프로젝트 설정은
  `activeInputHandler = 1` (Input System Package 전용)이라 `Keyboard.current` 경로를 탄다.

## 다음 단계 (이번 범위 아님)

1. ESC → 일시정지 패널 (재개 / 설정 / 메인으로)
2. `StageData` ScriptableObject → 씬 재로드 대신 `BattleSceneController.LoadStage(data)`
3. `DeckManager` + `CardView` 오브젝트 풀
4. 보스 스테이지 컷씬 씬 Additive 로드
5. `SampleScene` → `Battle` 로 이름 변경 (`SceneNames.cs` 한 줄만 수정)
