using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 지금 조작하지 않는 동료들이 <b>화면 밖 어디에 있는지</b>를 가장자리에 표시한다.
    ///
    /// 태그 구조상 한 명만 필드에 서므로 나머지 넷은 화면에서 통째로 사라진다 —
    /// 유저 입장에서는 "누가 남았는지, 어느 쪽에서 나올지"를 알 방법이 없었다.
    ///
    /// <b>좌표를 지어내지 않는다.</b> <see cref="PartyStandby"/>가 들고 있는 그 값을 그대로
    /// 그린다 — 교대로 나간 몸이 실제로 도착하는 자리이고, 다음에 다시 나올 자리다.
    /// 그래서 표식이 틀리면 <b>몸이 다른 데서 나오는 것으로</b> 곧바로 드러난다.
    /// </summary>
    public class PartyMarkerSource : MonoBehaviour, IMarkerSource
    {
        [Tooltip("머리 위로 띄우는 높이. 조작 캐릭터 화살표(2.4)보다 낮게 둬 층을 나눈다.")]
        [SerializeField] private float headOffset = 1.9f;

        [Tooltip("정렬 오프셋. 조작 캐릭터 화살표(360)보다 뒤다 — 겹치면 내 표식이 이겨야 한다.")]
        [SerializeField] private int sortingOffset = 350;

        [Tooltip("월드 단위 글자 크기. 조작 캐릭터 화살표보다 작다.")]
        [SerializeField] private float characterSize = 0.062f;

        [Tooltip("살아 있는 대기 동료의 표식 색.")]
        [SerializeField] private Color aliveColor = new Color(0.62f, 0.78f, 1f, 0.9f);

        [Tooltip("쓰러진 동료의 표식 색. 자리는 그대로 두되 회색으로 죽었음을 알린다.")]
        [SerializeField] private Color deadColor = new Color(0.45f, 0.45f, 0.48f, 0.55f);

        [Tooltip("직업을 글자 하나로 표시한다. 비우면 전부 ◆로 그린다.")]
        [SerializeField] private bool showRoleGlyph = true;

        private readonly List<StandbySeat> seats = new List<StandbySeat>();

        private TagSwapController cachedSwap;

        public void Collect(List<MarkerRequest> into)
        {
            if (cachedSwap == null)
                cachedSwap = FindAnyObjectByType<TagSwapController>();

            if (cachedSwap == null) return;

            cachedSwap.Standby.Collect(seats, cachedSwap.CurrentIndex);

            for (int i = 0; i < seats.Count; i++)
            {
                StandbySeat seat = seats[i];

                into.Add(new MarkerRequest
                {
                    ground = seat.Point,
                    height = 0f,
                    headOffset = headOffset,
                    glyph = Glyph(seat.Body),
                    color = seat.Alive ? aliveColor : deadColor,
                    sortingOffset = sortingOffset,
                    size = characterSize,

                    // 부유는 주지 않는다. 조작 캐릭터 화살표만 움직여야 그쪽이 먼저 눈에 든다.
                    bob = 0f,
                });
            }
        }

        /// <summary>
        /// 직업 한 글자. 표식이 넷이면 "누가 남았는지"가 <b>모양으로</b> 읽혀야 한다 —
        /// 전부 같은 점이면 자리만 알려 주고 정체는 여전히 모른다.
        /// </summary>
        private string Glyph(Entity body)
        {
            if (!showRoleGlyph) return "◆";

            if (body is Ally ally)
            {
                switch (ally.Role)
                {
                    case Role.Tanker:  return "방";
                    case Role.Warrior: return "검";
                    case Role.Archer:  return "궁";
                    case Role.Wizard:  return "법";
                }
            }

            // 플레이어는 직업이 아니라 본인이다.
            return body is Player ? "★" : "◆";
        }
    }
}
