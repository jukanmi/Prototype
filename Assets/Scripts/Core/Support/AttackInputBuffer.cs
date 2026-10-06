using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 평타 선입력. 누른 순간을 <b>잠깐 기억</b>했다가 다음 타로 넘어갈 수 있게 되면 꺼내 쓴다.
    ///
    /// <see cref="Control.Command"/>는 한 프레임짜리라(<c>Tick</c>이 맨 앞에서 <c>Clear</c>한다)
    /// 공격 모션 도중에 누른 입력이 그냥 사라졌다. 프레임을 정확히 맞춰야만 연타가 되는 건
    /// 조작이 아니라 운이다.
    ///
    /// <b>큐가 아니라 1회성</b>이다. 마구 두들겨서 4타 · 5타가 예약되면 안 된다 —
    /// 한 번 소비하면 비고, 다음 타는 다시 눌러야 한다.
    /// </summary>
    public struct AttackInputBuffer
    {
        private float remaining;

        /// <summary>지금 꺼내 쓸 입력이 남아 있는지.</summary>
        public bool HasInput => remaining > 0f;

        /// <summary>눌렀다. 이미 남아 있어도 창을 새로 채운다(마지막에 누른 것이 기준).</summary>
        public void Press(float window) => remaining = Mathf.Max(0f, window);

        /// <summary>
        /// 시간을 흘린다. 불릿타임에는 <c>dt == 0</c>이라 창이 얼어붙는다 —
        /// 시간이 멈춘 동안 선입력만 혼자 만료되면 조준하고 나온 순간 콤보가 끊긴다.
        /// </summary>
        public void Tick(float dt)
        {
            if (remaining <= 0f) return;
            remaining = Mathf.Max(0f, remaining - dt);
        }

        /// <summary>남아 있으면 true를 내고 비운다.</summary>
        public bool TryConsume()
        {
            if (remaining <= 0f) return false;

            remaining = 0f;
            return true;
        }

        public void Clear() => remaining = 0f;
    }
}
