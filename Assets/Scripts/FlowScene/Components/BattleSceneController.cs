using UnityEngine;
using UnityEngine.SceneManagement;

namespace Prototype
{
    /// <summary>
    /// 배틀 씬의 지휘자. ESC 이탈, 씬 초기화, 그리고 <b>승패 판정</b>을 맡는다.
    /// 씬 자체가 통째로 언로드되므로 오브젝트 리셋 코드는 필요 없다.
    ///
    /// 판정은 여기서 하고 <b>전환은 <see cref="GameManager"/>가 한다.</b> 승패는 전투 상황을
    /// 봐야 알 수 있어서 씬 안에 있어야 하고, "다음이 어디인가"는 씬이 바뀌어도 남아야 해서
    /// Boot 씬에 있어야 한다.
    ///
    /// 그래서 <b>GameManager 없이 스테이지 씬만 단독으로 Play 하면 승패 기능이 아예 돌지 않는다.</b>
    /// 배치와 전투 감각만 확인하는 용도다.
    /// </summary>
    public class BattleSceneController : MonoBehaviour, IStageRestarter
    {
        [Tooltip("StageRoom 이 없는 씬에서 쓸 출구선 X. 방이 있으면 실제 방의 오른쪽 벽에서 푼 값이 이긴다 " +
                 "— 방 크기 보정이 벽을 옮기므로 여기 적은 고정값은 그 방에서 맞지 않는다.")]
        [SerializeField] private float exitX = 5f;

        /// <summary>이번 판의 출구선. <see cref="ResolveExitLine"/>이 방에서 푼다.</summary>
        private float exitLine;

        private bool isExiting;
        private bool isRestarting;

        /// <summary>
        /// 이긴 뒤 출구가 열렸는가. 이긴 순간 출구선 너머에 서 있었으면 한 번 안쪽으로 들어와야 열린다 —
        /// 바닥에 떨어진 보상(<see cref="ClearLoot"/>)을 보기도 전에 칸이 넘어가지 않게.
        /// </summary>
        private bool exitArmed;

        /// <summary>승패 기능이 도는가. GameManager 가 있어야 켜진다.</summary>
        private bool flowEnabled;

        /// <summary>양쪽이 등록을 마쳐 판정을 시작했는가. 한 번 켜지면 내려가지 않는다.</summary>
        private bool judging;

        private StageOutcome outcome = StageOutcome.Undecided;

        private StageResultUI resultUI;

        /// <summary>
        /// 웨이브 · 라운드를 굴리는 쪽. <b>없어도 된다</b> — 씬에 적을 직접 놓은 방
        /// (SampleScene · Stage_Mini · 훈련장)은 지금까지처럼 그대로 돈다.
        /// 있으면 승리 판정이 이쪽에 "아직 나올 적이 남았는지"를 물어본다.
        /// </summary>
        private StageProgressSource progress;

        /// <summary>
        /// 파티 명부. <b>패배 판정이 보는 곳</b>이고, 파티 체력바가 보는 것과 같은 목록이다.
        ///
        /// <see cref="BattleRegistry"/>로 전멸을 판정하면 안 된다 — 거기에는 필드에 선 몸만
        /// 들어 있어서(꺼진 동료는 <c>OnDisable</c>에서 빠진다) 시전자가 바뀌는 사이에
        /// 아무도 등록돼 있지 않은 프레임이 생기고, 그 한 프레임이 전멸로 읽혔다.
        ///
        /// <b>없어도 된다</b> — 파티 없이 도는 훈련장은 등록 목록으로 떨어진다.
        /// </summary>
        private PartyRoster party;

        /// <summary>지금 떠 있는 이 씬의 이름. 전환할 때 "무엇을 내릴지"가 된다.</summary>
        private string SceneName => gameObject.scene.name;

