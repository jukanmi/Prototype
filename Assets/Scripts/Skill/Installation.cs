using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 설치기의 몸. 실체(렌더러)는 없다 — 설치 지점에 서서 <see cref="SkillData.hitDataList"/>를
    /// <see cref="SkillData.HitTime"/> 타임라인대로 터뜨리고, 다 쏘면 스스로 사라진다.
    ///
    /// 시전자 상태와 <b>분리</b>돼 있다. 시전자는 설치만 하고 castTime + recoveryTime 뒤에 풀려나므로
    /// 불릿타임이면 다음 슬롯이 도는 동안 여기서 후속타가 겹쳐 나간다.
    /// 시계는 <see cref="TimeControl.DeltaTime"/> — 컷인 정지 · 불릿타임 배율을 그대로 따른다.
    ///
    /// 시전자가 죽으면 <see cref="Combat.Attack"/>이 false를 돌려 헛침이 된다. 무대에서 내려가
    /// 비활성이어도 Attack은 순수 메서드라 그대로 나간다.
    /// </summary>
    public class Installation : MonoBehaviour
    {
        private static readonly List<Installation> live = new List<Installation>();

        /// <summary>지금 서 있는 설치기 전부. <see cref="SkillRangeIndicator"/>가 범위를 그릴 때 훑는다.</summary>
        public static IReadOnlyList<Installation> Live => live;

        private SkillData data;
        private SkillContext ctx;
        private Vector3 center;
        private float timer;

        /// <summary>지금까지 낸 타 수. 로그 · 테스트용.</summary>
        public int Fired { get; private set; }

        public bool IsDone => data == null || data.hitDataList == null || Fired >= data.hitDataList.Count;

        public static Installation Place(SkillData data, in SkillContext ctx, Vector3 center)
        {
            var go = new GameObject($"[설치기] {data.skillName}");
            go.transform.position = center;

            var inst = go.AddComponent<Installation>();
            inst.data = data;
            inst.ctx = ctx;
            inst.center = center;
            live.Add(inst);

            BattleLog.Log(LogCategory.Skill,
                $"  └ {data.skillName} 설치 — {center} · 후속 {data.hitDataList?.Count ?? 0}타", ctx.caster);

            return inst;
        }

        private void Update() => Tick(TimeControl.DeltaTime);

        private void OnDestroy() => live.Remove(this);

        /// <summary>
        /// 다음 타가 터질 자리. 시전자는 이미 풀려나 <see cref="SkillState.TryGetRangePreview"/>가
        /// 못 그리므로 설치기가 직접 답한다 — 마지막 타까지 계속 보인다.
        /// progress는 전체 타임라인 기준이라 원이 차오르는 속도가 곧 남은 시간이다.
        /// </summary>
        public bool TryGetRangePreview(out AttackRangePreview range)
        {
            range = default;
            if (IsDone) return false;

            HitData next = data.hitDataList[Fired];
            float end = Mathf.Max(0.01f, data.HitTime(data.hitDataList.Count - 1));

            range = AttackRangePreview.FromCircle(center, data.RadiusFor(in next) * ctx.RadiusScale,
                                                  Mathf.Clamp01(timer / end));
            return true;
        }

        /// <summary>public인 이유는 에디트모드 테스트가 Update 없이 직접 펌프하기 위해서다.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            timer += dt;

            while (!IsDone && timer >= data.HitTime(Fired))
                Fire();

            if (IsDone) Destroy(gameObject);
        }

        private void Fire()
        {
            HitData hit = data.hitDataList[Fired];

            // 카드 배율(황금 카드)은 SkillState.FireNextHit와 같은 자리에서 곱한다.
            hit.damageData.damage *= ctx.DamageScale;

            // 타별 반경 — 조여드는 균열처럼 타마다 다를 수 있다. 차징 배율도 같이 곱한다.
            float radius = data.RadiusFor(in hit) * ctx.RadiusScale;

            SkillVfx style = data.vfx.AsSkill();

            // 실체가 없으니 타격마다 한 번 번쩍여 "여기서 나갔다"를 보여 준다.
            BattleVfx.Cast(center, radius, in style);

            // ponytail: 중심 높이는 지면 고정 — 띄운 적이 radius 밖으로 뜨면 빗나간다.
            // 필요해지면 SkillState.FireArea처럼 대상 고도를 더한다.
            int hits = EffectUtil.AreaStrike(center, radius, ctx.CasterCombat, in hit, in style);

            Fired++;

            if (hits == 0)
                BattleLog.Log(LogCategory.Skill,
                    $"  └ {data.skillName} 설치기 {Fired}/{data.hitDataList.Count}타 — 반경 {radius:0.#} 안에 적 없음, 헛침", ctx.caster);
            else
                BattleLog.Log(LogCategory.Skill,
                    $"  └ {data.skillName} 설치기 {Fired}/{data.hitDataList.Count}타 발동 (t={timer:0.##}s, {hits}명)", ctx.caster);
        }
    }
}
