using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Prototype.Tests
{
    /// <summary>
    /// 입력을 읽는 곳이 정말 하나뿐인지 소스를 훑어서 확인한다.
    ///
    /// 이 규약은 타입으로 표현할 수가 없다 — 누구든 아무 스크립트에서
    /// <c>Keyboard.current</c>를 한 줄 쓰면 그만이고, 컴파일도 되고 동작도 한다.
    /// 그렇게 새어 나간 키는 리바인드 대상에서 빠져서, 사용자가 키를 바꿔도
    /// 그 하나만 옛 키로 남는다. 그게 이 테스트가 막는 사고다.
    /// </summary>
    public class InputSourceTests
    {
        /// <summary>입력 계층 자신. 여기서는 디바이스를 만져도 된다.</summary>
        private const string InputLayerFolder = "Scripts/Input";

        /// <summary>런타임 조작과 무관한 에디터 도구는 제외한다.</summary>
        private const string EditorFolder = "Editor";

        private static readonly string[] Forbidden =
        {
            "Keyboard.current",
            "Mouse.current",
            "Gamepad.current",
            "Input.GetKey",
            "Input.GetAxis",
            "Input.GetButton",
            "Input.GetMouse",
        };

        [Test]
        public void OnlyTheInputLayerTouchesDevicesDirectly()
        {
            string root = Path.Combine(Application.dataPath, "Scripts");
            Assert.That(Directory.Exists(root), Is.True, $"{root} 가 없다.");

            var offenders = new List<string>();

            foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');

                if (normalized.Contains(InputLayerFolder)) continue;
                if (normalized.Contains($"/{EditorFolder}/")) continue;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (IsComment(lines[i])) continue;

                    foreach (string pattern in Forbidden)
                    {
                        if (!lines[i].Contains(pattern)) continue;

                        string relative = normalized.Substring(normalized.IndexOf("Assets/", System.StringComparison.Ordinal));
                        offenders.Add($"{relative}:{i + 1} — {pattern}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty,
                "입력을 직접 읽는 곳이 남아 있다. PlayerInputController를 거치게 고쳐라:\n  "
                + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// 주석은 봐 준다. 결정을 적어 둔 문장에 키 이름이 나오는 건 막을 이유가 없다.
        /// 여는 중괄호 안쪽까지 따지지는 않는다 — 이 정도로 충분히 걸린다.
        /// </summary>
        private static bool IsComment(string line)
        {
            string trimmed = line.TrimStart();
            return trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*");
        }
    }
}
