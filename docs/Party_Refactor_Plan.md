# Party 리팩토링 계획 (캔버스 #11)

작성 2026-09-06 · 대상 `Assets/Scripts/Party/` — 8 파일 1,183 줄
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)
전제: **코드량 감소가 목표.** 프리팹·에디터 수작업이 늘어도 상관없다.
프리팹/`.asset`에 붙는 MonoBehaviour·ScriptableObject는 유니티 MonoScript 규칙상
**자기 이름의 파일이 필요하다.**

---

## 0. 현황

| 파일 | 줄 | 종류 | 프리팹 | 조립 |
|---|---:|---|---:|---:|
| PartyAssembler.cs | 465 | MonoBehaviour | **1** | 87 |
| PartyAssembleRules.cs | 199 | 순수 static | 0 | 0 |
| PartyCatalog.cs | 117 | 순수 class | 0 | 0 |
| PartyMemberData.cs | 107 | **ScriptableObject** | 0 | 0 |
| PartyLoadout.cs | 90 | **ScriptableObject** | 0 | 9 |
| PartySpawnPoint.cs | 79 | MonoBehaviour | **9** | 0 |
| PlayerData.cs | 76 | **ScriptableObject** | 0 | 0 |
| AllyLayers.cs | 50 | 순수 static | 0 | 0 |

### 조립 87줄은 UI가 아니다

`PartyAssembler`의 `Build*`는 **동료 프리팹 인스턴스화**다:

> 동료마다 제 프리팹을 인스턴스화한다. 예전에는 프리팹 안에 `Ally` 더미 슬롯
> 4칸을 미리 구워 두고 거기에 표만 꽂았다.

프리팹으로 이관할 UI 조립이 아니다. `PartyLoadout`의 9줄도
`CreateRuntime()` — 데이터 생성이다. **회수 대상 0.**

## 1. 파일명이 고정되는 것 — 5개

- `PartyAssembler`(프리팹 1) · `PartySpawnPoint`(프리팹 9) — MonoBehaviour
- `PartyMemberData` · `PartyLoadout` · `PlayerData` — ScriptableObject,
  `Assets/Data/Resources/Party/*.asset`이 문다

## 2. 병합 — 8 → 6

합칠 수 있는 건 순수 코드 3개뿐이다.

```
Party/
├ PartyAssembler.cs     (프리팹 1 — 고정)                     465
├ PartyAssembleRules.cs PartyAssembleRules + AllyLayers   199+50 → 249
├ PartyCatalog.cs                                             117
├ PartyMemberData.cs    (SO — 고정)                           107
├ PartyLoadout.cs       (SO — 고정)                            90
├ PartySpawnPoint.cs    (프리팹 9 — 고정)                       79
└ PlayerData.cs         (SO — 고정)                            76
                                                    합계   ~1,183
```

**7 파일.** `AllyLayers`(50, 순수 static)를 `PartyAssembleRules`에 넣는다 —
둘 다 유니티 객체를 안 만지는 규칙이다.

> `PartyCatalog`는 합치지 않는다. 주석이 존재 이유를 말한다 —
> *"파티 선택 화면은 코드로 스스로 지어지므로 (…) 씬 배선을 0으로 유지하려면
> 이 통로가 유일하다"* 며 `Resources`를 쓴다.
> **#14에서 `PartySelectUI`를 프리팹으로 만들면 이 우회가 없어질 수 있다** —
> 그때 다시 본다.

## 3. 결과

```
Party/   8 파일 1,183 줄  →  7 파일 ~1,183 줄   (0)
```

**줄이 안 준다.** SO 3개 + 프리팹 2개라 병합 여지가 거의 없다.

## 4. #14와 연결

`PartyCatalog`의 `Resources` 우회는 [yg_Refactor_Plan.md](yg_Refactor_Plan.md)에서
`PartySelectUI`(515줄, 조립 226줄)를 프리팹으로 만들 때 같이 판단한다.
프리팹이 되면 인스펙터에 에셋을 직접 물릴 수 있어 `Resources`가 불필요해진다.

## 검증

- [ ] 파티 편성 → 전투 진입 시 동료 4명이 제 위치에 뜨는가
- [ ] 동료별 프리팹(Ally_Arc/Tan/War/Wiz)이 각자 것으로 뜨는가
- [ ] 스폰 포인트 9곳이 정상인가 (`PartySpawnPoint`)
- [ ] 파티 `.asset`이 인스펙터에서 정상인가 (SO 파일명 확인)