        private void Start()
        {
            // 일시정지 창은 씬 단독 실행에서도 필요하다(그 안의 [처음부터]도). GameManager 체크보다 먼저 띄운다.
            // 런 재시작([처음부터])은 창 안에 있다. 우측 상단 [ESC 일시정지] 안내는 런 바의 [ESC] 설정이 흡수했다.
            PauseMenu.Create(this, "마을로", ExitToLobby, onRestart: RestartStage);
            // 런 바 — 지도 · 칸과 같은 상단 띠. 일시정지 창보다 뒤에 지어야 [ESC] 설정이 붙는다.
            RunBarUI.Create(this, RunBarUI.Mode.Battle);

            progress = FindAnyObjectByType<StageProgressSource>();

            // 로스터는 그쪽 Start 에서 만들어지므로 여기서는 참조만 잡는다 —
            // 실행 순서가 정해져 있지 않아 지금 Roster 를 읽으면 비어 있을 수 있다.
            party = FindAnyObjectByType<PartyRoster>();

            // 판정이 안 도는 씬 단독 실행에서도 풀어 둔다 — 로그로 방과 문턱을 같이 확인할 수 있다.
            ResolveExitLine();

            // 씬 단독 실행 대응 — Boot 씬 없이 배틀 씬만 Play 했을 때
            if (GameManager.Instance == null)
            {
                Debug.LogWarning(
                    "[Battle] GameManager 없음. 씬 단독 실행 모드로 진행합니다 — 승리·패배 판정이 돌지 않습니다.");
                return;
            }

            flowEnabled = true;
            resultUI = StageResultUI.Create(this);

            BeginStage();
        }

        private void BeginStage()
        {
            TimeControl.Reset();

            GameManager gm = GameManager.Instance;

            // 지도를 거치지 않고 이 씬이 떴다. 승리하면 갈 칸이 없어 전체 클리어로 보인다 —
            // Boot 에서 시작하면 생길 수 없는 상태라, 보이면 흐름이 어딘가 새고 있다는 뜻이다.
            if (gm.CurrentNode == null)
                Debug.LogWarning($"[Battle] 들어가 있는 지도 칸이 없다({SceneName}). 이기면 전체 클리어로 처리된다.");

            Debug.Log($"[Battle] 전투 시작 {gm.FloorNumber}층 / {gm.FloorCount}층 ({SceneName}) — {gm.CurrentNodeModifier}");
        }

        private void Update()
        {
            if (isExiting || isRestarting) return;

            // ESC는 일시정지 창이 맡는다(PauseMenu) — 거기 [마을로]가 ExitToLobby 다.

            if (flowEnabled) TickOutcome();
        }

        // ── 승패 판정 ────────────────────────────────────

        private void TickOutcome()
        {
            // 전면 UI(일시정지 · 총공격 편집)가 떠 있는 동안은 판정을 멈춘다 —
            // 그 위로 클리어 화면이 겹쳐 뜨거나, 창을 보는 사이 칸이 넘어가면 안 된다.
            if (GameplayModal.IsOpen) return;

            if (outcome == StageOutcome.Undecided)
            {
                Judge();
                return;
            }

            // 이긴 뒤에는 출구만 본다. 진 뒤에는 버튼이 다음 행동을 정한다.
            if (outcome != StageOutcome.Victory || !GameManager.Instance.HasNextNode) return;

            bool atExit = ExitReached();
            if (!exitArmed)
            {
                exitArmed = !atExit;
                return;
            }

            if (atExit) ReturnToMap();
        }

        private void Judge()
        {
            // Entity 는 Start 에서 스스로 등록한다. 그 전에 판정하면 적 0명 = 승리라,
            // 씬이 뜨자마자 결과 화면이 나온다.
            if (!judging)
            {
                // 웨이브·라운드 방은 첫 적이 나오기 전까지 적이 0명이다. 등록 수만 보고 열면
                // 그 사이에 "적 0명 = 승리"가 나온다 — 한 기라도 나올 때까지 기다린다.
                if (progress != null && !progress.HasSpawnedAny) return;

                if (!StageOutcomeRules.CanJudge(BattleRegistry.Enemies.Count, BattleRegistry.Allies.Count))
                    return;

                judging = true;
            }

            StageOutcome next = StageOutcomeRules.Evaluate(
                BattleRegistry.AliveEnemyCount(),
                PartyWiped(),
                progress != null && progress.ThreatsRemaining);

            if (next == StageOutcome.Undecided) return;

            outcome = next;

            if (next == StageOutcome.Victory) HandleVictory();
            else HandleDefeat();
        }

        /// <summary>
        /// 파티가 전멸했는가. <b>명부에 묻고</b>, 명부가 없는 씬에서만 등록 목록으로 떨어진다.
        /// 어느 쪽을 믿을지는 <see cref="StageOutcomeRules.PartyWiped"/>가 정한다.
        /// </summary>
        private bool PartyWiped()
            => StageOutcomeRules.PartyWiped(
                party != null ? party.Roster.Count : 0,
                party != null ? party.AliveCount : 0,
                BattleRegistry.AliveAllyCount());

