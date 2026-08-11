namespace Prototype
{
    /// <summary>
    /// 액션 자산(<c>Assets/Settings/InputSystem_Actions.inputactions</c>)의 맵 · 액션 이름.
    ///
    /// 문자열을 코드 여기저기에 흩뿌리면 자산에서 이름 하나 바꿨을 때 조용히 죽는다.
    /// 여기로 모아 두고 <c>InputActionAssetTests</c>가 자산과 대조한다.
    /// </summary>
    public static class InputActionNames
    {
        /// <summary>전투 중 항상 켜져 있는 맵. 지휘키도 여기 있다.</summary>
        public static class Gameplay
        {
            public const string Map = "Gameplay";

            public const string Move = "Move";
            public const string Attack = "Attack";
            public const string Jump = "Jump";
            public const string Dash = "Dash";

            /// <summary>
            /// 불릿타임 진입 · 실행. 기본 E.
            ///
            /// 예전엔 Execute(Space)를 따로 뒀는데, Order 페이즈에서 둘 다 Resolve로 가는
            /// 같은 동작이라 리바인드 화면에서 서로 다른 기능처럼 보였다. 하나로 합쳤다.
            /// </summary>
            public const string BulletTime = "BulletTime";

            public const string CardUse = "CardUse";

            /// <summary>
            /// 동료 교대. 실시간 전투에는 동료가 한 명만 서 있고 이 키가 다음 생존자로 돌린다.
            ///
            /// 지휘키가 <b>아니다</b> — 정지 중에는 안 먹어야 한다. 그래서
            /// <c>InputRebindRules.CommanderActions</c>에 넣지 않는다. 넣으면 불릿타임 맵의
            /// WASD와 겹친다고 잡힌다.
            /// </summary>
            public const string Swap = "Swap";
        }

        /// <summary>
        /// 손패 카드 조작. Order 페이즈이면서 <b>조준 중이 아닐 때</b>만 켜진다.
        ///
        /// 액션이 <c>Navigate</c> 하나뿐이다. 집기 · 놓기까지 방향에 실어 두면
        /// 한 키가 두 액션을 동시에 발동하는 일이 원천적으로 없고,
        /// 리바인드도 컴포지트 하나만 바꾸면 네 방향이 전부 따라온다.
        ///
        /// 한 번 누르면 한 칸 가는 <b>이산</b> 입력이다 — 같은 WASD라도
        /// 조준의 연속 이동과 성격이 달라 맵을 나눴다.
        /// </summary>
        public static class BulletTime
        {
            public const string Map = "BulletTime";

            public const string Navigate = "Navigate";
        }

        /// <summary>
        /// 스킬 시전 위치 지정. 카드를 집은 상태에서 위(<c>Navigate.up</c>)로 들어온다.
        /// 확정 · 취소 후 다시 <see cref="BulletTime"/> 으로 돌아간다.
        /// </summary>
        public static class BulletTimeSkillShot
        {
            public const string Map = "BulletTimeSkillShot";

            public const string Aim = "Aim";

            /// <summary>마우스 커서의 화면 좌표.</summary>
            public const string AimPoint = "AimPoint";

            /// <summary>
            /// 마우스 이동량. <see cref="AimPoint"/>의 프레임 차분으로 대신하면 안 된다 —
            /// 맵을 켠 첫 프레임에 position이 0을 뱉어서, 실제 좌표가 들어오는 다음 프레임을
            /// "화면 절반만큼 움직였다"로 읽는다.
            /// </summary>
            public const string AimDelta = "AimDelta";

            public const string Confirm = "Confirm";
            public const string Cancel = "Cancel";
        }

        /// <summary>UI 맵. <c>InputSystemUIInputModule</c>이 쓰는 이름이라 바꾸면 안 된다.</summary>
        public static class UI
        {
            public const string Map = "UI";

            public const string Cancel = "Cancel";
        }
    }
}
