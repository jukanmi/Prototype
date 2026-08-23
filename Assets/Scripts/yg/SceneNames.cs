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
        /// 훈련장. 허수아비 하나와 <b>고정 손패</b>만 놓은 씬이다 —
        /// 콤보 한 싸이클을 매번 같은 조건에서 굴려 보는 자리.
        /// </summary>
        public const string StageTraining = "Stage_Training";

        /// <summary>
        /// 본편 스테이지 1~4. 씬에는 적이 <b>하나도 없다</b> —
        /// 웨이브 방은 <see cref="Prototype.StageDirector"/>가, 아레나 방은
        /// <see cref="Prototype.StageRunner"/>가 표를 읽어 그때그때 소환한다.
        /// 그래서 배치를 고칠 때 씬을 열 필요가 없다.
        ///
        /// <see cref="Stage02"/>와 <see cref="Stage05"/>는 성격이 다르다 —
        /// <b>아레나 2개와 그 사이를 잇는 통로</b>로 이뤄진 스크롤 스테이지다.
        /// 나머지는 방 하나짜리 웨이브 스테이지다.
        ///
        /// 번호는 파일명과 같은 두 자리다 — 한 자리로 두면 Stage_1 과 Stage_10 이 목록에서 섞인다.
        /// </summary>
        public const string Stage01 = "Stage_01";
        public const string Stage02 = "Stage_02";
        public const string Stage03 = "Stage_03";
        public const string Stage04 = "Stage_04";
        public const string Stage05 = "Stage_05";

        /// <summary>본편 스테이지를 번호(1~5)로 얻는다. 범위 밖은 양끝으로 물린다.</summary>
        public static string Stage(int number)
        {
            switch (number)
            {
                case 1:  return Stage01;
                case 2:  return Stage02;
                case 3:  return Stage03;
                case 4:  return Stage04;
                default: return number < 1 ? Stage01 : Stage05;
            }
        }

    }
}
