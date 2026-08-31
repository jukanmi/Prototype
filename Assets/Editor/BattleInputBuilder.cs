using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Prototype.EditorTools
{
    /// <summary>
    /// 전투 호스트 프리팹을 만든다. <b>입력 · 조종 · 태그 교대 · 덱 · 파티</b>가 전부 여기 들어간다.
    ///
    /// <b>왜 한 덩어리인가.</b> 예전에는 이 배선이 씬마다 흩어져 있었다 —
    /// 스테이지 씬 아홉 개가 각각 <c>Player</c> 1 + <c>Ally</c> 4 + <c>CombatManager</c>를
    /// 따로 들고 있었고, 동료 하나당 프리팹 오버라이드가 51개였다.
    /// 장착 카드 한 장을 바꾸려면 씬 아홉 개를 열어야 했고, 그중 하나만 어긋나도
    /// "그 스테이지만 덱이 다르다"로만 드러났다.
    ///
    /// 태그 교대가 몸을 <c>SetActive(false)</c>로 내리기 때문에 입력은 <b>절대 꺼지지 않는
    /// 오브젝트</b>에 있어야 한다. 그 오브젝트가 파티까지 소유하게 된 것이 이 프리팹이다.
    ///
    /// <b>루트는 원점에 고정</b>이다. 자식 몸들만 각자의 <see cref="Prototype.Physics"/>로 움직인다 —
    /// 루트가 움직이면 출구 판정(월드 x)과 히트박스 배율이 함께 어긋난다.
    /// 검증은 <c>BattleInputPrefabTests</c> · <c>PartyPrefabTests</c>가 한다.
    /// </summary>
    public static class BattleInputBuilder
    {
        /// <summary>
        /// 지금 이 프리팹이 있는 자리. <b>옛 경로를 상수로 박지 않는다</b> —
        /// 프리팹을 폴더로 옮긴 뒤 옛 자리에 다시 구우면 GUID가 달라져
        /// 씬 아홉 개가 계속 옛 것을 물고, 그 사실이 화면에 전혀 안 드러난다.
        /// </summary>
        public static string PrefabPath => PrefabLocator.BattleInputPath;

        public const string PartyRootName = "Party";
        public const string CameraAnchorName = "CameraAnchor";

        private const string ActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";
        private static string PlayerPrefabPath => PrefabLocator.PlayerPath;
        private static string AllyPrefabPath => PrefabLocator.AllyPath;
        private static string CombatPrefabPath => PrefabLocator.CombatManagerPath;
        private const string DefaultLoadoutPath =
            "Assets/Data/Resources/" + PartyCatalog.ResourceFolder + "/" + PartyCatalog.DefaultLoadoutName + ".asset";

        [MenuItem("Prototype/전투 - 입력 호스트 프리팹 만들기", priority = 30)]
        public static void Build()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actions == null)
            {
                Debug.LogError($"[BattleInputBuilder] {ActionsPath} 를 못 찾았다. 액션 자산부터 만들 것.");
                return;
            }

            GameObject playerPrefab = Require(PlayerPrefabPath);
            GameObject allyPrefab = Require(AllyPrefabPath);
            GameObject combatPrefab = Require(CombatPrefabPath);
            if (playerPrefab == null || allyPrefab == null || combatPrefab == null) return;

            var root = new GameObject("BattleInput");

            // ── 입력 ────────────────────────────────────
            var playerInput = root.AddComponent<PlayerInput>();
            playerInput.actions = actions;

            // 비어 있으면 아무 맵도 자동으로 안 켜진다. 컨트롤러가 OnEnable에서 직접 켜기는
            // 하지만 그 전에 도는 코드가 입력을 못 읽는다.
            playerInput.defaultActionMap = InputActionNames.Gameplay.Map;

            // 폴링으로 읽으므로 콜백은 아무도 안 받는다. SendMessage로 두면
            // 매 입력마다 리플렉션만 돌고 얻는 게 없다.
            playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

            root.AddComponent<PlayerInputController>();
            root.AddComponent<InputMapSwitcher>();
            root.AddComponent<PlayerPilot>();
            TagSwapController swap = root.AddComponent<TagSwapController>();
            root.AddComponent<BattleCommander>();
            PartyAssembler assembler = root.AddComponent<PartyAssembler>();

            // 파티 체력 HUD. 로스터의 주인(TagSwapController)과 <b>같은 오브젝트</b>에 둔다 —
            // 찾을 것도 배선할 것도 없고, 씬이 언로드되면 함께 사라진다.
            PartyHealthHUD partyHud = root.AddComponent<PartyHealthHUD>();

            // ── 파티 ────────────────────────────────────
            // <b>빈 컨테이너</b>다. 몸은 런타임에 PartyAssembler 가 만든다 —
            // 동료마다 제 프리팹을 쓰므로 여기서 구울 수 있는 "공통 슬롯"이 더는 없다.
            // 기본 프리팹 둘만 참조로 꽂아 두고, 표에 프리팹이 없을 때 그리로 떨어진다.
            var partyRoot = new GameObject(PartyRootName);
            partyRoot.transform.SetParent(root.transform, false);

            // ── 카메라 앵커 ─────────────────────────────
            // Party 컨테이너의 <b>형제</b>다. 원점에 박히는 건 루트와 Party 노드뿐이고,
            // 앵커는 파티 몸들처럼 자유롭게 움직인다.
            var anchorGo = new GameObject(CameraAnchorName);
            anchorGo.transform.SetParent(root.transform, false);
            CameraAnchor anchor = anchorGo.AddComponent<CameraAnchor>();

            // ── 덱 · 콤보 · HUD ─────────────────────────
            var combatGo = (GameObject)PrefabUtility.InstantiatePrefab(combatPrefab, root.transform);
            combatGo.name = "CombatManager";
            combatGo.transform.localPosition = Vector3.zero;

            // ── 배선 ────────────────────────────────────
            // 몸은 런타임 생성물이라 여기서 꽂을 인스턴스가 없다. 대신 <b>기본 프리팹</b>을
            // 꽂는다 — PartyMemberData.prefab · PlayerData.prefab 이 비었을 때 쓰는 폴백이고,
            // 이게 없으면 표가 프리팹을 안 지정한 순간 파티가 통째로 안 선다.
            var bullet = combatGo.GetComponentInChildren<BulletTimeController>(true);
            var executor = combatGo.GetComponentInChildren<ComboExecutor>(true);
            var selector = combatGo.GetComponentInChildren<TargetSelector>(true);

            Wire(assembler, so =>
            {
                so.FindProperty("defaultPlayerPrefab").objectReferenceValue = playerPrefab.GetComponent<Player>();
                so.FindProperty("defaultAllyPrefab").objectReferenceValue = allyPrefab.GetComponent<Ally>();
                so.FindProperty("partyRoot").objectReferenceValue = partyRoot.transform;
                so.FindProperty("swap").objectReferenceValue = swap;
                so.FindProperty("bulletTime").objectReferenceValue = bullet;
                so.FindProperty("standaloneLoadout").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<PartyLoadout>(DefaultLoadoutPath);
            });

            Wire(swap, so =>
            {
                so.FindProperty("bulletTime").objectReferenceValue = bullet;
                so.FindProperty("targetSelector").objectReferenceValue = selector;
                so.FindProperty("pilot").objectReferenceValue = root.GetComponent<PlayerPilot>();
                so.FindProperty("cameraAnchor").objectReferenceValue = anchor;
            });

            Wire(bullet, so =>
            {
                so.FindProperty("executor").objectReferenceValue = executor;
                so.FindProperty("targetSelector").objectReferenceValue = selector;
                so.FindProperty("swap").objectReferenceValue = swap;
            });

            Wire(root.GetComponent<BattleCommander>(), so =>
            {
                so.FindProperty("bulletTime").objectReferenceValue = bullet;
                so.FindProperty("swap").objectReferenceValue = swap;
            });

            Wire(partyHud, so => so.FindProperty("swap").objectReferenceValue = swap);

            Wire(combatGo.GetComponentInChildren<DebugComboHUD>(true), so =>
            {
                so.FindProperty("bulletTime").objectReferenceValue = bullet;
                so.FindProperty("targetSelector").objectReferenceValue = selector;
            });

            // 루트는 원점 · 무회전 · 배율 1. 이게 "물리 간섭 없는 컨테이너"의 전부다.
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();

            Debug.Log($"[BattleInputBuilder] {PrefabPath} 생성 완료 — " +
                      "빈 Party 컨테이너 + CombatManager. 몸은 런타임에 로드아웃대로 만들어진다. " +
                      "씬에서 Player · Ally · CombatManager 인스턴스를 제거하고 " +
                      "'Prototype ▸ 파티 - 씬 마이그레이션'을 돌릴 것.", prefab);
        }

        private static GameObject Require(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogError($"[BattleInputBuilder] {path} 를 못 찾았다.");
            return go;
        }

        /// <summary>
        /// <c>private [SerializeField]</c>를 꽂는다. 인스펙터에서 손으로 하면
        /// 빠뜨려도 게임이 그냥 돌아가 버려 눈치채기 어렵다 — 배선을 코드에 둔다.
        /// </summary>
        private static void Wire(Object target, System.Action<SerializedObject> apply)
        {
            if (target == null) return;

            var so = new SerializedObject(target);
            apply(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
