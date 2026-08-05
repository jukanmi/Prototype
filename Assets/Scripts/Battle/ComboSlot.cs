using System;

namespace Prototype
{
    /// <summary>
    /// 카드 한 장의 실행 단위. 카드 · 조준값 · 시전자를 함께 묶는다.
    /// <see cref="Hand"/>가 이 구조체를 그대로 들고 있어 손패 = 실행 순서가 된다.
    /// </summary>
    [Serializable]
    public struct ComboSlot
    {
        public ComboCard card;

        /// <summary>유저가 찍었거나 자동으로 채워진 조준값.</summary>
        public TargetInfo target;

        /// <summary>유저가 직접 조준했는지. false면 발동 직전에 자동 조준으로 채운다.</summary>
        public bool aimed;

        /// <summary>이 카드를 실행할 동료. 발동 직전에 카드의 직업으로 결정된다.</summary>
        public Ally caster;

        public bool IsEmpty => card == null;
        public SkillData Data => card != null ? card.Data : null;
    }
}
