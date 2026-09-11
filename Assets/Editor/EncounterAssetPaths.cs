using System.Collections.Generic;
using UnityEditor;

namespace Prototype
{
    /// <summary>
    /// 웨이브 애셋이 어디 있는가. <b>에디터 전용</b>이다 — 게임은 경로로 웨이브를 찾지 않는다.
    /// 씬 보드가 참조로 들고 있고, 디렉터는 그 목록만 읽는다.
    ///
    /// <b>굽는 스크립트는 없다.</b> 예전에는 <c>StageWaveCatalog</c>의 하드코딩 표를 애셋으로
    /// 내려찍는 빌더가 있었지만, 표가 사라지면서 <b>애셋 자체가 원본</b>이 됐다.
    /// 그 상태에서 굽기를 남겨 두면 한 번 잘못 눌러 손으로 저작한 웨이브를 통째로 잃는다.
    ///
    /// 남은 것은 자리 규칙뿐이다. 검증 테스트가 애셋을 찾는 유일한 근거라 한 군데에 둔다 —
    /// 두 벌이 되면 구운 자리와 찾는 자리가 갈리고, 증상은 "웨이브가 없다"가 된다.
    /// </summary>
    public static class EncounterAssetPaths
    {
        public const string Folder = "Assets/Data/Waves";

        /// <summary>웨이브로 도는 스테이지. 아레나(2 · 5)는 라운드로 돌아 여기 없다.</summary>
        public static readonly int[] WaveStages = { 1, 3, 4 };

        /// <summary>애셋 하나의 자리.</summary>
        public static string PathFor(int stage, int index) => $"{Folder}/Wave_{stage}_{index + 1}.asset";

        // ── 아레나 라운드 ───────────────────────────────
        //
        // 방과 아레나가 같은 애셋 타입을 쓰지만 폴더는 나눠 둔다. 이름 규칙이 다르고
        // (방은 스테이지 · 번호, 아레나는 스테이지 · 아레나), 검증도 각자 다른 것을 본다.

        public const string RoundFolder = "Assets/Data/Rounds";

        /// <summary>아레나로 도는 스테이지.</summary>
        public static readonly int[] ArenaStages =
        {
            StageWaveCatalog.ArenaStageNumber,
            StageWaveCatalog.BossStageNumber,
        };

        /// <summary>라운드 애셋 하나의 자리. 아레나 번호는 1부터다.</summary>
        public static string RoundPathFor(int stage, int arena)
            => $"{RoundFolder}/Round_{stage}_{arena}.asset";

        /// <summary>한 스테이지의 라운드를 아레나 번호순으로 읽는다. 빠진 번호에서 멈춘다.</summary>
        public static List<WaveAsset> LoadRounds(int stage)
        {
            var rounds = new List<WaveAsset>();

            for (int arena = 1; ; arena++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<WaveAsset>(RoundPathFor(stage, arena));
                if (asset == null) break;

                rounds.Add(asset);
            }

            return rounds;
        }

        /// <summary>
        /// 한 스테이지의 웨이브를 번호순으로 읽는다. 빠진 번호에서 멈춘다 —
        /// 중간이 비면 그 뒤는 씬 보드에도 안 꽂혀 있을 것이므로 조용히 이어 세지 않는다.
        /// </summary>
        public static List<WaveAsset> Load(int stage)
        {
            var waves = new List<WaveAsset>();

            for (int i = 0; ; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<WaveAsset>(PathFor(stage, i));
                if (asset == null) break;

                waves.Add(asset);
            }

            return waves;
        }
    }
}
