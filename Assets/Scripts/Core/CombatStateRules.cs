using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 전투 상태 전이표를 <b>한 벌만</b> 유지하는 정적 규칙 테이블.
    /// 실전투 판정(Combat)과 콤보 예측(ComboPredictor)이 같은 함수를 호출하므로
    /// 두 벌의 로직이 어긋날 일이 없다.
    /// </summary>
    public static class CombatStateRules
    {
        // ── 공중 콤보 제한 ──────────────────────────────
        /// <summary>공중 피격 1회마다 붙는 중력 가중치.</summary>
        public const float AirGravityStep = 0.35f;
        /// <summary>중력 가중치 상한. 이 이상 무거워지지 않는다.</summary>
        public const float MaxAirGravityScale = 3.5f;
        /// <summary>이 횟수를 넘기면 띄우기가 무시되고 그대로 낙하한다.</summary>
        public const int MaxAirHit = 6;

        /// <summary>
        /// 피격 시 다음 상태를 결정한다.
        /// hitData.nextState는 <b>요청값</b>일 뿐이며, 최종 판정은 이 함수가 내린다.
        /// </summary>
        public static CombatState Next(CombatState cur, in HitData hit, int airHitCount = 0)
        {
            // 사망은 어떤 것도 덮어쓰지 못한다.
            if (cur == CombatState.Dead)
                return CombatState.Dead;

            // 기상 무적 — 판정 자체가 통과하지 않는다.
            if (cur == CombatState.Getup)
                return CombatState.Getup;

            // 다운 무적. 예외적으로 바닥쓸기(OTG)만 관통한다.
            if (cur == CombatState.Down)
                return hit.canOtg ? Arbitrate(cur, hit.nextState, airHitCount) : CombatState.Down;

            return Arbitrate(cur, hit.nextState, airHitCount);
        }

        private static CombatState Arbitrate(CombatState cur, CombatState request, int airHitCount)
        {
            // 공중 피격 중에는 무엇을 맞아도 공중 피격을 유지한다.
            // 단, 밀치기(넉백)만은 예외로 통과시켜 벽 바운드 연계를 허용한다.
            if (cur == CombatState.AerialHit || cur == CombatState.WallBound)
                return request == CombatState.Knockback ? CombatState.Knockback : CombatState.AerialHit;

            switch (request)
            {
                // 띄우기 — Neutral / LightHit / Knockback / Down(OTG) 에서 진입 가능.
                // 공중 히트 상한을 넘겼으면 무시하고 그대로 떨어뜨린다.
                case CombatState.AerialHit:
                    return airHitCount >= MaxAirHit ? cur : CombatState.AerialHit;

                case CombatState.Knockback:
                    return CombatState.Knockback;

                case CombatState.LightHit:
                    return CombatState.LightHit;

                case CombatState.Neutral:
                    // 상태 전이를 요구하지 않는 타격(순수 데미지)은 약경직 처리.
                    return CombatState.LightHit;

                default:
                    return request;
            }
        }

        /// <summary>
        /// 선행 조건 충족 여부. require가 Neutral이면 조건 없음으로 본다.
        /// 콤보 슬롯의 '강화 / 기본' 표시가 이 값으로 갈린다.
        /// </summary>
        public static bool CanChain(CombatState prev, CombatState require)
        {
            if (require == CombatState.Neutral)
                return true;

            // 벽 바운드는 공중 상태의 연장으로 취급한다.
            if (require == CombatState.AerialHit &&
                (prev == CombatState.AerialHit || prev == CombatState.WallBound || prev == CombatState.Knockback))
                return true;

            return prev == require;
        }

        /// <summary>피격 판정이 아예 통과하지 못하는 무적 상태.</summary>
        public static bool IsInvincible(CombatState s, bool otgHit = false)
        {
            if (s == CombatState.Getup) return true;
            if (s == CombatState.Down) return !otgHit;
            return false;
        }

        /// <summary>행동 불능 — Control 입력을 받지 않는 상태.</summary>
        public static bool IsStunned(CombatState s)
        {
            switch (s)
            {
                case CombatState.LightHit:
                case CombatState.AerialHit:
                case CombatState.Knockback:
                case CombatState.WallBound:
                case CombatState.Down:
                case CombatState.Getup:
                case CombatState.Dead:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>공중 히트 누적에 따른 중력 배율. 맞을수록 무겁게 떨어진다.</summary>
        public static float AirGravityScale(int airHitCount)
            => Mathf.Min(1f + airHitCount * AirGravityStep, MaxAirGravityScale);

        /// <summary>벽에 닿았을 때의 전이. 넉백 중이면 벽 바운드.</summary>
        public static CombatState OnWallContact(CombatState cur)
            => cur == CombatState.Knockback ? CombatState.WallBound : cur;

        /// <summary>바닥에 닿았을 때의 전이. 공중 피격 계열은 전부 다운.</summary>
        public static CombatState OnGroundContact(CombatState cur)
        {
            switch (cur)
            {
                case CombatState.AerialHit:
                case CombatState.Knockback:
                case CombatState.WallBound:
                    return CombatState.Down;
                default:
                    return cur;
            }
        }

        /// <summary>경직이 풀렸을 때 돌아갈 상태.</summary>
        public static CombatState OnStunEnd(CombatState cur)
        {
            switch (cur)
            {
                case CombatState.LightHit:
                    return CombatState.Neutral;
                case CombatState.Down:
                    return CombatState.Getup;   // 다운 → 기상(무적)
                case CombatState.Getup:
                    return CombatState.Neutral; // 기상 완료 → 복귀 (airHitCount 리셋 시점)
                default:
                    return cur;
            }
        }
    }
}