        /// <summary>
        /// 조작 중인 몸이 출구선을 넘었는가.
        ///
        /// 꺼진 동료는 <c>SetActive(false)</c> 상태라 좌표가 마지막에 있던 자리 그대로다.
        /// 활성 여부를 같이 보지 않으면 꺼진 동료의 옛 좌표로 스테이지가 넘어간다.
        /// </summary>
        private bool ExitReached()
        {
            foreach (Entity e in BattleRegistry.Allies)
            {
                if (e == null || !e.isActiveAndEnabled || e.Combat.IsDead) continue;
                if (StageOutcomeRules.ReachedExit(e.transform.position.x, exitLine)) return true;
            }

            return false;
        }

        /// <summary>
        /// 이번 판의 출구선을 정한다. <b>방이 있으면 방이 이긴다</b> —
        /// <see cref="StageRoom"/>이 지도 칸의 비율대로 오른쪽 벽을 옮기므로,
        /// 저작해 둔 고정값은 100% 방에서만 맞는다. 80% 방이면 문턱이 벽보다 밖에 남아
        /// <b>이겨도 다음 스테이지로 못 넘어간다.</b>
        ///
        /// 방이 없는 씬(SampleScene · 훈련장)은 저작값 그대로다.
        /// <see cref="StageRoom"/>은 실행 순서 -300이라 이 Start 보다 먼저 방을 적용해 둔다.
        /// </summary>
        private void ResolveExitLine()
        {
            exitLine = exitX;

            StageRoom room = StageRoom.Find();
            if (room == null) return;

            exitLine = StageOutcomeRules.ExitLine(room.Room.MaxX);

            if (room.Percent != RoomRules.FullPercent)
                Debug.Log($"[Battle] 출구선 x = {exitLine} (방 {room.Percent}%, 오른쪽 벽 x = {room.Room.MaxX})", this);
        }

        private void HandleVictory()
        {
            // 시간이 멈춘 채(컷인 중 등) 마지막 적이 죽으면 Scale 이 0 인 채로 남아 출구까지 걸어갈 수가 없다.
            TimeControl.Reset();

            GameManager gm = GameManager.Instance;

            // 들어가 있는 칸이 없으면(지도를 안 거친 씬 — BeginStage가 이미 경고했다) 일반 전투로 친다.
            MapNodeKind kind = gm.CurrentNode != null ? gm.CurrentNode.Kind : MapNodeKind.Battle;

            Debug.Log($"[Battle] {gm.FloorNumber}층 {kind} 클리어");

            if (gm.HasNextNode)
            {
                DropLoot(gm.FloorNumber, kind);

                // 안내만 띄우고 조작은 막지 않는다. 걸어가야 넘어가는 방식이다. 안내 칸은 폭 260이라 짧게 쓴다.
                // 정예는 배율 · 유물을 보여 준다 — 정예를 고른 값을 받았다는 것이 화면에서 확인돼야 다음에도 고른다.
                // ×는 기본 폰트에 글리프가 있다는 보장이 없어 x로 쓴다(CardOfferView의 "데미지 x1.5"와 같은 이유).
                bool elite = kind == MapNodeKind.Elite;
                string relic = ClearRewardRules.GrantsRelic(kind) ? " · 유물 1개" : "";
                string label = elite
                    ? $"금화 x{GoldRules.ClearRewardPercent(kind) / 100f:0.##}{relic} · 지도로"
                    : "보상을 줍고 지도로";

                resultUI?.ShowExitArrow(label, elite ? "정예 클리어" : "스테이지 클리어");
                return;
            }

            // 마지막 칸(보스)은 런이 끝난다 — 주워 갈 다음이 없으니 보상도 안 떨어진다.
            resultUI?.ShowAllClear(ExitToLobby);
        }

        /// <summary>
        /// 클리어 보상을 바닥에 놓는다 — 금화 하나(그 칸의 클리어 골드)와 상자 하나(<see cref="ClearLoot"/>).
        /// 적을 잡을 때는 아무것도 안 들어온다. 판정은 결과가 정해질 때 한 번만 부르므로 한 칸에 한 번만 떨어지고,
        /// 진 판에서 재시작해 이기면 그때 처음 떨어진다.
        ///
        /// 층이 깊을수록 금화가 크고, 정예 칸이면 배율이 붙고 상자가 빨갛다. 유물은 Mock이라 로그와 안내 문구뿐이다.
        /// </summary>
        private void DropLoot(int floor, MapNodeKind kind)
        {
            int gold = GoldRules.StageClearReward(floor, kind);

            StageRoom room = StageRoom.Find();
            RoomRect rect = room != null ? room.Room : RoomRect.Default;

            Entity body = BattleRegistry.Controlled;
            Vector3 at = body != null
                ? BeltScroll.ToGround(body.transform.position)
                : new Vector3(rect.CenterX, 0f, rect.CenterZ);

            ClearLoot.Drop(at, rect, exitLine, gold, ClearRewardRules.GrantsCard(kind), kind == MapNodeKind.Elite);

            if (ClearRewardRules.GrantsRelic(kind))
                Debug.Log("[Battle] 클리어 보상 유물 1개 (Mock — 효과 없음)");
        }

