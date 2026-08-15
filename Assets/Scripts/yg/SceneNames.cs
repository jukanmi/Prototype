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
    }
}
