# Deck & Progression 리팩토링 계획 (캔버스 #10)

작성 2026-09-06 · 대상 `Assets/Scripts/Deck/` + `Progression/` — 15 파일 2,298 줄
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)
전제: **코드량 감소가 목표.** 프리팹·에디터 수작업이 늘어도 상관없다.
프리팹/`.asset`에 붙는 MonoBehaviour·ScriptableObject는 유니티 MonoScript 규칙상
**자기 이름의 파일이 필요하다.**

---

> ## ★ 여기는 실제로 코드가 준다 — UI 조립 309줄
>
> `Battle/UI` 다음으로 UI 조립 코드가 많은 그룹이다.
> 프리팹 배선은 **15개 전부 0** — 전 UI가 코드로 지어진다.

## 0. 현황

| 파일 | 폴더 | 줄 | 종류 | 프리팹 | **UI 조립** |
|---|---|---:|---|---:|---:|
| DeckBuilderUI.cs | Progression | 441 | MonoBehaviour | 0 | **156** |
| LevelUpSession.cs | Progression | 404 | MonoBehaviour | 0 | **88** |
| Deck.cs | Deck | 327 | 순수 class | 0 | 0 |
| RunProgression.cs | Progression | 194 | 순수 class | 0 | 0 |
| PartyState.cs | Progression | 179 | 순수 class | 0 | 0 |
| CardOfferView.cs | Progression | 168 | 순수 class | 0 | **65** |
| CardOfferRules.cs | Progression | 130 | 순수 static | 0 | 0 † |
| SkillCatalog.cs | Progression | 94 | **ScriptableObject** | 0 | 0 |
| DeckRules.cs | Deck | 93 | 순수 static | 0 | 0 |
| ExpRules.cs | Progression | 81 | 순수 static | 0 | 0 |
| CardGrantRules.cs | Progression | 65 | 순수 static | 0 | 0 |
| ComboCard.cs | Deck | 49 | struct | 0 | 0 |
| DeckStartupMode.cs | Progression | 29 | enum | 0 | 0 |
| ExpRewards.cs | Progression | 28 | 순수 static | 0 | 0 |
| GameplayModal.cs | Progression | 16 | 순수 static | 0 | 0 |

† `CardOfferRules.Build(...)`는 이름만 Build일 뿐 **카드 후보 목록 생성**이다.
UI 조립이 아니다 — 자동 측정의 오탐이었다.

## 1. UI 조립 309줄 → 프리팹 (핵심 작업)

세 화면 전부 코드로 지어진다. 프리팹 배선이 0이라 **인스펙터 값을 잃을 걱정도 없다.**

| 파일 | 회수 | 화면 |
|---|---:|---|
| `DeckBuilderUI` | 156 | 디버그 모드 시작 덱 편집기 (카드 전 종류를 일반·황금 두 벌로 격자 배치) |
| `LevelUpSession` | 88 | 라운드 클리어 후 레벨업 · 카드 획득 (판 2장) |
| `CardOfferView` | 65 | 카드 세 장 제시 |

`DeckBuilderUI`가 156줄로 가장 크다 — **격자 레이아웃이라 프리팹 이득이 특히 크다.**
유니티 `GridLayoutGroup`을 인스펙터에서 잡으면 코드가 통째로 사라진다.

> **주의**: `LevelUpSession`은 [UI_Refactor_Plan.md](UI_Refactor_Plan.md) 2-3의
> `sortingOrder` 사다리에 들어 있다(210). `DeckBuilderUI`도(220).
> `Core/UiLayer.cs` 작업과 같이 처리한다.

> `LevelUpSession:115` · `DeckBuilderUI:132`는 이미
> `SimpleUI.EnsureEventSystem()`을 부른다 — UI 계획 2-1의 통합 대상에 이미 맞춰져 있다.

## 2. 병합 — 15 → 8

프리팹 0이라 자유롭다. `SkillCatalog`(SO)만 파일명 고정.

```
Progression/
├ DeckBuilderUI.cs   (프리팹화 후 ~285)                              441
├ LevelUpSession.cs  LevelUpSession + CardOfferView + GameplayModal
│                    (프리팹화 후 ~435)                     404+168+16 → 588
├ Rules.cs           CardOfferRules + ExpRules + CardGrantRules
│                    + ExpRewards                        130+81+65+28 → 304
├ RunProgression.cs  RunProgression + PartyState + DeckStartupMode
│                                                        194+179+29 → 402
└ SkillCatalog.cs    (SO — 고정)                                      94

Deck/
├ Deck.cs            Deck + DeckRules + ComboCard          327+93+49 → 469
```

**6 파일** (프리팹화 전 기준 2,298줄).

**병합 근거**

- `CardOfferView`(168)의 소비자는 `LevelUpSession`뿐. `GameplayModal`(16)은
  모달 잠금 헬퍼로 둘 다 쓴다.
- 규칙 4개(`CardOfferRules`·`ExpRules`·`CardGrantRules`·`ExpRewards`)는 전부
  유니티 비의존 순수 계산. `Core/Rules.cs`와 같은 성격이지만 **성장 도메인이라
  여기 둔다.**
- `Deck`·`DeckRules`·`ComboCard`는 `Deck/` 폴더 전체다. **폴더가 파일 3개뿐이라
  하나로 합치고 폴더를 없애도 된다** — `Progression/Deck.cs`로.

## 3. 결과

```
Deck/ + Progression/   15 파일 2,298 줄  →  6 파일 ~1,989 줄   (-309)
```

**UI 조립 309줄이 실제로 사라진다.** `Battle/UI`(1,106) 다음으로 큰 회수처다.

## 4. 우선순위 — 높다

[Refactor_Master_Plan.md](Refactor_Master_Plan.md) 5단계(UI 프리팹 재작성)에
**이 그룹을 같이 넣는다.** 회수 순서:

```
1  DeckInspectorUI   276   (Battle/UI)
2  ComboBoardUI      250   (Battle/UI)
3  RebindUI          225   (Battle/UI)
4  PartySelectUI     226   (yg, #14)
5  DeckBuilderUI     156   ← 이 문서
6  PartyHealthHUD    110   (Battle/UI)
7  LevelUpSession     88   ← 이 문서
8  CardOfferView      65   ← 이 문서
```

## 검증

**프리팹화 후 (각 화면별로)**
- [ ] 덱 편집기 — 카드 격자, 좌클릭 +1 / 우클릭 −1, 하단 목록 갱신
- [ ] 레벨업 화면 — 단계 고르기 판 ↔ 카드 3장 판 왕복, 남은 경험치로 반복
- [ ] 카드 제시 3장의 배경색이 정상인가
- [ ] 모달이 열린 동안 게임 조작이 잠기는가 (`GameplayModal`)
- [ ] 겹침 순서 — 레벨업(210)이 스테이지 결과(200) 위, 덱빌더(220)가 그 위
