namespace Prototype.YG
{
    /// <summary>
    /// 씬 이름 상수. 문자열 하드코딩으로 인한 오타를 막는다.
    /// </summary>
    public static class SceneNames
    {
        public const string Boot     = "Boot";
        public const string MainMenu = "MainMenu";

        /// <summary>
        /// 전투 씬. 씬 파일명은 아직 SampleScene 이지만 나중에 Battle 로 바뀔 가능성이 높아
        /// 상수명은 용도 기준으로 둔다. 파일명이 바뀌면 이 한 줄만 고치면 된다.
        /// </summary>
        public const string Battle   = "SampleScene";

        /// <summary>
        /// 스테이지 씬. <see cref="Battle"/>(SampleScene)을 복제해 적만 갈아끼운 것이라
        /// 입력 · HUD · 방 · 카메라 배선을 그대로 물려받는다.
        ///
        /// 씬 파일은 <c>Assets/Scenes/Level/</c> 아래에 있지만 <c>LoadScene</c>은 이름으로 찾으므로
        /// 경로가 아니라 파일명만 적는다.
        /// </summary>
        public const string StageMini = "Stage_Mini";
        public const string StageBoss = "Stage_Boss";

        /// <summary>
        /// 원거리 스테이지 두 짝. 둘 다 마법사(<c>Enemy_WizardRanged</c>)로만 채워져 있고,
        /// 같은 프리팹에 <b>다른 EnemyData</b>를 물려 난이도를 가른다.
        ///
        /// Soft는 원거리 적을 어떻게 붙잡는지 배우는 자리, Hard는 그걸 근접 적과 섞어
        /// "붙을 수가 없는" 압박으로 되돌려주는 자리다.
        /// </summary>
        public const string StageSoft = "Stage_Soft";
        public const string StageHard = "Stage_Hard";

        /// <summary>
        /// 훈련장. 허수아비 하나와 <b>고정 손패</b>만 놓은 씬이다 —
        /// 콤보 한 싸이클을 매번 같은 조건에서 굴려 보는 자리.
        /// </summary>
        public const string StageTraining = "Stage_Training";
    }
}
