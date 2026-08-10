using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 모으는 중인 캐릭터 머리 위에 차징 게이지를 띄운다.
    ///
    /// <see cref="ChargeSkillState"/>는 불릿타임 큐가 다 끝난 뒤에 터진다. 그동안 화면에는
    /// 제자리에 선 동료만 보여서 "왜 안 나가지"로 읽혔다. 남은 양이 보이면
    /// 차징기를 앞 슬롯에 둘수록 세진다는 규칙이 눈으로 확인된다.
    ///
    /// <see cref="RangeIndicator"/>와 같은 자리에 붙는다 — 씬 배선 0.
    /// </summary>
    public class ChargeGauge : MonoBehaviour
    {
        [Tooltip("머리 위로 띄우는 높이.")]
        [SerializeField] private float headOffset = 1.6f;

        [Tooltip("게이지 가로 폭(월드 단위). 시트 원본 비율은 유지한다.")]
        [SerializeField] private float width = 1.1f;

        [Tooltip("정렬 오프셋. 캐릭터보다 확실히 앞이어야 한다.")]
        [SerializeField] private int sortingOffset = 300;

        private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();

        // 조준과 마찬가지로 시간이 멈춘 동안에도 보여야 한다.
        // 캐릭터 위치는 LateUpdate에 확정되므로(BeltScrollView) 그 뒤에 읽는다.
        private void LateUpdate()
        {
            VfxLibrary lib = VfxLibrary.Get();
            VfxClip clip = lib != null ? lib.chargeGauge : null;

            int used = 0;
            if (clip != null && clip.IsValid)
            {
                used += DrawAll(BattleRegistry.Allies, clip, used);
                used += DrawAll(BattleRegistry.Enemies, clip, used);
            }

            // 남는 렌더러는 끈다. 파괴하지 않는다 — 다음 차징에 다시 쓴다.
            for (int i = used; i < pool.Count; i++)
                pool[i].enabled = false;
        }

        private int DrawAll(IReadOnlyList<Entity> list, VfxClip clip, int start)
        {
            if (list == null) return 0;

            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                Entity e = list[i];
                if (e == null || e.Combat == null || e.Combat.IsDead) continue;

                if (!(e.StateMachine?.CurState is IChargeState charge) || !charge.IsCharging) continue;

                Draw(Take(start + n), e, clip, charge.ChargeRatio);
                n++;
            }

            return n;
        }

        /// <summary>
        /// 게이지가 놓일 자리. 뒤에 선 적은 몸이 줄어 머리도 내려오므로
        /// 오프셋에도 같은 깊이 배율을 먹인다. 게이지 자체의 크기(width)는 안 건드린다 —
        /// 어느 깊이에서나 같은 크기로 읽혀야 한다.
        /// </summary>
        public static Vector3 GaugePosition(Vector3 ground, float height, float headOffset)
            => BeltScroll.ToView(ground, height + headOffset * BeltScroll.ScaleAt(ground.z));

        private void Draw(SpriteRenderer sr, Entity e, VfxClip clip, float ratio)
        {
            // 시트는 프레임 0이 가득 찬 상태다. 남은 양이 아니라 "채운 양"으로 뒤집어 읽는다.
            sr.sprite = clip.FrameAt(1f - Mathf.Clamp01(ratio));
            if (sr.sprite == null) return;

            Physics phys = e.Physics;
            Vector3 ground = phys.GroundPosition;

            sr.transform.position = GaugePosition(ground, phys.Height, headOffset);
            sr.transform.rotation = Quaternion.identity;

            float w = sr.sprite.bounds.size.x;
            float s = w > 0.0001f ? width / w : 1f;
            sr.transform.localScale = new Vector3(s, s, 1f);

            // 캐릭터와 같은 깊이 정렬 공식(BeltScrollView.ApplySorting) 위에서 오프셋만 준다.
            sr.sortingOrder = Mathf.RoundToInt(-ground.z * 100f) + sortingOffset;
            sr.color = Color.white;
            sr.enabled = true;
        }

        private SpriteRenderer Take(int index)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject("ChargeGauge");
                go.transform.SetParent(transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
                sr.enabled = false;

                pool.Add(sr);
            }

            return pool[index];
        }
    }
}
