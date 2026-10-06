# Input & Control 리팩토링 계획 (캔버스 #12)

작성 2026-09-06 · 대상 `Assets/Scripts/Input/` + `Control/` — 11 파일 1,816 줄
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)
전제: **코드량 감소가 목표.** 프리팹·에디터 수작업이 늘어도 상관없다.
프리팹/`.asset`에 붙는 MonoBehaviour·ScriptableObject는 유니티 MonoScript 규칙상
**자기 이름의 파일이 필요하다.**

---

## 0. 현황

| 파일 | 폴더 | 줄 | 종류 | 프리팹 | 조립 |
|---|---|---:|---|---:|---:|
| EnemyControl.cs | Control | 478 | MonoBehaviour | **6** | 34 |
| PlayerInputController.cs | Input | 348 | MonoBehaviour | **1** | 0 |
| PlayerPilot.cs | Control | 229 | MonoBehaviour | **1** | 0 |
| RebindManager.cs | Input | 194 | **ScriptableObject** | 0 | 0 |
| InputRebindRules.cs | Input | 145 | 순수 static | 0 | 0 |
| InputActionNames.cs | Input | 91 | 순수 static | 0 | 0 |
| KeyBindingPreset.cs | Input | 89 | **ScriptableObject** | 0 | 0 |
| InputBindingUtility.cs | Input | 75 | 순수 static | 0 | 0 |
| InputDisplayNames.cs | Input | 70 | 순수 static | 0 | 0 |
| InputMapSwitcher.cs | Input | 60 | MonoBehaviour | **1** | 0 |
| Control.cs | Control | 37 | MonoBehaviour | 0 | 0 |

`EnemyControl`의 조립 34줄은 **적 제어 객체 생성**이다. UI 아님. 회수 대상 0.

## 1. 파일명 고정 — 6개

`EnemyControl`(6) · `PlayerInputController`(1) · `PlayerPilot`(1) ·
`InputMapSwitcher`(1) — MonoBehaviour.
`RebindManager` · `KeyBindingPreset` — ScriptableObject.

## 2. 병합 — 11 → 7

Input의 순수 static 4개가 합칠 대상이다.

```
Input/
├ InputRebindRules.cs   InputRebindRules + InputBindingUtility
│                       + InputActionNames + InputDisplayNames   145+75+91+70 → ~381
├ RebindManager.cs      (SO — 고정)                                          194
├ KeyBindingPreset.cs   (SO — 고정)                                           89
├ PlayerInputController.cs  (프리팹 1 — 고정)                                348
└ InputMapSwitcher.cs   (프리팹 1 — 고정)                                     60

Control/
├ EnemyControl.cs       (프리팹 6 — 고정)                                    478
├ PlayerPilot.cs        (프리팹 1 — 고정)                                    229
└ Control.cs            (MonoBehaviour, 프리팹 0)                             37
                                                                 합계   ~1,816
```

**8 파일.** 순수 static 4개는 전부 "키 이름 · 표시 이름 · 바인딩 규칙"이라
한 파일이 맞다.

> `RoleNames`(#5 Core)의 주석이 `InputDisplayNames`를 *"같은 자리의 물건"* 이라
> 부른다. 둘을 한 곳에 모을지는 판단이 갈린다 — 이 문서는 **각자 폴더 유지**로
> 잡았다(직업 표기는 파티/카드가 쓰고, 키 표기는 입력만 쓴다).

## 3. `Control/` 폴더 — 3개뿐

`Control.cs`(37, 프리팹 0)만 합칠 수 있는데, `EnemyControl`·`PlayerPilot` 둘 다
프리팹 고정이라 흡수처가 없다. **폴더 유지.**

## 4. 결과

```
Input/ + Control/   11 파일 1,816 줄  →  8 파일 ~1,816 줄   (0)
```

**줄이 안 준다.**

## 검증

- [ ] 키 리바인드 → 저장 → 재시작 후 유지
- [ ] 키 충돌 검사 (`InputRebindRules`)
- [ ] 화면에 뜨는 키 이름이 정상인가 (`InputDisplayNames`)
- [ ] 전투/UI 입력맵 전환 (`InputMapSwitcher`)
- [ ] 적 6종 프리팹의 `EnemyControl`이 정상인가
