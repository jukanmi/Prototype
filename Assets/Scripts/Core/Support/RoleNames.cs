namespace Prototype
{
    /// <summary>
    /// 직업의 화면 표기. <see cref="InputDisplayNames"/>와 같은 자리의 물건이다 —
    /// <c>Role.Tanker</c>를 사람이 읽는 말로 바꾸는 곳이 여러 군데로 갈리지 않게 한다.
    ///
    /// 열거형 이름을 그대로 쓰지 않는 이유는 파티 편성 · 컷인 · 카드가 전부
    /// 같은 말을 써야 하기 때문이다. 여기 한 줄만 고치면 전부 따라온다.
    /// </summary>
    public static class RoleNames
    {
        public static string Of(Role role)
        {
            switch (role)
            {
                case Role.Tanker:  return "탱커";
                case Role.Warrior: return "전사";
                case Role.Archer:  return "궁수";
                case Role.Wizard:  return "마법사";
                default:           return role.ToString();
            }
        }
    }
}
