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
    }
}