        private void HandleDefeat()
        {
            CastDirector.RecallScene();
            TimeControl.Reset();
            StopAllEnemies();

            Debug.Log($"[Battle] {GameManager.Instance.FloorNumber}층 패배");

            resultUI?.ShowDefeat(RetryStage, ExitToLobby);
        }

        /// <summary>진 뒤에도 적이 시체를 계속 두들기지 않게 한다.</summary>
        private static void StopAllEnemies()
        {
            foreach (Entity e in BattleRegistry.Enemies)
                if (e is Enemy enemy) enemy.StopAI();
        }

        // ── 전환 ────────────────────────────────────────

        /// <summary>
        /// 전환은 <b>먼저 접수시키고 그다음에 치운다.</b>
        ///
        /// 순서를 뒤집으면 안 된다 — SceneLoader 가 다른 전환 중이라 요청을 거절했을 때
        /// <see cref="BattleRegistry"/>만 비워진 채로 이 씬이 계속 돌아, 아무도 서로를 못 찾는
        /// 유령 전투가 된다. 접수된 뒤에 비우는 것은 안전하다: SwapTo 는 페이드부터 시작하므로
        /// 실제 언로드는 몇 프레임 뒤다.
        /// </summary>
        private void ReturnToMap()
        {
            if (isExiting || isRestarting) return;

            // 몸이 아직 씬에 있을 때 찍는다. 전환이 시작되면 물어볼 곳이 없다.
            CapturePartyState();

            if (!GameManager.Instance.CompleteNodeAndReturnToMap(SceneName, OnReturnedToMap)) return;

            isExiting = true;   // 전환이 끝날 때까지 이 씬의 판정을 멈춘다
            CleanupStage();
            ClearSceneStatics();
        }

        /// <summary>
        /// 파티의 잔여 체력 · 생사를 런 데이터에 찍는다. <b>스테이지를 클리어하고 넘어갈 때만</b> 부른다.
        ///
        /// 그 시점 선택이 곧 재시작 규칙이다 — 패배 후 같은 스테이지를 다시 하면
        /// <b>그 스테이지를 시작할 때의 기록</b>이 그대로 다시 읽힌다. 되돌리는 코드가 필요 없고,
        /// 전멸해서 진 판이 "동료가 전부 영구 사망한 채로 재시작"이 되지도 않는다.
        ///
        /// 동료의 사망은 런이 끝날 때까지 영구다. 다음 스테이지에서
        /// <see cref="Prototype.PartyAssembler"/>가 그 슬롯을 아예 비운다.
        /// </summary>
        private static void CapturePartyState()
        {
            // <b>비활성까지 본다.</b> 인자 없는 FindAnyObjectByType 은 비활성 GameObject 를 거르므로,
            // 주인공이 꺼져 있는 순간이면 null 이 온다. 주인공 하나를 못 찾은 대가로
            // <b>파티 다섯 명분 기록이 통째로 날아간다</b> — Capture 가 주인공을 통해
            // 동료를 훑기 때문이다. 증상은 "다음 스테이지에서 전원 만피"다.
            Player[] found = FindObjectsByType<Player>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (found.Length == 0)
            {
                Debug.LogError("[Battle] Player 를 못 찾아 파티 상태를 못 찍었다 — " +
                               "다음 스테이지에서 전원이 만피로 부활한다.");
                return;
            }

            RunProgression.Current.Party.Capture(found[0]);
        }

        /// <summary>패배 후 [이 스테이지 재시작]. 스테이지 번호는 유지된다.</summary>
        private void RetryStage()
        {
            if (isExiting || isRestarting) return;
            if (!GameManager.Instance.RestartCurrentStage(SceneName, OnBattleReloaded)) return;

            isRestarting = true;
            CleanupStage();
            ClearSceneStatics();
        }

