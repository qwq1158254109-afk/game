using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StudentGrowth
{
    [DefaultExecutionOrder(120)]
    [DisallowMultipleComponent]
    public sealed class StageHubSelectionController : MonoBehaviour
    {
        public StageHubMapSceneController hubController;
        public FullscreenMapSceneController runtimeSceneController;
        public MapInteractionController interactionController;
        public LocationSceneRouter locationSceneRouter;
        public GameSessionState sessionState;
        public StageHubLocationNodeView[] nodes = new StageHubLocationNodeView[0];
        public StageHubLocationInfoView locationInfoView;
        public Text energyText;
        public Text pressureText;
        public Text moodText;
        public Text moneyText;
        public Image energyFill;
        public Image pressureFill;
        public Image moodFill;
        public Text residentsText;
        public Text eventsText;
        [Tooltip("是否允许玩家从阶段地图再次进入当前所在地点。")]
        public bool allowRepeatEnterCurrentLocation = true;
        public float refreshInterval = 0.2f;

        public StageHubLocationNodeView SelectedNode => selectedNode;

        private readonly StageMapLocationAvailabilityService availabilityService = new StageMapLocationAvailabilityService();
        private readonly Dictionary<string, MapLocationPortal> portalsById = new Dictionary<string, MapLocationPortal>();
        private StageHubLocationNodeView selectedNode;
        private StageHubLocationNodeView hoveredNode;
        private float nextRefreshTime;
        private bool initialized;
        private bool ownsGate;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ReleaseGate();
        }

        /// <summary>
        /// 场景加载完成就解锁。Acquire 只发生在「确认进入地点」之后，
        /// 那一刻已经在切场景了，新场景一加载就该恢复移动。
        /// 这是兜底：万一本对象没有走 OnDestroy（例如被直接销毁），
        /// 闸门也不会跨场景残留把角色永久锁死。
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ReleaseGate();
        }

        private void AcquireGate()
        {
            if (ownsGate)
            {
                return;
            }

            ownsGate = true;
            GameplayInputGate.Acquire(this);
        }

        private void ReleaseGate()
        {
            if (!ownsGate)
            {
                return;
            }

            ownsGate = false;
            // consumeCurrentFrame：确认的那一帧不要被当成移动输入带进新场景。
            GameplayInputGate.Release(this, true);
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!initialized || refreshInterval <= 0f || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + refreshInterval;
            RefreshStatusAndAvailability();
        }

        public void Initialize()
        {
            ResolveRuntimeReferences();
            CollectPortals();
            hubController?.RefreshPortals();
            BindNodes();
            ConfigureNavigation();
            WireConfirmButton();

            initialized = true;
            SelectDefaultNode();
            RefreshStatusAndAvailability();
        }

        public void HandleNodeClick(StageHubLocationNodeView node)
        {
            if (node == null)
            {
                return;
            }

            if (selectedNode == node)
            {
                ConfirmSelected();
                return;
            }

            SelectNode(node);
        }

        public void SetHoveredNode(StageHubLocationNodeView node)
        {
            hoveredNode = node;
            RefreshNodeVisuals();
        }

        public void SelectNode(StageHubLocationNodeView node)
        {
            if (node == null)
            {
                return;
            }

            selectedNode = node;
            locationInfoView?.SetResultMessage(string.Empty);
            RefreshNodeVisuals();
            RefreshLocationInfo();

            if (EventSystem.current != null && node.hitArea != null)
            {
                EventSystem.current.SetSelectedGameObject(node.hitArea.gameObject);
            }
        }

        public void MoveSelection(Vector2Int direction)
        {
            if (nodes == null || nodes.Length == 0)
            {
                return;
            }

            int currentIndex = FindNodeIndex(selectedNode);
            if (currentIndex < 0)
            {
                SelectDefaultNode();
                return;
            }

            int row = currentIndex / 3;
            int column = currentIndex % 3;
            column = Mathf.Clamp(column + direction.x, 0, 2);
            row = Mathf.Clamp(row - direction.y, 0, 1);
            int nextIndex = Mathf.Clamp(row * 3 + column, 0, nodes.Length - 1);
            SelectNode(nodes[nextIndex]);
        }

        public void ConfirmSelected()
        {
            if (selectedNode == null)
            {
                return;
            }

            if (!CanConfirmSelectedNode())
            {
                locationInfoView?.SetResultMessage(IsSelectedCurrentLocation()
                    ? "你已经在这个地点。"
                    : GetSelectedReason());
                return;
            }

            ResolveRuntimeReferences();
            MapInteractionPoint point = selectedNode.Portal == null
                ? null
                : selectedNode.Portal.GetComponent<MapInteractionPoint>();
            if (interactionController == null || point == null)
            {
                locationInfoView?.SetResultMessage("地点入口尚未完成运行时绑定。");
                return;
            }

            // 先起守护（协程同步跑到第一个 yield，Acquire 当场生效），再执行交互。
            // 顺序反了的话，同步切场景会让协程来不及登记，过场照样能走。
            StartCoroutine(GuardUntilSceneChanges());

            LocationActionExecutionResult result = interactionController.TryInteract(point);
            if (result != null && !result.success)
            {
                locationInfoView?.SetResultMessage(result.message);
                // 交互失败 = 不会切场景，立刻解锁，别让玩家干等 1.5 秒。
                ReleaseGate();
            }
        }

        /// <summary>
        /// 锁住输入直到场景真的换了。
        /// 必须带超时：TryInteract 返回 success 不等于一定会 LoadScene
        /// （显示模式被拦、路由提前 return 等分支都会返回成功但不切场景），
        /// 无脑 Acquire 就会把角色永久锁死——比原来的「过场能走」严重得多。
        /// </summary>
        private System.Collections.IEnumerator GuardUntilSceneChanges()
        {
            AcquireGate();
            string sceneBefore = SceneManager.GetActiveScene().name;
            float deadline = Time.unscaledTime + 1.5f;
            while (Time.unscaledTime < deadline)
            {
                if (SceneManager.GetActiveScene().name != sceneBefore)
                {
                    // 已经换了场景，剩下的交给 OnSceneLoaded 释放。
                    yield break;
                }

                yield return null;
            }

            ReleaseGate();
        }

        public void Back()
        {
            ResolveRuntimeReferences();
            // 先起守护（协程会在 StartCoroutine 里同步跑到第一个 yield，Acquire 当场生效），
            // 再触发路由——顺序反了的话，同步切场景会让协程来不及登记。
            StartCoroutine(GuardUntilSceneChanges());

            if (runtimeSceneController != null)
            {
                runtimeSceneController.ReturnToStageMap();
            }
            else
            {
                locationSceneRouter?.ReturnToStageMap();
            }
        }

        public bool IsCurrentLocation(string locationId)
        {
            string currentLocationId = sessionState == null || sessionState.locationState == null
                ? string.Empty
                : sessionState.locationState.currentLocationId;
            return !string.IsNullOrEmpty(locationId)
                && !string.IsNullOrEmpty(currentLocationId)
                && locationId == currentLocationId;
        }

        private void ResolveRuntimeReferences()
        {
            if (hubController == null)
            {
                hubController = FindAnyObjectByType<StageHubMapSceneController>();
            }

            if (runtimeSceneController == null)
            {
                runtimeSceneController = FindAnyObjectByType<FullscreenMapSceneController>();
            }

            if (interactionController == null)
            {
                interactionController = FindAnyObjectByType<MapInteractionController>();
            }

            if (locationSceneRouter == null && runtimeSceneController != null)
            {
                locationSceneRouter = runtimeSceneController.locationSceneRouter;
            }

            if (locationSceneRouter == null)
            {
                locationSceneRouter = FindAnyObjectByType<LocationSceneRouter>();
            }

            if (sessionState == null && runtimeSceneController != null)
            {
                sessionState = runtimeSceneController.sessionState;
            }

            if (sessionState == null && hubController != null)
            {
                sessionState = hubController.sessionState;
            }
        }

        private void CollectPortals()
        {
            portalsById.Clear();
            MapLocationPortal[] portals = FindObjectsByType<MapLocationPortal>(FindObjectsInactive.Include);
            for (int i = 0; i < portals.Length; i++)
            {
                MapLocationPortal portal = portals[i];
                if (portal != null && !string.IsNullOrEmpty(portal.locationId))
                {
                    portalsById[portal.locationId] = portal;
                }
            }
        }

        private void BindNodes()
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                StageHubLocationNodeView node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                portalsById.TryGetValue(node.locationId, out MapLocationPortal portal);
                StageMapLocationEntry entry = FindEntry(node.locationId);
                StageMapLocationStatus status = portal == null
                    ? StageMapLocationStatus.Disabled
                    : portal.CurrentStatus;
                node.Bind(this, portal, entry, status);
            }
        }

        private void ConfigureNavigation()
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                Button button = nodes[i] == null ? null : nodes[i].hitArea;
                if (button == null)
                {
                    continue;
                }

                int row = i / 3;
                int column = i % 3;
                Navigation navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = GetButton(row, Mathf.Max(0, column - 1)),
                    selectOnRight = GetButton(row, Mathf.Min(2, column + 1)),
                    selectOnUp = GetButton(Mathf.Max(0, row - 1), column),
                    selectOnDown = GetButton(Mathf.Min(1, row + 1), column)
                };
                button.navigation = navigation;
            }
        }

        private void WireConfirmButton()
        {
            if (locationInfoView == null || locationInfoView.confirmButton == null)
            {
                return;
            }

            locationInfoView.confirmButton.onClick.RemoveListener(ConfirmSelected);
            locationInfoView.confirmButton.onClick.AddListener(ConfirmSelected);
        }

        private void SelectDefaultNode()
        {
            StageHubLocationNodeView fallback = null;
            for (int i = 0; i < nodes.Length; i++)
            {
                StageHubLocationNodeView node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                fallback ??= node;
            }

            // 光标默认落在角色当前所在地点；找不到（例如尚未进入过任何地点）再退回
            // 第一个可进入的地点。原实现只挑「第一个可确认节点」，光标总是停在家里。
            for (int i = 0; i < nodes.Length; i++)
            {
                StageHubLocationNodeView node = nodes[i];
                if (node != null && IsCurrentLocation(node.locationId))
                {
                    SelectNode(node);
                    return;
                }
            }

            for (int i = 0; i < nodes.Length; i++)
            {
                StageHubLocationNodeView node = nodes[i];
                if (node != null && node.CanConfirm)
                {
                    SelectNode(node);
                    return;
                }
            }

            SelectNode(fallback);
        }

        private void RefreshStatusAndAvailability()
        {
            ResolveRuntimeReferences();
            hubController?.RefreshPortals();

            for (int i = 0; i < nodes.Length; i++)
            {
                StageHubLocationNodeView node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                StageMapLocationEntry entry = FindEntry(node.locationId);
                StageMapLocationStatus status = node.Portal == null
                    ? StageMapLocationStatus.Disabled
                    : node.Portal.CurrentStatus;
                node.SetStatus(entry, status);
            }

            StageState stageState = sessionState == null ? null : sessionState.stageState;
            StageData stage = stageState == null ? null : stageState.currentStage;
            PlayerState player = sessionState == null ? null : sessionState.playerState;

            SetText(energyText, player == null ? "精力 -" : "精力 " + player.energy + "/" + player.maxEnergy);
            SetText(pressureText, player == null ? "压力 -" : "压力 " + player.pressure);
            SetText(moodText, player == null ? "心情 -" : "心情 " + player.mood);
            SetText(moneyText, player == null ? "金币 -" : "金币 " + player.money);
            SetFill(energyFill, player == null || player.maxEnergy <= 0 ? 0f : (float)player.energy / player.maxEnergy);
            SetFill(pressureFill, player == null ? 0f : player.pressure / 100f);
            SetFill(moodFill, player == null ? 0f : player.mood / 100f);
            SetText(residentsText, "居民：暂无可用信息");
            SetText(eventsText, "事件：暂无额外事件");

            RefreshNodeVisuals();
            RefreshLocationInfo();
        }

        private void RefreshLocationInfo()
        {
            if (locationInfoView == null || selectedNode == null)
            {
                return;
            }

            StageMapLocationEntry entry = FindEntry(selectedNode.locationId);
            string reason = availabilityService.ResolveReason(entry, selectedNode.CurrentStatus);
            locationInfoView.Refresh(
                selectedNode.Portal,
                entry,
                selectedNode.CurrentStatus,
                selectedNode.nodeSprite == null ? null : selectedNode.nodeSprite.sprite,
                reason,
                IsSelectedCurrentLocation(),
                allowRepeatEnterCurrentLocation);
        }

        private bool CanConfirmSelectedNode()
        {
            return selectedNode != null
                && selectedNode.CanConfirm
                && (allowRepeatEnterCurrentLocation || !IsSelectedCurrentLocation());
        }

        private bool IsSelectedCurrentLocation()
        {
            return selectedNode != null && IsCurrentLocation(selectedNode.locationId);
        }

        private void RefreshNodeVisuals()
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                StageHubLocationNodeView node = nodes[i];
                if (node != null)
                {
                    node.SetSelected(node == selectedNode);
                }
            }
        }

        private StageMapLocationEntry FindEntry(string locationId)
        {
            if (hubController == null || hubController.stageMapData == null || hubController.stageMapData.locations == null)
            {
                return null;
            }

            for (int i = 0; i < hubController.stageMapData.locations.Count; i++)
            {
                StageMapLocationEntry entry = hubController.stageMapData.locations[i];
                if (entry != null && entry.locationId == locationId)
                {
                    return entry;
                }
            }

            return null;
        }

        private string GetSelectedReason()
        {
            if (selectedNode == null)
            {
                return "没有可选择的地点。";
            }

            return availabilityService.ResolveReason(FindEntry(selectedNode.locationId), selectedNode.CurrentStatus);
        }

        private Button GetButton(int row, int column)
        {
            int index = row * 3 + column;
            return index >= 0 && index < nodes.Length && nodes[index] != null ? nodes[index].hitArea : null;
        }

        private int FindNodeIndex(StageHubLocationNodeView node)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == node)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string GetDisplayName(StageHubLocationNodeView node)
        {
            if (node.Entry != null && !string.IsNullOrEmpty(node.Entry.displayName))
            {
                return node.Entry.displayName;
            }

            return node.Portal != null ? node.Portal.displayName : node.fallbackDisplayName;
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void SetFill(Image target, float value)
        {
            if (target != null)
            {
                target.fillAmount = Mathf.Clamp01(value);
            }
        }
    }
}
