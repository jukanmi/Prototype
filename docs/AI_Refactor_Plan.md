# AI & Brains 리팩토링 계획 (캔버스 #7)

작성 2026-09-06 · 대상 `Assets/Scripts/AI/` + `AI/Brains/` — 10 파일 1,453 줄
상위 문서: [Refactor_Master_Plan.md](Refactor_Master_Plan.md)
전제: **코드량 감소가 목표.** 프리팹·에디터 수작업이 늘어도 상관없다.
프리팹/`.asset`에 붙는 MonoBehaviour·ScriptableObject는 유니티 MonoScript 규칙상
**자기 이름의 파일이 필요하다.**

---

## 0. 현황

| 파일 | 줄 | 종류 | 프리팹 | 조립 |
|---|---:|---|---:|---:|
| BossPatternAction.cs | 460 | MonoBehaviour | **1** | 7 |
| EnemyChargeAction.cs | 229 | MonoBehaviour | **1** | 6 |
| IEnemySpecialAction.cs | 206 | interface + 부속 | 0 | 0 |
| EnemySpecialSequence.cs | 204 | 순수 class | 0 | 0 |
| IEnemyBrain.cs | 152 | interface + `struct BrainStats` | 0 | 0 |
| Brains/BossBrainAsset.cs | 87 | ScriptableObject | 0 | 0 |
| Brains/ChargerBrainAsset.cs | 34 | ScriptableObject | 0 | 0 |
| Brains/RangedBrainAsset.cs | 32 | ScriptableObject | 0 | 0 |
| Brains/MeleeBrainAsset.cs | 30 | ScriptableObject | 0 | 0 |
| Brains/DummyBrainAsset.cs | 19 | ScriptableObject | 0 | 0 |

## 1. 구조가 이미 옳다 — 건드릴 이유 없음

주석이 3층 분리를 명시한다:

> `BossPatternAction` — 보스 패턴 하나의 정의. 판정 · 이동 · 연출 · 버프를 전부
> 데이터로 들고 있다. **언제 쓸지(사거리 · HP · 쿨)는 여기 없다** — 그건 판단이라
> `BossBrainAsset`이 갖는다.

> `EnemyControl`(#12) — 감지 · 타이머 · 게이트를 맡고 **판단은 브레인에 위임**한다.
> 종류별 행동 차이는 브레인 애셋을 갈아끼워 만든다.

**행동(Action) / 판단(Brain) / 제어(Control)** 가 깔끔하게 갈려 있다.

## 2. 병합 — 10 → 6

```
AI/
├ BossPatternAction.cs  (MonoBehaviour, 프리팹 1 — 파일명 고정)        460
├ EnemyChargeAction.cs  (MonoBehaviour, 프리팹 1 — 파일명 고정)        229
├ EnemyBrain.cs         IEnemyBrain + BrainStats
│                       + IEnemySpecialAction + EnemySpecialSequence  ~562
└ Brains/               (SO 5개 — 각자 파일 유지)                       202
                                                           합계    ~1,453
```

**병합 근거** — `IEnemyBrain`(152) · `IEnemySpecialAction`(206) ·
`EnemySpecialSequence`(204)는 전부 브레인이 쓰는 계약과 그 조합이다.
`IEnemyBrain`의 코드 소비자가 `IEnemySpecialAction.cs` 하나뿐인 것도 같은 신호다.

### SO 애셋 확인 완료 — 5개 전부 파일명 고정

guid 전수 조사 결과 **5개 모두 `.asset`이 물고 있다:**

```
BossBrainAsset     ← Assets/Data/Enemy/Brain_Boss.asset
ChargerBrainAsset  ← Assets/Data/Enemy/Brain_Charger.asset
RangedBrainAsset   ← Assets/Data/Enemy/Brain_Ranged.asset
MeleeBrainAsset    ← Assets/Data/Enemy/Brain_Melee.asset
DummyBrainAsset    ← Assets/Data/Enemy/Brain_Dummy.asset
```

**병합 불가.** 위 배치가 그대로 유효하다.

> `DummyBrainAsset`(19줄)의 유일한 **코드** 소비자는 `TrainingSceneBuilder`
> (삭제 예정 빌더)지만, `Brain_Dummy.asset`이 타입을 물고 있으므로
> 빌더를 지워도 **살려 둬야 한다.**

## 3. 결과

```
AI/   10 파일 1,453 줄  →  6 파일 ~1,453 줄   (0)
```

**줄이 안 준다.** 순수 로직 + SO라 정리 대상이 아니다.

## 검증

- [ ] 근접 · 원거리 · 돌진 · 보스 각 적의 행동이 이전과 같은가
- [ ] 보스 패턴 선택(사거리 · HP · 쿨)이 정상인가
- [ ] 특수 행동 시퀀스가 순서대로 도는가
- [ ] 브레인 애셋을 인스펙터에서 갈아끼울 수 있는가 (SO 파일명 확인)
