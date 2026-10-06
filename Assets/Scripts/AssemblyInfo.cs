using System.Runtime.CompilerServices;

// 테스트 전용 이음매(internal)를 에디터 테스트 어셈블리에만 연다.
// 게임 코드 쪽에서는 internal도 public과 같지만, 테스트 밖에서 부르면 안 되는 자리라는 표시다.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
