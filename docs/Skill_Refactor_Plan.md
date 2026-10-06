# Skill 리팩토링 계획 (캔버스 #6)

작성 2026-09-06 · 대상 `Assets/Scripts/Skill/` — 9 파일 1,861 줄
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)
전제: **코드량 감소가 목표.** 프리팹·에디터 수작업이 늘어도 상관없다.
프리팹/`.asset`에 붙는 MonoBehaviour·ScriptableObject는 유니티 MonoScript 규칙상
**자기 이름의 파일이 필요하다.**

---

## 0. 현황

| 파일 | 줄 | 종류 | 프리팹 | 조립 |
|---|---:|---|---:|---:|
| SkillState.cs | 671 | 순수 class | 0 | 0 |
| Effects.cs | 368 | `EffectUtil` + `ISkillEffect` 구현체들 | 0 | 0 |
| SkillData.cs | 231 | **ScriptableObject** | 0 | 0 |
| ChargeSkillState.cs | 158 | 순수 class | 0 | 0 |
| KnockbackPreview.cs | 147 | 순수 class | 0 | 0 |
| WallFinder.cs | 92 | 순수 class | 0 | 0 |
| SkillContext.cs | 85 | 순수 class | 0 | 0 |
| EffectRunner.cs | 81 | MonoBehaviour | **0** | 0 |
| ISkillEffect.cs | 28 | interface | 0 | 0 |

**프리팹 배선 0 · 조립 코드 0.** 이 그룹은 순수 로직이다.

## 1. 회수할 코드가 없다

조립 코드가 0이라 프리팹 이관으로 뺄 게 없다. `SkillState`(671)는
*"실제로 동료가 움직이고 때리는 건 전부 여기서 일어난다"* 는 런타임 코어다.

## 2. 판단 필요 — 안 쓰는 Effect 4종

`SkillData.cs:165`가 `[SerializeReference] List<ISkillEffect> effects`로 들고 있어
**효과는 `.asset`에 타입명으로 저장된다.** `Assets/Data/Skills/*.asset` 전수 검색:

| Effect | .asset 사용 |
|---|---:|
| `ChargeEffect` | **1** |
| `ShieldEffect` | 0 |
| `DamageCutEffect` | 0 |
| `TauntEffect` | 0 |
| `LifestealEffect` | 0 |

> **죽은 코드가 아니라 "안 쓴 팔레트"다.** 아트 빌더와 같은 성격 — 새 스킬을
> 만들 때 고르는 선택지다. 최근 커밋이 계속 스킬 추가
> (`feat:일섬 구현` · `feat: 서릿발, 회전베기 스킬 구현`)이므로 **유지 권장.**
>
> 스킬 설계가 확정돼 이 4종을 쓸 계획이 없다면 그때 지운다. 지우면 `Effects.cs`
> 368줄 중 상당분이 빠진다.

## 3. 병합 — 9 → 6

프리팹 배선 0이라 병합이 자유롭다. 다만 줄은 안 준다.

```
Skill/
├ SkillState.cs        SkillState + ChargeSkillState       671+158 → ~825
├ Effects.cs           EffectUtil + 구현체 + ISkillEffect    368+28 → ~392
├ SkillData.cs         (ScriptableObject — 파일명 고정)                231
├ KnockbackPreview.cs  KnockbackPreview + WallFinder        147+92 → ~236
├ SkillContext.cs                                                      85
└ EffectRunner.cs      (MonoBehaviour)                                  81
                                                          합계    ~1,850
```

**근거**

- `ChargeSkillState`(158)는 `SkillState` 파생. 같은 파일이 맞다.
- `ISkillEffect`(28)는 구현체가 전부 `Effects.cs`에 있다. 계약을 구현체 옆에 둔다.
- `WallFinder`(92)는 `KnockbackPreview`가 벽 판정에 쓴다. 1:1 결합.

> `SkillData.cs`는 **ScriptableObject라 파일명이 고정된다.** `Assets/Data/Skills/`의
> `.asset`들이 이 타입을 물고 있다 — MonoBehaviour와 같은 제약이 SO에도 걸린다.

## 4. 결과

```
Skill/   9 파일 1,861 줄  →  6 파일 ~1,850 줄   (-11, 병합 오버헤드)
```

**줄은 안 준다.** 순수 로직이라 정리 대상이 아니다.
줄이 줄려면 2절의 Effect 4종을 지우는 판단이 필요하다.

## 검증

- [ ] 스킬 시전 → 이동 → 타격 → 후딜 전 과정
- [ ] 차지 스킬 홀드/해제 (`ChargeSkillState`)
- [ ] 넉백 미리보기 화살표가 벽에서 멈추는가 (`WallFinder`)
- [ ] 피격당해도 스킬이 안 끊기는가 (슈퍼아머, 결정 로그 ③)
- [ ] 사망 시 `ForceChangeState`로 관통되는가
- [ ] 인스펙터에서 스킬 `.asset`의 effects 목록이 정상인가 (SO 파일명 확인)
