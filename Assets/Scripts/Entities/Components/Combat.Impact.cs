using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 맞은 몸이 <b>어떻게 날아가는가</b> — 넉백 · 띄우기 · 내려찍기 · 벽 바운드 · 벽 스턴.
    /// 튜닝값은 Combat.cs 인스펙터 필드에 있다.
    /// </summary>
    public partial class Combat
    {
        /// <summary>
        /// 이 타격이 <b>어디서 왔는지</b>. 장판은 시전자와 떨어진 좌표에서 터지므로 기준점을 따로 싣고 오고,
        /// 없으면 시전자 위치를 쓴다 — 근접 히트박스와 투사체는 그게 맞다.
        /// 넉백 방향과 패링 전방 판정이 같은 기준을 봐야 해서 한 곳에 모아 둔다.
        /// </summary>
        private bool TryResolveHitOrigin(in HitData hit, Combat attacker, out Vector3 origin)
        {
            if (hit.hasOrigin)
            {
                origin = hit.origin;
                return true;
            }

            if (attacker != null)
            {
                origin = attacker.transform.position;
                return true;
            }

            // 공격자도 기준점도 없다 — 방향을 알 수 없다.
            origin = transform.position;
            return false;
        }

        /// <returns>이번 타격이 실제로 민 방향(수평 단위벡터). 밀지 않았으면 0벡터.</returns>
        private Vector3 ApplyKnockback(in HitData hit, Combat attacker)
        {
            TryResolveHitOrigin(in hit, attacker, out Vector3 casterPos);
            Vector3 casterFwd = attacker != null ? attacker.Physics.Facing : physics.Facing;

            // 맞은 순간 속력을 0으로 만든다. 남은 관성 위에 힘을 얹으면 같은 데이터인데도
            // 대상이 달려오던 중이냐 떨어지던 중이냐에 따라 밀리는 거리와 뜨는 높이가
            // 달라져 연계가 빗나간다. 수직은 낙하 성분만 끊는다 — 올라가는 중에 끊으면
            // 띄워 둔 몸이 후속타를 맞는 순간 정점에서 뚝 떨어진다.
            physics.ResetInertia();
            physics.StopFall();

            if (hit.snapZ && (attacker != null || hit.hasOrigin))
                physics.SnapZ(casterPos.z);

            // 벽 레이어를 같이 넘긴다 — 밀치기(TowardWall)는 시전자가 아니라 벽이 방향을 정한다.
            Vector3 dir = hit.ResolveDirection(casterPos, casterFwd, transform.position, physics.WallMask);
            if (hit.pushDistance > 0f)
                physics.AddImpulse(dir, physics.ImpulseToTravel(PushClamped(in hit, casterPos)),
                                   resetInertia: false);

            float launch = ResolveLaunch(in hit);

            // 부호가 방향이다. 위로 올릴 때만 상한이 의미가 있고, 아래로는 그대로 꽂는다.
            if (launch > 0f) physics.AddLaunch(launch, hit.capAirborne ? hit.airborneHeight : 0f);
            else if (launch < 0f) physics.AddSlam(-launch);

            return dir;
        }

        /// <summary>
        /// 벽 스턴 판정. 밀려 나가기 전에 <b>때린 순간</b> 결정한다.
        ///
        /// 접촉 순간의 실측 속도로 재던 때는 두 군데에서 샜다. 하나는 감쇠 —
        /// <see cref="Physics.AddImpulse"/>가 지수감쇠라 벽에 닿을 때쯤이면 속도가 이미
        /// 문턱 아래다(밀림 3m · 감쇠 8이면 2m를 밀린 순간 속도가 8 밑으로 떨어진다).
        /// 다른 하나는 <b>이미 벽에 붙어 있는 적</b> — <c>OnCollisionEnter</c>가 새로 뜨지 않아
        /// 아무리 세게 밀어도 판정 자체가 열리지 않았다. 벽에 몰아붙이고 치는 게
        /// 숄더차지의 그림인데 거기서만 안 걸렸다.
        ///
        /// 그래서 저작값이 만들어 낼 <b>예상 충돌 속도</b>로 잰다. 지수감쇠는
        /// 속도 = 감쇠계수 × 남은 거리라, 벽까지의 거리만 알면 값이 그대로 나온다.
        /// 실제로 안 움직여도(=거리 0) 밀어 넣은 힘 전부가 충돌 속도가 된다.
        /// </summary>
        private void TryWallStun(in HitData hit, Vector3 dir)
        {
            // 띄우기 · 넉백은 벽 바운드가 가져간다. 벽 스턴은 약경직 전용이다.
            if (CombatState != CombatState.LightHit) return;
            if (hit.pushDistance <= 0f || wallBounceTimer > 0f) return;

            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f) return;
            dir.Normalize();

            // 레이는 몸 중심에서 나가 반지름만큼 길게 잡힌다 — 그만큼 보수적으로 판정된다.
            float gap = WallFinder.DistanceToWall(physics.GroundPosition, dir, physics.WallMask);
            if (gap >= hit.pushDistance) return;   // 벽까지 못 간다(벽이 없으면 무한대)

            float impact = physics.ImpulseDamping * (hit.pushDistance - gap);
            if (impact < wallStunSpeedThreshold) return;

            // 뒤따라올 실제 접촉이 한 번 더 때리지 않게 막는다 — 판정은 여기 한 번뿐이다.
            wallBounceTimer = wallBounceCooldown;

            // 경직(SetStunTimer)이 아니라 디버프다. 경직으로 걸면 0.2초 뒤 들어오는 평타의
            // hitStunDuration이 1.2초를 그대로 덮어써 벽에 처박은 보람이 사라진다.
            ApplyDebuff(Debuff.Stun, wallStunDuration);

            Vector3 point = physics.GroundPosition + dir * gap;
            point.y = transform.position.y;

            EmitWallVfx(new Physics.WallHit(-dir, point, impact),
                        Mathf.Clamp01(impact / Mathf.Max(0.01f, wallHardSpeed)));

            BattleLog.Log(LogCategory.Physics,
                $"{name} <b>벽 스턴</b> | 벽까지 {gap:0.##}m · 예상 충돌 {impact:0.#} " +
                $"(문턱 {wallStunSpeedThreshold:0.#} · 스턴 {wallStunDuration:0.##}s)", this);
        }

        /// <summary>
        /// 이번 타격이 실을 수직 <b>속도</b>. 양수면 띄우고 음수면 꽂는다 —
        /// <see cref="HitData.airborneHeight"/>의 부호가 그대로 넘어온다.
        ///
        /// 저작은 높이로 하고 환산은 여기서 한 번만 한다. 대상마다 중력이 다를 수 있으므로
        /// 맞는 쪽의 <see cref="Physics.LaunchForHeight"/>를 쓴다.
        ///
        /// 대상이 이미 떠 있으면 <c>aerialAirborneHeight</c>가 우선하고, 0이면
        /// <c>airborneHeight</c>로 떨어진다.
        ///
        /// 공중 대상에는 마지막으로 <see cref="Physics.AirHitLift"/>를 바닥값으로 깐다.
        /// 띄우기 값이 없는 평타도 한 대마다 조금씩 올려 체공을 벌어 주기 위한 것이다.
        /// <b>지상 대상은 건드리지 않는다</b> — 지상에까지 걸면 모든 평타가 띄우기가 되어
        /// 지상 콤보가 통째로 사라진다.
        /// <b>내려찍기도 건드리지 않는다</b> — 부양을 깔면 꽂으려던 몸이 도로 떠오른다.
        /// </summary>
        private float ResolveLaunch(in HitData hit)
        {
            if (physics.PhysicsState != PhysicsState.Aerial)
                return SignedLaunch(hit.airborneHeight);

            float height = hit.aerialAirborneHeight > 0f ? hit.aerialAirborneHeight : hit.airborneHeight;
            float launch = SignedLaunch(height);

            return launch < 0f ? launch : Mathf.Max(launch, physics.AirHitLift);
        }

        /// <summary>높이의 크기로 속도를 구하고 부호를 되돌려 준다. 음수 높이 = 아래로.</summary>
        private float SignedLaunch(float height)
            => Mathf.Sign(height) * physics.LaunchForHeight(Mathf.Abs(height));

        /// <summary>
        /// 끌어당기기의 이동 거리를 <b>중심까지의 거리</b>로 잘라 준다.
        ///
        /// <see cref="Physics.AddImpulse"/>는 지수감쇠라 이동거리가 충격량에만 비례하고
        /// 시작 거리와는 무관하다. 그래서 고정값을 쓰면 중심 가까이 있던 적이 중심을 지나쳐
        /// 반대편으로 튀고, 맞은편 적과 교차하면서 오히려 흩어진다.
        ///
        /// 잘라 두면 <c>pushDistance</c>는 "최대 끌어올 거리"의 의미가 되고,
        /// 멀리 있는 적만 거리를 다 쓰므로 전원이 중심에 모인다.
        ///
        /// 저작값이 이미 거리라 <b>여기는 거리 공간에서 끝난다</b> — 힘으로의 환산은 호출부 한 곳뿐이다.
        /// </summary>
        private float PushClamped(in HitData hit, Vector3 center)
        {
            if (hit.mode != KnockbackMode.TowardCaster) return hit.pushDistance;

            Vector3 flat = center - transform.position;
            flat.y = 0f;

            return Mathf.Min(hit.pushDistance, flat.magnitude);
        }

        // ── 벽 ──────────────────────────────────────────

        private void HandleWallHit(Physics.WallHit wall)
        {
            // 벽 모서리에서 접촉이 연달아 들어오면 같은 자리에서 계속 튕긴다.
            if (wallBounceTimer > 0f) return;

            // 벽 스턴(약경직 + 강한 밀림)은 여기서 재지 않는다 — 감쇠 때문에 접촉 순간 속도가
            // 이미 문턱 아래이고, 벽에 붙은 적은 접촉 이벤트조차 안 뜬다. <see cref="TryWallStun"/> 참고.
            CombatState next = CombatStateRules.OnWallContact(CombatState);
            if (next == CombatState) return;

            wallBounceTimer = wallBounceCooldown;

            // 세게 처박을수록 크게 튕기고 높이 뜬다. 0~1로 정규화해 한 값으로 전부 몬다.
            float force = Mathf.Clamp01(wall.speed / Mathf.Max(0.01f, wallHardSpeed));

            SetCombatState(next);

            float back = physics.Reflect(wall.normal, wallRestitution, wallMinBounce);
            physics.AddLaunch(Mathf.Lerp(wallMinLaunch, wallMaxLaunch, force));

            EmitWallVfx(in wall, force);

            BattleLog.Log(LogCategory.Physics,
                $"{name} <b>벽 바운드</b> | {CombatState} → <b>{next}</b> | " +
                $"충돌 {wall.speed:0.#} → 반사 {back:0.#} (세기 {force * 100f:0}%)", this);
        }

        /// <summary>부딪힌 벽면에서 터뜨린다. 세게 박을수록 크게.</summary>
        private void EmitWallVfx(in Physics.WallHit wall, float force)
        {
            Vector3 ground = wall.point;
            float height = Mathf.Max(0f, ground.y - physics.GroundY);
            ground.y = 0f;

            // 방향은 벽 바깥쪽 — 파편이 벽에서 튀어나오는 것처럼 읽힌다.
            BattleVfx.WallBounce(ground, height, wall.normal, Mathf.Lerp(0.5f, 1.3f, force));
        }
    }
}
