// 스킬 전용 연출 — 시전자 몸에서 나오는 참격 · 장판 · 사슬.
// BattleVfx(적중 순간 스프라이트 한 장)와 따로 논다. 이쪽은 스킬 하나의 타임라인 전체를 그린다.
//
// SkillState가 세 군데서 부른다: 시전(OnCast) · 매 프레임(Tick) · 타격마다(OnHit).
// 타격 시각은 SkillData.HitTime이 유일한 출처라, 타격보다 먼저 시작해야 하는 연출
// (사슬이 날아가 꽂히는 순간 = 타격)도 같은 함수로 거꾸로 계산한다.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>스킬이 쓰는 전용 연출. <see cref="SkillData.fx"/>에서 고른다.</summary>
    public enum SkillFxKind
    {
        None,
        UpwardSlash,   // 올려베기
        SpinSlash,     // 회전베기
        FrostField,    // 서릿발
        FlashSlash,    // 일섬
        ComboSlash,    // 연격
        HookChain,     // 갈고리 사슬
        ChainBind,     // 사슬 속박
        MagicMissile,  // 마력 화살
        ManaRift,      // 마력 균열
        ShoulderCharge,// 숄더 차지
        ManaSpear,     // 마나 스피어
    }

    /// <summary>
    /// 스킬 시전 한 번의 연출 감독. <see cref="SkillState"/>가 시전마다 하나 만든다.
    ///
    /// 연출 컴포넌트(SlashVFX 등)는 원래 2D 평면(XY)용이다. 여기서 카메라를 마주 보게
    /// (<see cref="BeltScroll.Billboard"/>) 돌려 세우면 그 평면이 곧 화면이 된다 —
    /// 로컬 X는 화면 오른쪽, 로컬 Y는 화면 위쪽이다.
    /// 바닥에 눕는 연출(장판 · 회전 범위)은 깊이 1이 화면에서 sinθ로 눌려 보이므로
    /// groundTilt에 <see cref="BeltScroll.ScreenUp"/>.z를 넣는다.
    /// </summary>
    public abstract class SkillFx
    {
        /// <summary>연출 전체 크기 배율. 원본은 키 약 1.9 유닛 캐릭터 기준이다.</summary>
        public static float SizeScale = 1f;

        /// <summary>발밑에서 몸통 중심까지 — <b>화면 위</b> 방향 거리. 참격의 중심이 여기 온다.</summary>
        public const float BodyLift = 0.9f;

        // 정렬 순서는 시전자 깊이(-z·100) 기준 상대값. 캐릭터 정렬(BeltScrollView)과 같은 공식이다.
        protected const int FrontOrder = 60;      // 시전자보다 앞
        protected const int BackOrder = -60;      // 시전자보다 뒤 (회전 범위의 먼 쪽 절반)
        protected const int FloorOrder = -5000;   // 모든 캐릭터보다 뒤, 배경(-10000)보다 앞

        protected readonly SkillData data;
        protected readonly SkillContext ctx;

        public static SkillFx Create(SkillData data, in SkillContext ctx)
        {
            if (data == null || ctx.caster == null || !BattleVfx.Enabled) return null;

            switch (data.fx)
            {
                case SkillFxKind.UpwardSlash: return new UpwardSlashFx(data, in ctx);
                case SkillFxKind.SpinSlash: return new SpinSlashFx(data, in ctx);
                case SkillFxKind.FrostField: return new FrostFieldFx(data, in ctx);
                case SkillFxKind.FlashSlash: return new FlashSlashFx(data, in ctx);
                case SkillFxKind.ComboSlash: return new ComboSlashFx(data, in ctx);
                case SkillFxKind.HookChain: return new HookChainFx(data, in ctx);
                case SkillFxKind.ChainBind: return new ChainBindFx(data, in ctx);
                case SkillFxKind.MagicMissile: return new MagicMissileFx(data, in ctx);
                case SkillFxKind.ManaRift: return new ManaRiftFx(data, in ctx);
                case SkillFxKind.ShoulderCharge: return new ShoulderChargeFx(data, in ctx);
                case SkillFxKind.ManaSpear: return new ManaSpearFx(data, in ctx);
                default: return null;
            }
        }

        protected SkillFx(SkillData data, in SkillContext ctx)
        {
            this.data = data;
            this.ctx = ctx;
        }

        /// <summary>시전 시작. 자리 잡기(PlaceCaster)가 끝난 뒤라 시전자 위치가 최종이다.</summary>
        public virtual void OnCast() { }

        /// <summary>매 프레임. time = 시전 시작부터 흐른 시간 (<see cref="SkillData.HitTime"/>과 같은 시계).</summary>
        public virtual void Tick(float time) { }

        /// <summary>index번째 타격이 나간 직후. 파고들기 이동까지 끝난 뒤다.</summary>
        public virtual void OnHit(int index) { }

        /// <summary>시전이 끝났거나 끊겼다. 남은 연출은 끝까지 재생되고, 다 끝나면 다른 시전이 다시 빌려 간다.</summary>
        public virtual void Release() { }

        /// <summary>
        /// 이 연출이 투사체를 대신 그리는지. 참이면 SkillState가 쏜 투사체의 렌더러를 끈다 —
        /// 판정 · 착탄 이펙트는 그대로 두고 그림만 바꾼다.
        /// </summary>
        public virtual bool HidesProjectile => false;

        /// <summary>투사체 그림 끄기. 프리팹은 다른 스킬과 공유하므로 생성된 사본에만 한다.</summary>
        public static void HideRenderers(GameObject go)
        {
            if (go == null) return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        // ── 시전자 · 대상 위치 ─────────────────────────────

        protected Entity Caster => ctx.caster;
        protected Physics Phys => ctx.CasterPhysics;
        protected int HitCount => data.hitDataList != null ? data.hitDataList.Count : 0;

        /// <summary>바라보는 쪽이 화면 오른쪽인지. 깊이 방향만 보고 있으면 오른쪽으로 친다.</summary>
        protected bool FacingRight => Phys == null || Phys.Facing.x >= -0.0001f;
        protected float FacingSign => FacingRight ? 1f : -1f;

        /// <summary>바닥 방향(수평) 정면. 없으면 화면 오른쪽.</summary>
        protected Vector3 FlatFacing
        {
            get
            {
                Vector3 f = Phys != null ? Phys.Facing : Vector3.right;
                f.y = 0f;
                return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.right;
            }
        }

        /// <summary>발밑(점프 높이 포함). 엔티티 루트가 곧 이 자리다.</summary>
        protected static Vector3 Feet(Entity e) => e.transform.position;

        /// <summary>몸통 중심. 스프라이트가 화면을 보고 서 있으므로 화면 위로 올린다.</summary>
        protected static Vector3 BodyCenter(Entity e) => Feet(e) + BeltScroll.ScreenUp * (BodyLift * SizeScale);

        /// <summary>캐릭터와 같은 깊이 정렬 기준값.</summary>
        protected static int DepthOrder(Vector3 world) => Mathf.RoundToInt(-world.z * 100f);

        /// <summary>
        /// 부채꼴 · 원형 타의 반지름. SkillState.ConeRadius와 같은 규약 —
        /// 범위 배수의 x를 시전자 피격 가로폭에 곱한다.
        /// </summary>
        protected float ConeRadius(int index)
        {
            if (data.hitDataList == null || index >= data.hitDataList.Count) return 3f;
            return Caster.HurtboxSize.x * data.hitDataList[index].castRangeScale.x;
        }

        /// <summary>두 타격 사이 간격.</summary>
        protected float Gap(int from, int to) => data.HitTime(to) - data.HitTime(from);

        /// <summary>카메라를 마주 보게 세운다. 이 평면이 곧 화면이다.</summary>
        protected static void Stand(Component fx, Vector3 world)
        {
            Transform t = fx.transform;
            t.SetPositionAndRotation(world, BeltScroll.Billboard);
            t.localScale = Vector3.one;
        }

        protected static float GroundTilt => Mathf.Clamp(Mathf.Abs(BeltScroll.ScreenUp.z), 0.1f, 1f);

        protected static VfxLibrary Lib => VfxLibrary.Get();
    }

    // ══ 연출별 감독 ═══════════════════════════════════════════

    /// <summary>올려베기 — 타격마다 아래에서 위로 크게 올려 베는 참격 한 줄기.</summary>
    internal sealed class UpwardSlashFx : SkillFx
    {
        public UpwardSlashFx(SkillData data, in SkillContext ctx) : base(data, in ctx) { }

        public override void OnHit(int index)
        {
            SkillFxPool.Slot slot = SkillFxPool.Acquire<SlashVFX>(f => f.IsPlaying, f =>
            {
                VfxLibrary lib = Lib;
                if (lib != null) { f.slashShader = lib.animeSlash; f.sparkShader = lib.slashSpark; }
                f.playOnEnable = false;
                f.onFinished = SlashVFX.FinishAction.None;
                f.sortingOrder = FrontOrder;
            });
            if (slot == null) return;

            var fx = (SlashVFX)slot.fx;
            fx.ApplyPreset(SlashVFX.Preset.Upward);
            fx.size *= SizeScale;

            // 호의 중심은 몸 약간 앞 — 칼이 몸 앞에서 원을 그린다.
            Stand(fx, BodyCenter(Caster) + BeltScroll.ScreenRight * (0.3f * SizeScale * FacingSign));
            fx.SetFacing(FacingRight);
            slot.SetDepth(DepthOrder(Feet(Caster)));
            fx.Play();

            slot.Release();
        }
    }

    /// <summary>회전베기 — 1 · 2타는 한 바퀴 참격, 마지막 타는 충격파가 터지는 마무리.</summary>
    internal sealed class SpinSlashFx : SkillFx
    {
        private SkillFxPool.Slot slot;

        public SpinSlashFx(SkillData data, in SkillContext ctx) : base(data, in ctx) { }

        public override void OnCast()
        {
            slot = SkillFxPool.Acquire<SpinSlashVFX>(f => f.IsPlaying, f =>
            {
                VfxLibrary lib = Lib;
                if (lib != null) f.spinShader = lib.spinSlash;
                f.sortingOrder = FrontOrder;
                f.backSortingOrder = BackOrder;
            });
        }

        public override void OnHit(int index)
        {
            if (slot == null) return;
            var fx = (SpinSlashVFX)slot.fx;

            // 판정은 시전자를 중심으로 한 원(360도 부채꼴)이다. 앞뒤 · 깊이를 같은 반지름으로 맞춘다.
            float r = ConeRadius(index);
            fx.frontReach = r;
            fx.backReach = r;
            fx.depthReach = r;
            fx.groundTilt = GroundTilt;
            fx.bladeHeight = BodyLift * SizeScale;

            Stand(fx, Feet(Caster));
            fx.SetFacing(FacingRight);
            slot.SetDepth(DepthOrder(Feet(Caster)));

            // 마지막 타는 언제나 마무리(3타), 그 앞은 1 · 2타를 번갈아.
            int hit = index >= HitCount - 1 ? 3 : index % 2 + 1;
            fx.PlayHit(hit);
        }

        public override void Release()
        {
            slot?.Release();
            slot = null;
        }
    }

    /// <summary>
    /// 서릿발 — 1타에 부채꼴 얼음 장판이 퍼지고, 2타에 깨져 터진다.
    /// 깨지기 전 하얗게 달아오르는 시간(chargeTime)이 있으므로, 2타 판정 순간에 "깨짐"이 오도록
    /// 1타 시점에 2타 시작을 미리 예약한다(PlayFull).
    /// </summary>
    internal sealed class FrostFieldFx : SkillFx
    {
        private SkillFxPool.Slot slot;

        public FrostFieldFx(SkillData data, in SkillContext ctx) : base(data, in ctx) { }

        public override void OnHit(int index)
        {
            if (index == 0) Spread();
            else if (index == 1 && slot != null) ((FrostFieldVFX)slot.fx).PlayBurst();   // 예약이 이미 돌았으면 아무 일 없음
        }

        private void Spread()
        {
            slot = SkillFxPool.Acquire<FrostFieldVFX>(f => f.IsPlaying, f =>
            {
                VfxLibrary lib = Lib;
                if (lib != null) f.frostShader = lib.frostField;
                f.sortingOrder = FrontOrder;
                f.floorSortingOrder = FloorOrder;
            });
            if (slot == null) return;
            var fx = (FrostFieldVFX)slot.fx;

            HitData hit = data.hitDataList[0];
            fx.radius = Mathf.Max(0.5f, ConeRadius(0));
            fx.halfAngle = Mathf.Clamp(hit.castConeAngle > 0f ? hit.castConeAngle * 0.5f : 28f, 5f, 80f);
            fx.groundTilt = GroundTilt;
            fx.cellSize = 0.55f * SizeScale;

            Vector3 feet = Feet(Caster);
            Stand(fx, feet);
            slot.SetDepth(DepthOrder(feet));

            if (HitCount >= 2)
            {
                // 1타 → 2타 사이에 [퍼짐 → 대기 → 달아오름]을 다 넣는다. 짧으면 퍼짐부터 줄인다.
                float gap = Gap(0, 1);
                fx.chargeTime = Mathf.Min(0.15f, gap * 0.5f);
                fx.spreadTime = Mathf.Clamp(gap - fx.chargeTime, 0.08f, 0.35f);
                fx.burstDelay = Mathf.Max(0f, gap - fx.chargeTime - fx.spreadTime);
                fx.PlayFull(feet, FacingRight);
            }
            else
            {
                fx.PlaySpread(feet, FacingRight);
            }
        }

        public override void Release()
        {
            slot?.Release();
            slot = null;
        }
    }

    /// <summary>
    /// 일섬 — 1타(파고들기)에 시작점 → 도착점 돌진 궤적, 2타부터 지나온 경로 위로 참격이 쌓였다가
    /// 마지막에 공간째 깨진다. 참격 개수 · 간격은 남은 타격 수와 HitTime에서 그대로 가져온다.
    /// </summary>
    internal sealed class FlashSlashFx : SkillFx
    {
        private SkillFxPool.Slot slot;
        private Vector3 start;

        public FlashSlashFx(SkillData data, in SkillContext ctx) : base(data, in ctx) { }

        public override void OnCast()
        {
            // 파고들기 전 자리. 1타에서 시전자가 이동하므로 지금 찍어 둔다.
            start = BodyCenter(Caster);

            slot = SkillFxPool.Acquire<FlashSlashVFX>(f => f.IsPlaying, f =>
            {
                VfxLibrary lib = Lib;
                if (lib != null) f.flashShader = lib.flashSlash;
                f.sortingOrder = FrontOrder;
            });
        }

        public override void OnHit(int index)
        {
            if (slot == null) return;
            var fx = (FlashSlashVFX)slot.fx;

            if (index == 0) Dash(fx);
            else if (index == 1) fx.PlaySlashes();
        }

        private void Dash(FlashSlashVFX fx)
        {
            Vector3 end = BodyCenter(Caster);

            // 벽에 막혀 못 움직였으면 판정 깊이만큼 앞으로 긋는다.
            if ((end - start).sqrMagnitude < 0.09f)
            {
                HitData hit = data.hitDataList[0];
                float depth = Caster.HurtboxSize.z * Mathf.Max(hit.castRangeScale.z, 1f);
                end = start + FlatFacing * Mathf.Max(1.5f, depth);
            }

            // 원본은 6.5 유닛 돌진 기준이다. 경로가 짧으면 참격 길이 · 흩어짐도 같이 줄인다.
            float path = Vector3.Distance(start, end);
            float k = Mathf.Clamp(path / 6.5f, 0.4f, 1f) * SizeScale;
            fx.slashLength = new Vector2(3.5f, 6.0f) * k;
            fx.slashSpread = 0.45f * Mathf.Max(k, 0.6f);
            fx.glassCellSize = Mathf.Max(0.2f, 0.9f * Mathf.Max(k, 0.6f));

            int rest = HitCount - 1;
            if (rest >= 1)
            {
                fx.slashCount = rest;
                fx.slashInterval = rest >= 2 ? Mathf.Max(0.005f, Gap(1, HitCount - 1) / (rest - 1)) : 0.04f;
                fx.dashTime = Mathf.Clamp(Gap(0, 1) * 0.8f, 0.02f, 0.10f);
            }

            Stand(fx, Feet(Caster));
            slot.SetDepth(DepthOrder(Feet(Caster)));
            fx.PlayDash(start, end);
        }

        public override void Release()
        {
            slot?.Release();
            slot = null;
        }
    }

    /// <summary>연격 — 횡베기 → 내려베기 → 올려베기. 타격마다 다음 줄기.</summary>
    internal sealed class ComboSlashFx : SkillFx
    {
        private SkillFxPool.Slot slot;

        public ComboSlashFx(SkillData data, in SkillContext ctx) : base(data, in ctx) { }

        public override void OnCast()
        {
            slot = SkillFxPool.Acquire<ComboSlashVFX>(f => f.IsPlaying, f =>
            {
                VfxLibrary lib = Lib;
                if (lib != null) { f.slashShader = lib.animeSlash; f.sparkShader = lib.slashSpark; }
                f.buildScale = SizeScale;
                f.sortingOrder = FrontOrder;
            });
        }

        public override void OnHit(int index)
        {
            if (slot == null) return;
            var fx = (ComboSlashVFX)slot.fx;

            Stand(fx, BodyCenter(Caster));
            fx.SetFacing(FacingRight);
            slot.SetDepth(DepthOrder(Feet(Caster)));
            fx.PlayHit(Mathf.Min(index + 1, Mathf.Max(1, fx.HitCount)));
        }

        public override void Release()
        {
            slot?.Release();
            slot = null;
        }
    }

    /// <summary>
    /// 갈고리 사슬 — 손에서 사슬 여러 가닥이 사거리 끝까지 뻗었다가 곧바로 회수된다.
    /// 대상 위치와 무관하다 — 적에게 닿아도 멈추지 않는다.
    /// 사슬이 사거리 끝에 닿는 순간(launchTime)이 1타와 겹치도록 그만큼 먼저 쏜다.
    /// 양 끝은 매 프레임 시전자 몸통 중심에서 다시 계산한다.
    /// </summary>
    internal sealed class HookChainFx : SkillFx
    {
        /// <summary>사거리 끝에 닿은 뒤 회수를 시작하기까지. 짧게 둬야 "뻗었다 돌아온다"로 읽힌다.</summary>
        private const float HoldAtRangeEnd = 0.06f;

        private SkillFxPool.Slot slot;
        private bool fired;
        private float fireTime;
        private Vector3 lastTarget;

        public HookChainFx(SkillData data, in SkillContext ctx) : base(data, in ctx) { }

        public override void OnCast()
        {
            slot = SkillFxPool.Acquire<HookChainVFX>(f => f.IsPlaying, f =>
            {
                VfxLibrary lib = Lib;
                if (lib != null) f.chainShader = lib.hookChain;
                f.sortingOrder = FrontOrder;
            });
            if (slot == null) return;

            var fx = (HookChainVFX)slot.fx;
            fireTime = Mathf.Max(0f, data.HitTime(0) - fx.launchTime);
            if (fireTime <= 0f) Fire();
        }

        public override void Tick(float time)
        {
            if (!fired && time >= fireTime) Fire();
        }

        public override void OnHit(int index)
        {
            if (!fired) Fire();
        }

        private void Fire()
        {
            fired = true;
            if (slot == null) return;
            var fx = (HookChainVFX)slot.fx;

            Entity caster = Caster;
            float side = FacingSign;

            // 대상과 무관하게 항상 사거리(부채꼴 반지름) 끝까지 뻗었다가 곧바로 회수한다.
            // 적에게 닿은 자리에서 멈추면 "사거리 안의 적을 다 훑어 끌어온다"가 안 읽힌다.
            Vector3 reach = FlatFacing * ConeRadius(0);
            Vector3 hand = BeltScroll.ScreenRight * (0.35f * SizeScale * side);
            Vector3 lastHand = BodyCenter(caster) + hand;
            lastTarget = BodyCenter(caster) + reach;

            fx.holdTime = HoldAtRangeEnd;
            fx.originProvider = () =>
            {
                if (caster != null) lastHand = BodyCenter(caster) + hand;
                return lastHand;
            };
            fx.targetProvider = () =>
            {
                if (caster != null) lastTarget = BodyCenter(caster) + reach;
                return lastTarget;
            };

            Stand(fx, Feet(caster));
            slot.SetDepth(DepthOrder(Feet(caster)));
            fx.Fire(lastHand, lastTarget);
        }

        public override void Release()
        {
            slot?.Release();
            slot = null;
        }
    }

    /// <summary>
    /// 사슬 속박 — 대상을 사슬 두 줄이 감아 조인다. 다 조여드는 순간이 1타와 겹치도록 그만큼 먼저 감고,
    /// 공중 속박(AirBind)이 풀리거나 대상이 쓰러지면 끊어져 흩어진다. 시전이 끝나도 속박 동안 계속 본다.
    /// </summary>
    internal sealed class ChainBindFx : SkillFx
    {
        private SkillFxPool.Slot slot;
        private bool bound;
        private float bindTime;

        public ChainBindFx(SkillData data, in SkillContext ctx) : base(data, in ctx) { }

        public override void OnCast()
        {
            if (ctx.target == null || HitCount == 0) return;

            slot = SkillFxPool.Acquire<ChainBindVFX>(f => f.IsPlaying, f =>
            {
                VfxLibrary lib = Lib;
                if (lib != null) f.bindShader = lib.chainBind;
                f.sortingOrder = FrontOrder;
                f.backSortingOrder = BackOrder;
            });
            if (slot == null) return;

            var fx = (ChainBindVFX)slot.fx;
            bindTime = Mathf.Max(0f, data.HitTime(0) - (fx.appearTime + fx.tightenTime));
            if (bindTime <= 0f) Bind();
        }

        public override void Tick(float time)
        {
            if (!bound && slot != null && time >= bindTime) Bind();
        }

        public override void OnHit(int index)
        {
            if (!bound && slot != null) Bind();
        }

        private void Bind()
        {
            bound = true;
            Entity target = ctx.target;
            var fx = (ChainBindVFX)slot.fx;
            SkillFxPool.Slot held = slot;
            slot = null;                       // 이제 감시자가 들고 간다. Release()가 놓지 않는다.

            if (target == null) { held.Release(); return; }

            HitData hit = data.hitDataList[0];
            float duration = hit.debuffDuration > 0f ? hit.debuffDuration : 1f;

            fx.radius = 2.5f * SizeScale;
            fx.targetOffset = new Vector2(0f, BodyLift * SizeScale);
            Stand(fx, Feet(target));
            held.SetDepth(DepthOrder(Feet(target)));
            fx.Bind(target.transform, duration);

            // 속박이 일찍 풀리면(해제 · 사망) 그 자리에서 끊는다. 걸린 적이 없으면(면역 · 헛침) 조여든 직후 끊는다.
            bool seen = false;
            float waited = 0f;
            SkillFxPool.Watch(dt =>
            {
                if (!fx.IsPlaying) { held.Release(); return false; }

                bool dead = target == null || (target.Combat != null && target.Combat.IsDead);
                bool has = !dead && target.HasDebuff(Debuff.AirBind);
                if (has) seen = true;

                waited += dt;
                if (dead || (seen && !has) || (!seen && fx.IsBound && waited > 0.5f))
                {
                    fx.Release();
                    // 끊어지는 연출이 끝날 때까지 빌려주지 않는다.
                    SkillFxPool.Watch(_ => { if (fx.IsPlaying) return true; held.Release(); return false; });
                    return false;
                }
                return true;
            });
        }

        public override void Release()
        {
            // 감기 전에 시전이 끊겼으면 빌린 것만 돌려준다.
            slot?.Release();
            slot = null;
        }
    }

    /// <summary>
    /// 마력 화살 — 몸에서 나온 마력탄이 주위를 돌다가 대상 히트박스 위 탄착점으로 하나씩 날아간다.
    /// 판정은 실제 투사체(도착 폭발)가 한다. 마력탄이 그 투사체와 <b>같은 순간</b>에 닿도록
    /// 퍼짐 · 간격 · 비행 시간을 HitTime과 탄속에서 거꾸로 맞추고, 투사체 그림은 끈다.
    /// 탄착점도 타별 impactOffset(대상 크기 배수)을 그대로 쓴다.
    /// </summary>
    internal sealed class MagicMissileFx : SkillFx
    {
        private SkillFxPool.Slot slot;
        private bool fired;

        public MagicMissileFx(SkillData data, in SkillContext ctx) : base(data, in ctx) { }

        public override bool HidesProjectile => fired;

        public override void OnCast()
        {
            Entity target = ctx.target;
            int n = HitCount;
            if (target == null || n == 0) return;      // 대상이 없으면 투사체 그림을 그대로 둔다

            slot = SkillFxPool.Acquire<MagicMissileVFX>(f => f.IsPlaying, f =>
            {
                VfxLibrary lib = Lib;
                if (lib != null) f.missileShader = lib.magicMissile;
                f.sortingOrder = FrontOrder;
            });
            if (slot == null) return;
            var fx = (MagicMissileVFX)slot.fx;

            // 히트박스 배수 오프셋(-0.5..0.5) → 반폭 기준 앵커(-1..1)
            var anchors = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 o = data.hitDataList[i].impactOffset;
                anchors[i] = new Vector2(Mathf.Clamp(o.x * 2f, -1f, 1f), Mathf.Clamp(o.y * 2f, -1f, 1f));
            }
            fx.impactAnchors = anchors;

            // i번째 투사체는 HitTime(i)에 떠나 거리/탄속 뒤에 닿는다. 마력탄도 그 시각에 맞춘다.
            float first = data.HitTime(0);
            fx.hoverTime = Mathf.Min(0.10f, first * 0.3f);
            fx.emergeTime = Mathf.Max(0.05f, first - fx.hoverTime);
            fx.stagger = n >= 2 ? Mathf.Max(0f, (data.HitTime(n - 1) - first) / (n - 1)) : 0f;
            float dist = Vector3.Distance(BodyCenter(Caster), BodyCenter(target));
            fx.flightTime = Mathf.Clamp(dist / Mathf.Max(1f, data.projectileSpeed), 0.05f, 0.6f);
            fx.casterOffset = new Vector2(0f, BodyLift * SizeScale);

            Vector3 size = target.HurtboxSize;
            Stand(fx, Feet(Caster));
            slot.SetDepth(DepthOrder(Feet(Caster)));
            fx.Fire(Caster.transform, target.transform, new Vector2(size.x, size.y), new Vector2(0f, size.y * 0.5f));
            fired = true;
        }

        public override void Release()
        {
            slot?.Release();
            slot = null;
        }
    }

    /// <summary>
    /// 마력 균열 — 설치 지점에 균열이 형성(1타 전까지)되고, 폭풍이 몰아치다가 마지막 타에 접혀 폭발한다.
    /// 설치기(Installation)는 시전 순간 놓이고 같은 HitTime 시계로 터지므로, 시전 때 한 번 열면 끝까지 맞는다.
    /// </summary>
    internal sealed class ManaRiftFx : SkillFx
    {
        public ManaRiftFx(SkillData data, in SkillContext ctx) : base(data, in ctx) { }

        public override void OnCast()
        {
            int n = HitCount;
            if (n == 0) return;

            SkillFxPool.Slot slot = SkillFxPool.Acquire<ManaRiftVFX>(f => f.IsPlaying, f =>
            {
                VfxLibrary lib = Lib;
                if (lib != null) f.riftShader = lib.manaRift;
                f.sortingOrder = FrontOrder;
                f.floorSortingOrder = FloorOrder;
            });
            if (slot == null) return;
            var fx = (ManaRiftVFX)slot.fx;

            HitData first = data.hitDataList[0];
            fx.radius = Mathf.Max(0.5f, data.RadiusFor(in first));
            fx.groundTilt = GroundTilt;
            fx.coreHeight = BodyLift * SizeScale;

            // 형성 = 첫 타까지, 폭발 = 마지막 타. 폭발은 접히는 시간(collapseTime) 뒤에 온다.
            float end = data.HitTime(n - 1);
            fx.formTime = Mathf.Max(0.05f, data.HitTime(0));
            fx.collapseTime = Mathf.Clamp((end - fx.formTime) * 0.4f, 0.02f, 0.14f);
            float duration = Mathf.Max(0.01f, end - fx.formTime - fx.collapseTime);

            Vector3 center = ctx.Origin;
            Stand(fx, center);
            slot.SetDepth(DepthOrder(center));
            fx.Open(center, duration);

            slot.Release();
        }
    }

    /// <summary>
    /// 숄더 차지 — 선딜에 발밑에서 기운이 튀고, 돌진(ChargeEffect의 대쉬) 동안 어깨 앞 충격파 · 잔상이 따라가며,
    /// 멈춰 서면 앞으로 터진다. 대쉬는 길이가 정해져 있지 않아 실제 이동이 멎는 순간을 재서 끝낸다.
    /// </summary>
    internal sealed class ShoulderChargeFx : SkillFx
    {
        /// <summary>대쉬가 끝났다고 볼 속도(유닛/초)와, 그래도 안 멈추면 끊을 시간.</summary>
        private const float StopSpeed = 0.5f;
        private const float MaxDashTime = 1.2f;

        private SkillFxPool.Slot slot;
        private bool hit;

        public ShoulderChargeFx(SkillData data, in SkillContext ctx) : base(data, in ctx) { }

        public override void OnCast()
        {
            slot = SkillFxPool.Acquire<ShoulderChargeVFX>(f => f.IsPlaying, f =>
            {
                VfxLibrary lib = Lib;
                if (lib != null) { f.chargeShader = lib.shoulderCharge; f.silhouetteShader = lib.spriteSilhouette; }
                f.sortingOrder = FrontOrder;
                f.floorSortingOrder = FloorOrder;
            });
            if (slot == null) return;
            var fx = (ShoulderChargeVFX)slot.fx;

            fx.bodyHeight = BodyLift * 2f * SizeScale;
            fx.groundTilt = GroundTilt;
            fx.characterSprite = CharacterSprite(Caster);

            Stand(fx, Feet(Caster));
            slot.SetDepth(DepthOrder(Feet(Caster)));
            fx.Play(Caster.transform, FacingSign, 0f);
        }

        public override void OnHit(int index)
        {
            if (hit || slot == null) return;
            hit = true;

            var fx = (ShoulderChargeVFX)slot.fx;
            if (ctx.target != null) fx.PlayHit(BodyCenter(ctx.target));

            // 이 타와 같은 프레임에 대쉬가 걸린다(ILastHitEffect). 여기부터 움직임을 잰다.
            Entity caster = Caster;
            SkillFxPool.Slot held = slot;
            slot = null;
            Vector3 last = Feet(caster);
            bool moved = false;
            float elapsed = 0f;

            SkillFxPool.Watch(dt =>
            {
                if (!fx.IsPlaying) { held.Release(); return false; }

                bool stop;
                if (caster == null) stop = true;
                else
                {
                    Vector3 now = Feet(caster);
                    Vector3 d = now - last;
                    d.y = 0f;
                    float speed = d.magnitude / dt;
                    last = now;
                    if (speed > StopSpeed * 2f) moved = true;
                    elapsed += dt;
                    stop = (moved && speed < StopSpeed) || elapsed > MaxDashTime;
                }

                if (!stop) return true;
                fx.End();
                SkillFxPool.Watch(_ => { if (fx.IsPlaying) return true; held.Release(); return false; });
                return false;
            });
        }

        public override void Release()
        {
            // 들이받기 전에 시전이 끊겼으면 그 자리에서 마무리한다.
            if (slot == null) return;
            ((ShoulderChargeVFX)slot.fx).End();
            slot.Release();
            slot = null;
        }

        /// <summary>잔상 · 오라에 쓸 몸 스프라이트. 그림자도 SpriteRenderer라 BeltScrollView의 몸 노드를 먼저 본다.</summary>
        private static SpriteRenderer CharacterSprite(Entity e)
        {
            BeltScrollView view = e.GetComponentInChildren<BeltScrollView>();
            Transform root = view != null ? view.SpriteRoot : null;
            SpriteRenderer sr = root != null ? root.GetComponentInChildren<SpriteRenderer>() : null;
            return sr != null ? sr : e.GetComponentInChildren<SpriteRenderer>();
        }
    }

    /// <summary>
    /// 마나 스피어 — 가슴 높이에서 앞으로 판정 길이(castRangeScale.z)만큼 마력창이 한 번에 꿰뚫고,
    /// 시전자 쪽에 후폭풍이 인다.
    /// </summary>
    internal sealed class ManaSpearFx : SkillFx
    {
        public ManaSpearFx(SkillData data, in SkillContext ctx) : base(data, in ctx) { }

        public override void OnHit(int index)
        {
            SkillFxPool.Slot slot = SkillFxPool.Acquire<ManaSpearVFX>(f => f.IsPlaying, f =>
            {
                VfxLibrary lib = Lib;
                if (lib != null) f.spearShader = lib.manaSpear;
                f.sortingOrder = FrontOrder;
                f.backSortingOrder = BackOrder;
            });
            if (slot == null) return;
            var fx = (ManaSpearVFX)slot.fx;

            HitData hit = data.hitDataList[index];
            float length = Caster.HurtboxSize.z * Mathf.Max(hit.castRangeScale.z, 1f);
            float forward = 0.6f * SizeScale;

            fx.muzzleOffset = new Vector2(forward, BodyLift * SizeScale);
            fx.groundTilt = GroundTilt;

            Vector3 feet = Feet(Caster);
            Vector3 muzzle = feet + FlatFacing * forward + BeltScroll.ScreenUp * (BodyLift * SizeScale);
            Stand(fx, feet);
            slot.SetDepth(DepthOrder(feet));
            fx.FireTo(muzzle, muzzle + FlatFacing * Mathf.Max(0.5f, length - forward));

            slot.Release();
        }
    }

    // ══ 풀 ═══════════════════════════════════════════

    /// <summary>
    /// 연출 컴포넌트 풀. 컴포넌트가 재생 중이거나 시전이 붙잡고 있으면(<see cref="Slot.Release"/> 전) 빌려주지 않는다.
    /// 씬에 미리 놓지 않아도 되도록 첫 호출에 스스로 생긴다(<see cref="VfxRunner"/>와 같은 방식).
    /// </summary>
    internal static class SkillFxPool
    {
        internal sealed class Slot
        {
            public MonoBehaviour fx;
            public bool reserved;
            public Func<MonoBehaviour, bool> isPlaying;

            private Renderer[] renderers;
            private int[] baseOrders;

            /// <summary>만들 때 넣어 둔 정렬 값(상대값)을 기억한다. 렌더러는 Awake에서 생기므로 켠 뒤에 부른다.</summary>
            public void CaptureOrders()
            {
                renderers = fx.GetComponentsInChildren<Renderer>(true);
                baseOrders = new int[renderers.Length];
                for (int i = 0; i < renderers.Length; i++) baseOrders[i] = renderers[i].sortingOrder;
            }

            /// <summary>시전자 깊이에 맞춰 정렬 순서를 다시 건다. 상대값 + 깊이.</summary>
            public void SetDepth(int depthOrder)
            {
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].sortingOrder = baseOrders[i] + depthOrder;
            }

            public bool Busy => fx == null || reserved || isPlaying(fx);

            public void Release() => reserved = false;
        }

        private static Transform root;
        private static readonly List<Slot> slots = new List<Slot>();
        private static readonly List<Func<float, bool>> watchers = new List<Func<float, bool>>();
        private static readonly List<Func<float, bool>> stepping = new List<Func<float, bool>>();

        /// <summary>
        /// 시전이 끝난 뒤에도 따라가야 하는 연출(속박이 풀리는 순간, 대쉬가 멎는 순간)을 매 프레임 본다.
        /// step(dt)이 false를 돌려주면 그만 본다. 게임 시계가 멈춘 동안(불릿타임)은 부르지 않는다.
        /// </summary>
        public static void Watch(Func<float, bool> step)
        {
            if (!Application.isPlaying || step == null) return;
            EnsureRoot();
            watchers.Add(step);
        }

        /// <summary>풀 루트에 붙어 감시자를 돌린다.</summary>
        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                float dt = TimeControl.DeltaTime;
                if (dt <= 0f || watchers.Count == 0) return;

                // 감시자 안에서 새 감시자를 붙일 수 있어 사본을 돈다.
                stepping.Clear();
                stepping.AddRange(watchers);
                watchers.Clear();
                for (int i = 0; i < stepping.Count; i++)
                    if (stepping[i](dt)) watchers.Add(stepping[i]);
            }
        }

        /// <summary>Domain Reload가 꺼져 있으면 static이 플레이 세션을 넘어 살아남는다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            root = null;
            slots.Clear();
            watchers.Clear();
        }

        /// <summary>
        /// 놀고 있는 T를 빌린다. 없으면 새로 만든다 — setup은 그때 한 번만, Awake 전에 돈다
        /// (꺼 둔 채 붙이고 값을 채운 뒤 켠다. ComboSlashVFX는 Awake에서 줄기를 짓기 때문).
        /// </summary>
        public static Slot Acquire<T>(Func<T, bool> isPlaying, Action<T> setup) where T : MonoBehaviour
        {
            if (!Application.isPlaying) return null;
            EnsureRoot();

            for (int i = slots.Count - 1; i >= 0; i--)
            {
                Slot s = slots[i];
                if (s.fx == null) { slots.RemoveAt(i); continue; }
                if (!(s.fx is T) || s.Busy) continue;

                s.reserved = true;
                return s;
            }

            var go = new GameObject(typeof(T).Name);
            go.SetActive(false);
            go.transform.SetParent(root, false);

            T fx = go.AddComponent<T>();
            setup?.Invoke(fx);
            go.SetActive(true);

            var slot = new Slot
            {
                fx = fx,
                reserved = true,
                isPlaying = m => isPlaying((T)m),
            };
            slot.CaptureOrders();
            slots.Add(slot);
            return slot;
        }

        private static void EnsureRoot()
        {
            if (root != null) return;

            slots.Clear();
            watchers.Clear();
            var go = new GameObject("[SkillFx]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
            root = go.transform;
        }
    }
}
