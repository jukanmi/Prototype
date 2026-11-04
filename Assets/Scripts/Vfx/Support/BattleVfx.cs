// 전투 연출 — 진입점 · 스프라이트 재생 · 스킬 표.
// 실제 그림은 VfxSprite가, 스킬별 어떤 연출인지는 SkillVfx(데이터 · Core/Support)가 정한다.
// 아래층은 BattleVfx를 직접 부르지 않고 IBattleVfx(Entities/Support)로 요청한다.
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Prototype
{
    // ══ BattleVfx ═══════════════════════════════════════════

    /// <summary>
    /// 전투 연출 진입점. 호출부는 <b>논리 좌표와 크기만</b> 넘긴다 —
    /// 화면 접기(<see cref="BeltScroll"/>)와 정렬은 여기서 한 번만 처리한다.
    ///
    /// 실제 그림은 <see cref="VfxSprite"/>가 스프라이트 시트로 그린다.
    /// 시트는 <see cref="VfxLibrary"/>(전역) 또는 <see cref="SkillVfx"/>(스킬 전용)에서 온다.
    /// 둘 다 비어 있으면 <b>아무것도 그리지 않는다</b> — 아트가 없던 시절의
    /// LineRenderer 대체 연출은 시트가 들어오면서 걷어냈다.
    /// </summary>
    public static class BattleVfx
    {
        /// <summary>연출을 끄고 싶을 때. 성능 비교나 캡처용.</summary>
        public static bool Enabled = true;

        /// <summary>흑백 시트에 곱하는 전역 색. 스킬이 <c>custom</c>을 켜면 그쪽이 이긴다.</summary>
        public static Color ImpactColor = new Color(1f, 0.86f, 0.45f, 1f);
        public static Color ShockColor = new Color(1f, 0.55f, 0.3f, 0.75f);

        /// <summary>
        /// 스킬 타격은 평타보다 크게 그린다.
        /// 평타가 계속 터지는 난전에서 "지금 스킬이 나갔다"가 읽혀야 한다.
        /// </summary>
        private const float SkillSize = 1.7f;

        /// <summary>
        /// 시전 표시. 스킬이 실제로 터지는 순간, 터지는 자리에 한 번 그린다.
        ///
        /// 불릿타임에서 유저가 찍은 좌표가 곧 여기다 — 조준할 때 본
        /// <see cref="RangeIndicator"/> 타원과 같은 자리·같은 반경으로 나온다.
        /// 시전자 본체 모션과 타격은 순간적이라 "내가 찍은 데서 터졌다"가 안 읽혔다.
        /// </summary>
        public static void Cast(Vector3 ground, float radius, in SkillVfx style = default)
        {
            if (!Enabled) return;

            VfxClip clip = Pick(style.castClip, Library != null ? Library.cast : null);
            if (clip == null) return;

            VfxRunner r = VfxRunner.Instance;
            if (r == null) return;

            Color tint = style.custom ? style.impact : ImpactColor;
            r.SpawnSprite(clip, ground, 0f, Vector3.right, Mathf.Max(0.5f, radius * style.Scale), tint);
        }

        /// <summary>타격. 실제로 맞았을 때만 부른다.</summary>
        public static void Impact(Vector3 ground, float height, Vector3 facing, float radius,
                                  in SkillVfx style = default)
        {
            if (!Enabled) return;

            VfxClip clip = Pick(style.hitClip, Library != null ? Library.impact : null);
            if (clip == null) return;

            VfxRunner r = VfxRunner.Instance;
            if (r == null) return;

            float s = style.Scale * (style.fromSkill ? SkillSize : 1f);

            r.SpawnSprite(clip, ground, height, facing, radius * s,
                          style.custom ? style.impact : ImpactColor);

            // 충격파는 시트가 따로 있을 때만 겹친다. 없으면 타격 한 장으로 끝.
            VfxClip wave = Library != null ? Library.shock : null;
            if (wave != null && wave.IsValid)
                r.SpawnSprite(wave, ground, height, facing, radius * 1.5f * s,
                              style.custom ? style.shock : ShockColor);
        }

        /// <summary>
        /// 벽 바운드. 전용 시트가 없으면 <see cref="Impact"/>와 같은 그림으로 떨어진다.
        /// 벽에 처박는 순간은 콤보에서 제일 잘 보이는 지점이라 칸을 따로 뒀다.
        /// </summary>
        public static void WallBounce(Vector3 ground, float height, Vector3 normal, float radius)
        {
            if (!Enabled) return;

            VfxClip clip = Library != null ? Library.wall : null;
            if (clip == null || !clip.IsValid)
            {
                Impact(ground, height, normal, radius);
                return;
            }

            VfxRunner r = VfxRunner.Instance;
            if (r == null) return;

            r.SpawnSprite(clip, ground, height, normal, radius, ImpactColor);
        }

        private static VfxLibrary Library => VfxLibrary.Get();

        /// <summary>스킬 전용 시트가 우선. 없으면 전역, 그것도 없으면 null(그리지 않음).</summary>
        private static VfxClip Pick(VfxClip own, VfxClip fallback)
        {
            if (own != null && own.IsValid) return own;
            return fallback != null && fallback.IsValid ? fallback : null;
        }

        /// <summary>
        /// 조준 표시(<see cref="RangeIndicator"/>)가 쓰는 공유 머티리얼.
        /// 선이 정점 색을 따르도록 스프라이트 셰이더를 쓴다.
        /// 빌드에서 셰이더가 스트립되지 않도록 Always Included Shaders 확인이 필요하다.
        /// </summary>
        internal static Material CreateMaterial()
        {
            Shader s = Shader.Find("Sprites/Default")
                       ?? Shader.Find("Universal Render Pipeline/Unlit")
                       ?? Shader.Find("Unlit/Color");

            if (s == null)
            {
                BattleLog.Warn(LogCategory.State, "BattleVfx: 셰이더를 찾지 못했다. 연출이 보이지 않는다.");
                return null;
            }

            return new Material(s) { hideFlags = HideFlags.HideAndDontSave };
        }

        // ── IBattleVfx 구현 — 아래층(Entities · Skill)은 BattleVfxSink로 여기를 부른다 ──

#if UNITY_EDITOR
        // 에디트 모드(EditMode 테스트)에서도 꽂혀 있어야 예전 동작과 같다.
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallSink() => BattleVfxSink.Current = new Sink();

        private sealed class Sink : IBattleVfx
        {
            public void Cast(Vector3 ground, float radius, in SkillVfx style)
                => BattleVfx.Cast(ground, radius, in style);

            public void Impact(Vector3 ground, float height, Vector3 facing, float radius, in SkillVfx style)
                => BattleVfx.Impact(ground, height, facing, radius, in style);

            public void WallBounce(Vector3 ground, float height, Vector3 normal, float radius)
                => BattleVfx.WallBounce(ground, height, normal, radius);
        }
    }
}
