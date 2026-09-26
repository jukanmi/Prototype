using UnityEngine;

namespace Prototype
{
    /// <summary>
    /// 씬에 하나뿐인 <b>조종사</b>. 유저 입력을 읽어 지금 모는 몸의 의도를 채운다.
    ///
    /// <b>몸에 안 붙는다.</b> 예전에는 몸마다 <c>PlayerControl</c>을 복제해 두고 태그 교대가
    /// 그중 하나만 켜는 빙의 모델이었다. 그 구조에서 두 가지가 샜다:
    /// <list type="bullet">
    /// <item>불릿타임에 불려 나온 시전자가 컷인 도중 제 발로 걸어 다녔다 —
    /// 몸에 붙은 자율 BT가 <c>IsCommanded</c>가 잠기기 전 창에서 돌았기 때문이다.</item>
    /// <item>프리팹과 씬이 어긋났다 — 어떤 몸에는 컨트롤이 붙고 어떤 몸에는 안 붙어도
    /// 게임이 그냥 돌아가 버려서 눈치채기 어려웠다.</item>
    /// </list>
    /// 조종사가 하나면 둘 다 구조적으로 불가능해진다.
    ///
    /// 지휘키(E · U · F)는 <see cref="BattleCommander"/>가 읽는다 — 그쪽은 몸과 무관한 입력이고
    /// 이미 밖에 있었다. 이 컴포넌트는 <b>몸을 움직이는 입력</b>만 맡는다.
    ///
    /// <see cref="BattleCommander"/> · <see cref="TagSwapController"/>와 한 오브젝트에 두면 된다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerPilot : MonoBehaviour
    {
        /// <summary>지금 모는 몸. 아무도 안 몰면 null.</summary>
        public Entity Body { get; private set; }

        /// <summary>
        /// 이 몸을 몬다. 직전 몸은 자동으로 놓는다.
        /// <see cref="TagSwapController"/>가 교대할 때마다 부른다.
        /// </summary>
        public void Take(Entity body)
        {
            if (ReferenceEquals(Body, body)) return;

            Release();

            Body = body;
            if (body == null) return;

            body.IsPiloted = true;
        }

        /// <summary>
        /// 아무도 몸을 안 넘겨줬으면 씬에서 하나 고른다.
        ///
        /// <b>태그 컨트롤러가 없는 씬을 위한 폴백</b>이다 — 스킬 테스트 씬처럼 그냥 열어서
        /// 돌리는 곳도 움직여야 한다. 예전에는 몸에 붙은 컨트롤을 <c>Entity.Awake</c>가
        /// <c>FirstEnabledControl</c>로 집어 같은 일을 했다.
        ///
        /// <see cref="TagSwapController"/>가 있는 씬에서는 그쪽이 <c>Start</c>에서
        /// <see cref="Take"/>를 불러 이 선택을 곧바로 덮는다.
        /// </summary>
        private void Start()
        {
            if (Body != null) return;

            Entity fallback = FindFallbackBody();
            if (fallback == null) return;

            BattleLog.Log(LogCategory.State,
                $"태그 컨트롤러가 없어 {BattleLog.Name(fallback)}을(를) 직접 잡는다", this);
            Take(fallback);
        }

        /// <summary>플레이어 몸을 우선한다. 없으면 필드에 선 아군 아무나.</summary>
        private static Entity FindFallbackBody()
        {
            var bodies = BattleRegistry.Allies;

            Entity any = null;

            for (int i = 0; i < bodies.Count; i++)
            {
                Entity e = bodies[i];
                if (e == null || e.Combat == null || e.Combat.IsDead) continue;

                if (e is Player) return e;
                if (any == null) any = e;
            }

            return any;
        }

        /// <summary>
        /// 몸을 놓는다. <b>남은 의도를 반드시 비운다</b> —
        /// 마지막 이동 방향을 물고 내려간 몸은 다시 섰을 때 혼자 걸어간다.
        /// </summary>
        public void Release()
        {
            if (Body == null) return;

            Body.IsPiloted = false;
            Body.ClearIntent();

            Body = null;
        }

        /// <summary>
        /// 몸의 <c>Update</c>보다 먼저 돌아야 한다 — 그래야 이번 프레임에 채운 의도를
        /// 같은 프레임의 상태머신이 읽는다. 순서를 뒤집으면 조작이 한 프레임씩 밀린다.
        /// 그래서 <see cref="DefaultExecutionOrderAttribute"/>로 -100에 못박아 두었다.
        /// </summary>
        private void Update() => Tick(TimeControl.DeltaTime);

        /// <summary>
        /// 한 프레임 분의 조종. 에디트 모드 테스트가 스케줄러 없이 직접 펌프한다
        /// (<see cref="BeltScrollView.Sync"/>와 같은 취지).
        /// </summary>
        public void Tick(float dt)
        {
            Entity body = Body;
            if (body == null) return;

            // 벤치에 내려간 몸은 몰지 않는다. 불릿타임에 조작 캐릭터가 잠깐 숨는 구간이 그렇다.
            if (!body.isActiveAndEnabled) return;

            body.ClearCommand();

            // 선입력 창은 이 아래의 어떤 return보다 먼저 흘러야 한다 —
            // 지휘 · 정지 · 경직으로 빠져나가는 동안 창이 얼면 풀리는 순간 묵은 입력이 터진다.
            body.TickPilotTimers(dt);

            // 등장 연출 중에는 조작이 먹지 않는다. 화면 밖에서 날아오는 도중에 입력을 받으면
            // 몸이 두 목표 사이에서 떨고, 착지 자리가 스킬 사거리 기준점이라 그대로 어긋난다.
            // 선입력 창은 위에서 이미 흘렸다 — 여기서 얼리면 착지하는 순간 묵은 입력이 터진다.
            if (body.IsEntering) return;

            // 지휘 중에는 어떤 명령도 내지 않는다. 상태머신은 Executor 소유다 —
            // 콤보가 도는 동안 조작 대상이 그 몸이면 입력과 시전이 겹친다.
            if (body.IsCommanded) return;

            // 매번 Instance를 본다. 캐싱하면 씬을 다시 열거나 입력 호스트가 교체됐을 때
            // 파괴된 인스턴스를 붙들고 조용히 입력이 죽는다.
            PlayerInputController input = PlayerInputController.Instance;
            if (input == null) return;

            HandleInput(body, input);
        }

        private void HandleInput(Entity body, PlayerInputController input)
        {
            // 정지 중에는 이동 · 평타 입력을 받지 않는다.
            // 조준은 BulletTime 맵의 Aim이 따로 받는다.
            if (TimeControl.IsFrozen) return;

            if (body.IsBusy) return;
            if (CombatStateRules.IsStunned(body.Combat.CombatState)) return;

            // 벨트스크롤 — 좌우는 X축, 위아래는 Z축(깊이).
            Vector2 move = input.Move;
            var direction = new Vector3(move.x, 0f, move.y);

            Command command = Command.None;

            if (input.AttackPressed)
            {
                command = Command.Attack;

                // 같은 누름을 두 곳에 넣는다. Command는 Idle · Move가 읽어 공격에 들어가는 데 쓰고,
                // 버퍼는 AttackState가 읽어 다음 타로 잇는 데 쓴다.
                // 공격 중에는 Command를 아무도 안 읽으므로 버퍼가 유일한 통로다.
                body.BufferAttack(body.AttackBufferWindow);
            }
            else if (input.JumpPressed)
            {
                command = Command.Jump;
            }
            else if (input.DashPressed && body.DashReady)
            {
                body.StartDashCooldown();
                command = Command.Dash;
            }
            else if (direction.sqrMagnitude > 0.0001f)
            {
                command = Command.Move;
            }

            body.Drive(command, direction);
        }
    }
}
