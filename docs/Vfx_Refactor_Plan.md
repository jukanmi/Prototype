# Vfx & Markers 리팩토링 계획 (캔버스 #13)

작성 2026-09-06 · 대상 `Assets/Scripts/Vfx/` + `Vfx/Markers/` — 20 파일 2,676 줄
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)
전제: **코드량 감소가 목표.** 프리팹·에디터 수작업이 늘어도 상관없다.
프리팹/`.asset`에 붙는 MonoBehaviour·ScriptableObject는 유니티 MonoScript 규칙상
**자기 이름의 파일이 필요하다.**

---

> **프리팹 배선이 20개 전부 0이다.** 이 그룹은 병합이 완전히 자유롭다.

## 0. 현황

| 파일 | 줄 | 종류 | 프리팹 | 조립 |
|---|---:|---|---:|---:|
| KnockbackIndicator.cs | 287 | MonoBehaviour | 0 | 17 |
| StatusEffectBar.cs | 242 | MonoBehaviour | 0 | 13 |
| BattleVfx.cs | 222 | MonoBehaviour | 0 | 20 |
| SkillRangeIndicator.cs | 176 | MonoBehaviour | 0 | 0 |
| CombatStateVisuals.cs | 166 | 순수 static | 0 | 0 |
| AttackRangeIndicator.cs | 163 | MonoBehaviour | 0 | 0 |
| EnemyStateLabel.cs | 156 | MonoBehaviour | 0 | 0 |
| VfxSprite.cs | 146 | MonoBehaviour | 0 | 0 |
| RangeIndicator.cs | 136 | MonoBehaviour | 0 | 19 |
| EnemyStateTint.cs | 127 | MonoBehaviour | 0 | 0 |
| StatusEffectVisuals.cs | 124 | 순수 static | 0 | 0 |
| ChargeGauge.cs | 118 | MonoBehaviour | 0 | 0 |
| ControlledCharacterArrow.cs | 114 | MonoBehaviour | 0 | 0 |
| Markers/WorldMarker.cs | 85 | MonoBehaviour | 0 | 6 |
| Markers/MarkerRules.cs | 81 | 순수 static | 0 | 0 |
| Markers/MarkerLayer.cs | 76 | MonoBehaviour | 0 | 0 |
| VfxClip.cs | 75 | **ScriptableObject** | 0 | 0 |
| SkillVfx.cs | 63 | 순수 static | 0 | 0 |
| Markers/MarkerRequest.cs | 63 | struct | 0 | 0 |
| VfxLibrary.cs | 56 | **ScriptableObject** | 0 | 0 |

**MonoBehaviour 14개인데 프리팹 참조가 전부 0** — 전부 코드로 `AddComponent`
된다. 파일명 제약이 없다.

**SO 2개(`VfxClip` · `VfxLibrary`)만 파일명 고정** — `Assets/Data/Vfx/`의
`.asset`과 `Data/Resources/VfxLibrary.asset`이 문다.

## 1. 조립 75줄 — 프리팹으로 옮길까

`KnockbackIndicator`(17) · `BattleVfx`(20) · `RangeIndicator`(19) ·
`StatusEffectBar`(13) · `WorldMarker`(6) = **75줄**.

**옮기지 않기를 권한다.** UI 캔버스와 달리 이건 **런타임에 개수가 정해지는
월드 마커**다 — 적 수만큼 상태바가, 넉백 대상 수만큼 화살표가 생긴다.
프리팹으로 만들어도 인스턴스화 코드가 그대로 남아서 회수량이 거의 없다.

> 회수하려면 마커 프리팹을 만들고 `Instantiate`로 바꾸는 건데, 75줄 중
> 실제로 사라지는 건 20~30줄이다. 손익이 안 맞는다.

## 2. 병합 — 20 → 8

프리팹 제약이 없어 자유롭게 묶는다.

```
Vfx/
├ BattleVfx.cs        BattleVfx + VfxSprite + SkillVfx            222+146+63 → ~431
├ Indicators.cs       KnockbackIndicator + RangeIndicator
│                     + AttackRangeIndicator + SkillRangeIndicator  287+136+163+176 → ~762
├ StatusVisuals.cs    StatusEffectBar + StatusEffectVisuals
│                     + CombatStateVisuals                        242+124+166 → ~532
├ EnemyVisuals.cs     EnemyStateLabel + EnemyStateTint
│                     + ChargeGauge + ControlledCharacterArrow    156+127+118+114 → ~515
├ VfxClip.cs          (SO — 고정)                                              75
├ VfxLibrary.cs       (SO — 고정)                                              56
└ Markers/
  └ Marker.cs         WorldMarker + MarkerRules + MarkerLayer
                      + MarkerRequest                              85+81+76+63 → ~305
                                                                   합계    ~2,676
```

**7 파일.**

**병합 근거**

- `BattleVfx` 주석: *"전투 연출 진입점. 호출부는 논리 좌표와 크기만 넘긴다. (…)
  실제 그림은 `VfxSprite`가 스프라이트 시트로 그린다."* 진입점과 실행부는 한 세트.
  `VfxSprite`·`SkillRangeIndicator`의 유일한 소비자가 `BattleVfx`인 것도 같은 신호.
- **Indicators 4개** — 전부 바닥에 범위/방향을 그리는 라인 렌더러다.
- **Markers 4개** — 이미 하위 폴더로 묶여 있다. 파일만 합친다.

## 3. 결과

```
Vfx/   20 파일 2,676 줄  →  7 파일 ~2,676 줄   (0)
```

**줄이 안 준다.** 대신 **파일이 20 → 7로 가장 크게 준다** —
프리팹 제약이 없는 유일한 큰 그룹이다.

## 4. 우선순위

"한눈에 보기" 목적이라면 **여기가 가장 효율이 좋다.** 20개가 7개가 되는데
프리팹을 하나도 안 건드린다. 위험도 최저.

## 검증

- [ ] 타격 이펙트 · 스킬 이펙트가 정상 재생되는가
- [ ] 넉백 화살표 · 공격 범위 · 스킬 범위 표시
- [ ] 적 상태이상 바 · 상태 라벨 · 틴트
- [ ] 차지 게이지 · 조종 캐릭터 화살표
- [ ] 월드 마커 정렬 순서 (깊이 기준)
- [ ] `VfxClip` · `VfxLibrary` `.asset`이 인스펙터에서 정상인가
