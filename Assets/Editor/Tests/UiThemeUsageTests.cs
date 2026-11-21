using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// UI 스크립트가 색을 <see cref="UiTheme"/>에서만 꺼내는가 — 소스를 읽어 <c>new Color(</c>를 찾는다.
    ///
    /// 규칙이 문서에만 있으면 석 달 뒤 다시 파일마다 색이 생긴다(기획서 7항).
    /// 새 색이 필요하면 <see cref="UiTheme"/>에 이름을 붙여 추가한다. 알파만 다르면 <c>.WithAlpha()</c>.
    ///
    /// 월드 연출(스킬 VFX · 상태 표시 · 기즈모)은 아트 소관이라 대상이 아니다 — 아래 폴더 목록에 없다.
    /// </summary>
    public class UiThemeUsageTests
    {
        /// <summary>검사하는 곳. <c>Assets/Scripts</c> 기준.</summary>
        private static readonly string[] Scope =
        {
            "Battle/Components/UI",
            "Battle/Support/BulletTimeGaugeWidget.cs",
            "FlowScene",
            "Progression/Support/CardOffer.cs",
            "Core/Support/UiKit.cs",
            "Core/Support/UiParts.cs",
        };

        private static readonly Regex Literal = new Regex(@"new\s+Color\s*\(", RegexOptions.Compiled);

        [Test]
        public void UiScripts_HaveNoColorLiterals()
        {
            string scripts = Path.Combine(Application.dataPath, "Scripts");
            var offenders = new List<string>();
            int scanned = 0;

            foreach (string rel in Scope)
            {
                string path = Path.Combine(scripts, rel);
                IEnumerable<string> files = Directory.Exists(path)
                    ? Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
                    : File.Exists(path) ? new[] { path } : new string[0];

                foreach (string file in files)
                {
                    scanned++;
                    string[] lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string code = StripComment(lines[i]);
                        if (Literal.IsMatch(code))
                            offenders.Add($"{Path.GetFileName(file)}:{i + 1}  {lines[i].Trim()}");
                    }
                }
            }

            Assert.That(scanned, Is.GreaterThan(10), "검사 범위의 경로가 바뀌어 아무것도 안 읽었다.");
            Assert.That(offenders, Is.Empty,
                        "UI 스크립트에 색이 직접 적혀 있다 — UiTheme 토큰을 쓸 것:\n" + string.Join("\n", offenders));
        }

        private static string StripComment(string line)
        {
            int c = line.IndexOf("//", System.StringComparison.Ordinal);
            return c >= 0 ? line.Substring(0, c) : line;
        }
    }
}