        /// <summary>
        /// 런을 처음부터 다시 시작한다. 일시정지 창(<see cref="PauseMenu"/>)의 [처음부터] 확인이 부른다.
        ///
        /// 오브젝트를 하나씩 초기값으로 되돌리는 대신 씬을 통째로 내렸다 다시 올린다 —
        /// 리셋 누락이 원천적으로 생길 수 없는 방식이고, 이 프로젝트는 이미 ESC 이탈에서
        /// 같은 구조를 쓰고 있다.
        /// </summary>
        public void RestartStage()
        {
            if (isExiting || isRestarting) return;
            if (SceneLoader.Instance != null && SceneLoader.Instance.IsBusy) return;

            // ── 씬 단독 실행 모드: SceneLoader 가 없으니 현재 씬을 직접 다시 로드한다.
            if (SceneLoader.Instance == null || GameManager.Instance == null)
            {
                Debug.Log("[Battle] 씬 단독 실행 모드 — 현재 씬을 다시 로드한다.");

                isRestarting = true;
                CleanupStage();
                ClearSceneStatics();

                SceneManager.LoadScene(SceneName);
                return;
            }

            if (!GameManager.Instance.RestartRun(SceneName, OnReturnedToMap)) return;

            Debug.Log("[Battle] 런 초기화 — 새 지도를 올린다.");

            isRestarting = true;
            CleanupStage();
            ClearSceneStatics();
        }

        private void ExitToLobby()
        {
            if (isExiting || isRestarting) return;
            if (GameManager.Instance == null) return;
            if (!GameManager.Instance.ReturnToLobby(SceneName, OnReturnedToLobby)) return;

            isExiting = true;
            CleanupStage();
            ClearSceneStatics();
        }

        /// <summary>
        /// 씬 경계를 넘어 살아남는 static 은 반드시 <b>지금</b> 비운다.
        ///
        /// 새 씬의 Entity 는 Start 에서 스스로 등록하므로, 로드가 끝난 뒤에 비우면
        /// 갓 등록된 새 Entity 까지 날아가 타게팅이 통째로 죽는다. 지금 비워도 곧 파괴될
        /// 옛 Entity 의 OnDestroy → Unregister 는 빈 목록에 대한 no-op 이라 안전하다.
        /// </summary>
        public static void ClearSceneStatics()
        {
            CastDirector.RecallScene();
            TimeControl.Reset();
            BattleRegistry.Clear();

            // 공격권도 씬을 넘어 살아남는다. 두고 가면 새 스테이지 첫 몇 초 동안
            // 지난 판의 임대가 정원을 차지해 아무도 공격하지 않는다.
            EnemyAttackTokens.Pool.ResetAll();
        }

        /// <summary>
        /// 새 배틀 씬이 올라온 뒤 도는 콜백. 이 시점에 this 는 이미 파괴돼 있다.
        /// <see cref="BattleRegistry"/> 를 여기서 비우면 안 된다 — 새 씬의 Entity 가
        /// 이미 등록을 마친 뒤이기 때문이다.
        /// </summary>
        private static void OnBattleReloaded()
        {
            AudioManager.Instance?.PlayBattleBgm();
        }

        /// <summary>
        /// 지도 씬이 올라온 뒤 도는 콜백. 이 시점에 this 는 이미 파괴돼 있다.
        /// 지도는 메뉴 곡이다 — 전투 곡이 계속 돌면 "아직 싸우는 중"으로 들린다.
        /// </summary>
        private static void OnReturnedToMap()
        {
            AudioManager.Instance?.PlayMenuBgm();
        }

        /// <summary>
        /// 씬이 통째로 언로드되므로 오브젝트 파괴는 불필요.
        /// 씬 경계를 넘어 살아남는 것들만 정리한다.
        /// </summary>
        private void CleanupStage()
        {
            CastDirector.RecallScene();
            StopAllCoroutines();

            // TODO: DOTween 등을 도입했다면 여기서 전역 트윈 Kill
        }

        /// <summary>
        /// 배틀 씬이 완전히 내려간 뒤 도는 콜백. 이 시점에 this 는 이미 파괴돼 있으므로
        /// 인스턴스 필드를 만지면 안 된다 — static 만 정리한다.
        /// </summary>
        private static void OnReturnedToLobby()
        {
            // BulletTimeController 가 Scale = 0 인 채로 파괴됐을 수 있다.
            // Domain Reload 가 꺼져 있으면 static 이 그대로 살아남아 다음 런이 멈춘 채 시작한다.
            TimeControl.Reset();

            // BattleRegistry 는 여기서 비우지 않는다 — 로비에는 주인공과 허수아비가 있어서
            // 로드가 끝난 뒤에 비우면 갓 등록된 그 둘이 날아간다. 떠나기 전에 ClearSceneStatics 가 이미 비웠다.

            AudioManager.Instance?.PlayMenuBgm();
        }

        private void OnDestroy()
        {
            Debug.Log("[Battle] 씬 정리 완료");
        }
    }
}
