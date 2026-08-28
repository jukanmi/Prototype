using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 플레이어가 현재 조작 중인 캐릭터(플레이어 또는 태그 교대된 동료) 머리 위에
    /// 화살표(<c>▼</c>)를 띄워 난전 중에도 내 위치를 즉시 식별할 수 있게 한다.
    ///
    /// <b>그리는 일은 <see cref="MarkerLayer"/>가 한다.</b> 예전에는 이 컴포넌트가
    /// TextMesh를 직접 만들고 매 프레임 그렸는데, 대기 동료 표식이 같은 몸통을 또 한 벌
    /// 만들게 되면서 빌보드 · 깊이 배율 · 정렬 계산이 두 곳에 생겼다. 지금은 여기가
    /// "누구 머리 위에 무엇을"만 답한다.
    ///
    /// <see cref="TagSwapController.Current"/>를 추적하며, 교대 시 새 몸으로 즉시 옮겨간다.
    /// </summary>
    public class ControlledCharacterArrow : MonoBehaviour, IMarkerSource
    {
        [Tooltip("머리 위로 띄우는 기본 높이. 상태 게이지(StatusEffectBar, 2.25)보다 위여야 겹치지 않는다.")]
        [SerializeField] private float headOffset = 2.4f;

        [Tooltip("상하 부유(플로팅) 진폭.")]
        [SerializeField] private float bobHeight = 0.12f;

        [Tooltip("상하 부유 속도.")]
        [SerializeField] private float bobSpeed = 5f;

        [Tooltip("정렬 오프셋. 상태 게이지(340)보다 앞이다.")]
        [SerializeField] private int sortingOffset = 360;

        [Tooltip("월드 단위 글자 크기.")]
        [SerializeField] private float characterSize = 0.08f;

        [Tooltip("화살표 기본 색상.")]
        [SerializeField] private Color arrowColor = new Color(1f, 0.86f, 0.35f, 1f);

        private TagSwapController cachedSwap;

        /// <summary>
        /// 화살표가 놓일 자리.
        ///
        /// 계산 본체는 <see cref="WorldMarker.Position"/>으로 옮겼다. 이 시그니처를 남겨 두는 이유는
        /// 기존 테스트가 여기를 부르고 있어서다 — 규칙이 바뀌지 않았음을 그쪽이 계속 지켜 준다.
        /// </summary>
        public static Vector3 ArrowPosition(Vector3 ground, float height, float headOffset, float bobOffset = 0f)
            => WorldMarker.Position(ground, height, headOffset, bobOffset);

        /// <summary>시간에 따른 상하 부유 오프셋 계산 (순수 함수).</summary>
        public static float BobOffset(float unscaledTime, float speed, float height)
            => Mathf.Sin(unscaledTime * speed) * height;

        public void Collect(List<MarkerRequest> into)
        {
            Entity target = ResolveTarget();
            if (target == null) return;

            Physics phys = target.Physics;
            if (phys == null) return;

            into.Add(new MarkerRequest
            {
                ground = phys.GroundPosition,
                height = phys.Height,
                headOffset = headOffset,
                glyph = "▼",
                color = arrowColor,
                sortingOffset = sortingOffset,
                size = characterSize,
                bob = BobOffset(Time.unscaledTime, bobSpeed, bobHeight),

                // 방향이 곧 뜻인 글리프다. 등장 연출로 화면 밖에 있을 때
                // 가장자리에서 "저쪽에서 오는 중"을 가리킨다.
                tilt = true,
            });
        }

        private Entity ResolveTarget()
        {
            if (cachedSwap == null)
                cachedSwap = FindAnyObjectByType<TagSwapController>();

            if (cachedSwap != null && cachedSwap.Current != null)
                return Usable(cachedSwap.Current);

            // 태그 컨트롤러가 없는 씬을 위한 폴백: 아군 중 조종사가 물고 있는 몸
            var allies = BattleRegistry.Allies;
            if (allies != null)
            {
                for (int i = 0; i < allies.Count; i++)
                {
                    Entity e = allies[i];
                    if (e != null && e.IsPiloted && Usable(e) != null) return e;
                }
            }

            return null;
        }

        /// <summary>
        /// 그려도 되는 몸인가. 꺼져 있거나 죽었으면 표식도 없다.
        ///
        /// <b>등장 연출 중에도 그린다</b> — 그때가 오히려 "내가 어디 있는지"가 가장 안 보이는
        /// 구간이고, 화면 밖이면 <see cref="MarkerLayer"/>가 가장자리에 물려 준다.
        /// </summary>
        private static Entity Usable(Entity e)
        {
            if (e == null || !e.gameObject.activeSelf) return null;
            if (e.Combat == null || e.Combat.IsDead) return null;

            return e;
        }
    }
}
