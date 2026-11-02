using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StudentGrowth
{
    /// <summary>
    /// 新游戏开场：点击“开始游戏”后，卧室近景 → 主角躺床 → 妈妈进门叫起床 → 主角应答 → 下床。
    ///
    /// 不新建场景，也不改 HomeBedroomScene 的场景资产：
    /// FullscreenMapSceneController.Start 里 TryBegin 命中后，本组件在运行时搭对话、
    /// 被子覆盖层和妈妈临时 Sprite，播完把控制权交还给地图。
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class OpeningCutsceneController : MonoBehaviour
    {
        private const string BedroomSceneName = "HomeBedroomScene";
        private const string MotherTextureResourcePath = "StudentGrowth/Opening/mother_black_hair";
        private const int BlanketSortingOrder = 2620;
        private const int PlayerLyingSortingOrder = 2610;
        private const int MotherSortingOrder = 1500;
        private const float BedHeadOffset = 2.45f;
        private const float CloseOrthoSize = 4.7f;
        private const float RoomOrthoSize = 9.25f;
        private const float CameraPullSeconds = 1.8f;
        private const float MotherWalkSeconds = 3f;

        private static bool legacyInputBlocked;

        private readonly List<GameObject> hiddenUi = new List<GameObject>();

        private FullscreenMapSceneController map;
        private RpgPlayerController player;
        private MapCameraController cameraController;
        private Camera sceneCamera;
        private Canvas canvas;
        private DialogueView dialogueView;
        private DialogueData dialogueData;
        private SpriteRenderer bedRenderer;
        private SpriteRenderer blanketRenderer;
        private SpriteRenderer motherRenderer;
        private Sprite blanketSprite;
        private Sprite motherSprite;
        private Texture2D motherTexture;
        private MapObjectYSorter playerYSorter;
        private int playerOriginalSortingOrder;
        private bool playerSorterWasEnabled;

        private Vector2 bedCenter = new Vector2(-8.3f, 5.26f);
        private Vector2 bedBottomCenter = new Vector2(-8.3f, 3.2f);
        private Vector2 bedHeadPosition = new Vector2(-8.3f, 5.85f);
        private Vector2 bedFootPosition = new Vector2(-8.3f, 2.4f);
        private Vector2 motherEnd = new Vector2(-5.7f, 4.3f);
        private readonly Vector2 motherStart = new Vector2(-12.4f, 0.55f);
        private Vector2 standPosition = new Vector2(-8.3f, 1.6f);

        private Vector3 cameraTargetPosition;
        private float cameraTargetOrtho;
        private bool controlLocked;
        private bool cameraLocked;
        private bool gateHeld;
        private bool followWasEnabled = true;
        private float originalOrthoSize;
        private Vector3 originalCameraPosition;

        public static bool TryBegin(FullscreenMapSceneController sceneController)
        {
            if (sceneController == null || !OpeningCutsceneState.Pending)
            {
                return false;
            }

            if (SceneManager.GetActiveScene().name != BedroomSceneName)
            {
                OpeningCutsceneState.Clear();
                return false;
            }

            OpeningCutsceneState.Consume();
            GameObject host = new GameObject("[OpeningCutscene]");
            OpeningCutsceneController controller = host.AddComponent<OpeningCutsceneController>();
            controller.map = sceneController;
            controller.Begin();
            return true;
        }

        private void Begin()
        {
            player = map.playerController != null
                ? map.playerController
                : UnityEngine.Object.FindAnyObjectByType<RpgPlayerController>();
            cameraController = map.cameraController != null
                ? map.cameraController
                : UnityEngine.Object.FindAnyObjectByType<MapCameraController>();
            sceneCamera = cameraController != null ? cameraController.targetCamera : Camera.main;
            if (sceneCamera == null)
            {
                sceneCamera = Camera.main;
            }

            GameplayInputGate.Acquire(this);
            gateHeld = true;
            controlLocked = true;
            cameraLocked = true;

            CacheBed();
            HideMapUi();
            PreparePlayer();
            PrepareCamera();
            BuildCanvas();
            BuildBlanket();
            SpawnMother();

            StartCoroutine(PlaySequence());
        }

        private void LateUpdate()
        {
            if (controlLocked && player != null)
            {
                player.canMove = false;
                if (player.body != null)
                {
                    player.body.simulated = false;
                }
            }

            if (cameraLocked && sceneCamera != null)
            {
                if (cameraController != null)
                {
                    cameraController.followEnabled = false;
                }

                sceneCamera.orthographicSize = cameraTargetOrtho;
                sceneCamera.transform.position = cameraTargetPosition;
            }
        }

        private void CacheBed()
        {
            GameObject bed = GameObject.Find("BedObject");
            if (bed == null)
            {
                return;
            }

            SpriteRenderer renderer = bed.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null)
            {
                return;
            }

            bedRenderer = renderer;
            Bounds bounds = renderer.bounds;
            bedCenter = new Vector2(bounds.center.x, bounds.center.y);
            bedBottomCenter = new Vector2(bounds.center.x, bounds.min.y);
            bedHeadPosition = new Vector2(bounds.center.x, bounds.min.y + BedHeadOffset);
            bedFootPosition = new Vector2(bounds.center.x, bounds.min.y - 0.75f);
            standPosition = new Vector2(bounds.max.x + 1.6f, bounds.min.y + 0.1f);
            motherEnd = new Vector2(bounds.max.x + 1.1f, bounds.min.y + 1.15f);
        }

        private void HideMapUi()
        {
            Hide(map.hudView != null ? map.hudView.gameObject : null);
            Hide(map.timeActionHudView != null ? map.timeActionHudView.gameObject : null);
            Hide(map.promptView != null ? map.promptView.gameObject : null);
            Hide(map.locationToastView != null ? map.locationToastView.gameObject : null);
            Hide(map.dialogueView != null ? map.dialogueView.gameObject : null);
            Hide(map.resultView != null ? map.resultView.gameObject : null);
            Hide(map.functionMenuView != null ? map.functionMenuView.gameObject : null);
            Hide(map.confirmView != null ? map.confirmView.gameObject : null);
            GameObject pauseCanvas = GameObject.Find("PauseCanvas");
            if (pauseCanvas != null && pauseCanvas.activeSelf)
            {
                Hide(pauseCanvas);
            }
        }

        private void Hide(GameObject target)
        {
            if (target == null || !target.activeSelf)
            {
                return;
            }

            hiddenUi.Add(target);
            target.SetActive(false);
        }

        private void RestoreMapUi()
        {
            for (int i = 0; i < hiddenUi.Count; i++)
            {
                if (hiddenUi[i] != null)
                {
                    hiddenUi[i].SetActive(true);
                }
            }

            hiddenUi.Clear();
        }

        private void PreparePlayer()
        {
            if (player == null)
            {
                return;
            }

            player.canMove = false;
            if (player.body == null)
            {
                player.body = player.GetComponent<Rigidbody2D>();
            }

            if (player.body != null)
            {
                player.body.simulated = false;
            }

            player.TeleportTo(bedHeadPosition);
            player.SetFacingDirection(Vector2.down);

            playerYSorter = player.GetComponent<MapObjectYSorter>();
            if (playerYSorter != null)
            {
                playerSorterWasEnabled = playerYSorter.enabled;
                playerYSorter.enabled = false;
            }

            if (player.spriteRenderer != null)
            {
                playerOriginalSortingOrder = player.spriteRenderer.sortingOrder;
                player.spriteRenderer.sortingOrder = PlayerLyingSortingOrder;
            }
        }

        private void PrepareCamera()
        {
            if (cameraController != null)
            {
                followWasEnabled = cameraController.followEnabled;
                cameraController.followEnabled = false;
                if (cameraController.targetCamera != null)
                {
                    sceneCamera = cameraController.targetCamera;
                }
            }

            if (sceneCamera == null)
            {
                return;
            }

            originalOrthoSize = sceneCamera.orthographicSize;
            originalCameraPosition = sceneCamera.transform.position;
            cameraTargetOrtho = CloseOrthoSize;
            cameraTargetPosition = new Vector3(
                bedCenter.x,
                bedCenter.y + 0.3f,
                originalCameraPosition.z);
            sceneCamera.orthographicSize = cameraTargetOrtho;
            sceneCamera.transform.position = cameraTargetPosition;
        }

        private void ApplyCloseCamera()
        {
            if (sceneCamera == null)
            {
                return;
            }

            cameraTargetOrtho = CloseOrthoSize;
            cameraTargetPosition = new Vector3(
                bedCenter.x,
                bedCenter.y + 0.3f,
                originalCameraPosition.z);
            sceneCamera.orthographicSize = cameraTargetOrtho;
            sceneCamera.transform.position = cameraTargetPosition;
        }

        private void BuildCanvas()
        {
            GameObject canvasObject = new GameObject(
                "OpeningCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject viewObject = new GameObject("OpeningDialogue", typeof(RectTransform));
            viewObject.transform.SetParent(canvasObject.transform, false);
            dialogueView = viewObject.AddComponent<DialogueView>();
            // Awake 会把 panelRoot 兜底成自身，必须先清空，BuildDefaultLayout 才建得出来。
            dialogueView.panelRoot = null;
            dialogueView.theme = map.hudTheme;
            dialogueView.BuildDefaultLayout(canvasObject.transform as RectTransform);

            if (dialogueView.speakerText != null)
            {
                dialogueView.speakerText.rectTransform.anchoredPosition = new Vector2(56f, -24f);
                dialogueView.speakerText.rectTransform.sizeDelta = new Vector2(1000f, 40f);
            }

            if (dialogueView.bodyText != null)
            {
                dialogueView.bodyText.rectTransform.anchoredPosition = new Vector2(56f, -76f);
                dialogueView.bodyText.rectTransform.sizeDelta = new Vector2(1088f, 140f);
            }
        }

        private void BuildBlanket()
        {
            if (bedRenderer == null || bedRenderer.sprite == null)
            {
                return;
            }

            Sprite sprite = bedRenderer.sprite;
            Rect rect = sprite.rect;
            int coverPixels = Mathf.RoundToInt(BedHeadOffset * sprite.pixelsPerUnit);
            coverPixels = Mathf.Clamp(coverPixels, 1, Mathf.FloorToInt(rect.height));

            blanketSprite = Sprite.Create(
                sprite.texture,
                new Rect(rect.x, rect.y, rect.width, coverPixels),
                new Vector2(0.5f, 0f),
                sprite.pixelsPerUnit);
            blanketSprite.name = "OpeningBlanketSprite";

            GameObject blanket = new GameObject("OpeningBlanket");
            blanket.transform.SetParent(transform, false);
            blanket.transform.position = new Vector3(bedBottomCenter.x, bedBottomCenter.y, 0f);
            blanketRenderer = blanket.AddComponent<SpriteRenderer>();
            blanketRenderer.sprite = blanketSprite;
            blanketRenderer.sortingLayerID = bedRenderer.sortingLayerID;
            blanketRenderer.sortingOrder = BlanketSortingOrder;
        }

        private void SpawnMother()
        {
            motherTexture = Resources.Load<Texture2D>(MotherTextureResourcePath);
            if (motherTexture == null)
            {
                Debug.LogWarning("[OpeningCutscene] 缺少妈妈贴图 Resources/" + MotherTextureResourcePath);
                return;
            }

            motherTexture.filterMode = FilterMode.Point;
            motherSprite = Sprite.Create(
                motherTexture,
                new Rect(0f, 0f, motherTexture.width, motherTexture.height),
                new Vector2(0.5f, 0.5f),
                16f);
            motherSprite.name = "OpeningMotherSprite";

            GameObject mother = new GameObject("OpeningMother");
            mother.transform.SetParent(transform, false);
            mother.transform.position = new Vector3(motherStart.x, motherStart.y, 0f);
            motherRenderer = mother.AddComponent<SpriteRenderer>();
            motherRenderer.sprite = motherSprite;
            motherRenderer.sortingLayerID = bedRenderer != null ? bedRenderer.sortingLayerID : 0;
            motherRenderer.sortingOrder = MotherSortingOrder;
        }

        private IEnumerator WaitForGameplay()
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline)
            {
                GameApplicationRuntime app = GameApplicationRuntime.Instance;
                if (app == null || app.Coordinator == null || app.Coordinator.State == PauseMenuState.Gameplay)
                {
                    yield break;
                }

                yield return null;
            }
        }

        private IEnumerator PlaySequence()
        {
            // 新游戏加载期间 PauseMenuView 还盖着“正在处理，请稍候…”，
            // 等流程真正进入 Gameplay、遮罩收起后再开始可见的开场。
            // 这里只做极短的让路（上限 2s），避免点完开始游戏后干等一长段。
            yield return WaitForGameplay();

            // 等 MapCameraController / RebindPlayer 在本帧跑完，再把镜头和玩家锁回来。
            yield return null;
            ApplyCloseCamera();
            yield return new WaitForSeconds(0.25f);

            StartCoroutine(PullCameraToRoom());
            yield return WalkMotherPath(BuildMotherPath(), MotherWalkSeconds);
            yield return new WaitForSeconds(0.25f);

            yield return PlayDialogue();

            if (dialogueView != null)
            {
                dialogueView.Hide();
            }

            yield return GetOutOfBed();

            List<Vector2> backPath = BuildMotherPath();
            backPath.Reverse();
            yield return WalkMotherPath(backPath, MotherWalkSeconds * 0.9f);

            if (motherRenderer != null)
            {
                yield return FadeRenderer(motherRenderer, 1f, 0f, 0.5f);
            }

            RestoreScene();
            ReleaseControl();
            Cleanup();
        }

        private IEnumerator PullCameraToRoom()
        {
            if (sceneCamera == null)
            {
                yield break;
            }

            Vector3 startPosition = cameraTargetPosition;
            Vector3 endPosition = new Vector3(0f, 0f, startPosition.z);
            float startSize = cameraTargetOrtho;
            float elapsed = 0f;

            while (elapsed < CameraPullSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / CameraPullSeconds));
                cameraTargetPosition = Vector3.Lerp(startPosition, endPosition, t);
                cameraTargetOrtho = Mathf.Lerp(startSize, RoomOrthoSize, t);
                yield return null;
            }

            cameraTargetPosition = endPosition;
            cameraTargetOrtho = RoomOrthoSize;
        }

        private IEnumerator PullCameraToPlayer(float duration)
        {
            if (sceneCamera == null)
            {
                yield break;
            }

            Vector3 startPosition = cameraTargetPosition;
            Vector3 endPosition = new Vector3(
                standPosition.x,
                standPosition.y + 0.6f,
                startPosition.z);
            float startSize = cameraTargetOrtho;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                cameraTargetPosition = Vector3.Lerp(startPosition, endPosition, t);
                cameraTargetOrtho = Mathf.Lerp(startSize, originalOrthoSize, t);
                yield return null;
            }

            cameraTargetPosition = endPosition;
            cameraTargetOrtho = originalOrthoSize;
        }

        private List<Vector2> BuildMotherPath()
        {
            // 从西门绕到床右侧：先到床尾，再横移到床边，最后沿床沿往上，
            // 避免直接穿过床的碰撞盒。
            return new List<Vector2>
            {
                motherStart,
                bedFootPosition,
                new Vector2(motherEnd.x, bedFootPosition.y),
                motherEnd
            };
        }

        private IEnumerator WalkMotherPath(List<Vector2> points, float duration)
        {
            if (motherRenderer == null || points == null || points.Count < 2)
            {
                yield break;
            }

            float totalDistance = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                totalDistance += Vector2.Distance(points[i], points[i + 1]);
            }

            if (totalDistance <= 0.001f)
            {
                yield break;
            }

            motherRenderer.transform.position = new Vector3(points[0].x, points[0].y, 0f);
            float elapsed = 0f;
            int segment = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float travelled = totalDistance * Mathf.Clamp01(elapsed / duration);

                while (segment < points.Count - 2)
                {
                    float segmentLength = Vector2.Distance(points[segment], points[segment + 1]);
                    if (travelled <= segmentLength)
                    {
                        break;
                    }

                    travelled -= segmentLength;
                    segment++;
                }

                float currentLength = Vector2.Distance(points[segment], points[segment + 1]);
                float t = currentLength <= 0.001f ? 0f : Mathf.Clamp01(travelled / currentLength);
                Vector2 current = Vector2.Lerp(points[segment], points[segment + 1], t);
                Vector3 position = new Vector3(current.x, current.y, 0f);
                position.y += Mathf.Sin(elapsed * Mathf.PI * 4f) * 0.045f;
                motherRenderer.transform.position = position;
                yield return null;
            }

            Vector2 last = points[points.Count - 1];
            motherRenderer.transform.position = new Vector3(last.x, last.y, 0f);
        }

        private IEnumerator PlayDialogue()
        {
            if (dialogueView == null)
            {
                yield break;
            }

            dialogueData = BuildDialogue();
            bool completed = false;
            dialogueView.Show(dialogueData, delegate { completed = true; });

            while (!completed)
            {
                if (SkipPressed())
                {
                    dialogueView.Hide();
                    yield break;
                }

                if (AdvancePressed())
                {
                    dialogueView.Advance();
                }

                yield return null;
            }
        }

        private DialogueData BuildDialogue()
        {
            DialogueData data = ScriptableObject.CreateInstance<DialogueData>();
            data.dialogueId = "dialogue_opening_wakeup";
            data.speakerName = "妈妈";
            data.lines = new List<DialogueLine>
            {
                new DialogueLine { text = "小懒虫，起床啦——太阳都晒到枕头上了。" },
                new DialogueLine { text = "今天可是个大日子，期末考试就在今天！" },
                new DialogueLine { text = "快起来吃早餐，别饿着肚子去考场。" },
                new DialogueLine { text = "好的，我这就起来。", speakerOverride = "我" }
            };
            return data;
        }

        private IEnumerator GetOutOfBed()
        {
            if (player == null)
            {
                yield break;
            }

            player.SetFacingDirection(Vector2.down);
            yield return MovePlayer(bedHeadPosition, bedFootPosition, 1.15f);

            player.SetFacingDirection(Vector2.down);
            yield return MovePlayer(bedFootPosition, standPosition, 0.35f);

            if (blanketRenderer != null)
            {
                yield return FadeRenderer(blanketRenderer, 1f, 0f, 0.25f);
            }
        }

        private IEnumerator MovePlayer(Vector2 from, Vector2 to, float duration)
        {
            float elapsed = 0f;
            player.TeleportTo(from);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                player.TeleportTo(Vector2.Lerp(from, to, t));
                yield return null;
            }

            player.TeleportTo(to);
        }

        private void RestoreScene()
        {
            controlLocked = false;
            cameraLocked = false;

            if (player != null)
            {
                if (playerYSorter != null)
                {
                    playerYSorter.enabled = playerSorterWasEnabled;
                }

                if (player.spriteRenderer != null)
                {
                    player.spriteRenderer.sortingOrder = playerOriginalSortingOrder;
                }

                player.TeleportTo(standPosition);
                if (player.body != null)
                {
                    player.body.simulated = true;
                }

                player.canMove = true;
            }

            RestoreMapUi();

            if (cameraController != null)
            {
                if (sceneCamera != null)
                {
                    sceneCamera.orthographicSize = originalOrthoSize;
                }

                cameraController.followEnabled = followWasEnabled;
                cameraController.SnapToTarget();
            }
            else if (sceneCamera != null)
            {
                sceneCamera.orthographicSize = originalOrthoSize;
                sceneCamera.transform.position = originalCameraPosition;
            }
        }

        private void ReleaseControl()
        {
            if (!gateHeld)
            {
                return;
            }

            gateHeld = false;
            GameplayInputGate.Release(this, true);
        }

        private void Cleanup()
        {
            if (dialogueData != null)
            {
                Destroy(dialogueData);
                dialogueData = null;
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            ReleaseControl();

            if (blanketSprite != null)
            {
                Destroy(blanketSprite);
                blanketSprite = null;
            }

            if (motherSprite != null)
            {
                Destroy(motherSprite);
                motherSprite = null;
            }
        }

        private static IEnumerator FadeRenderer(SpriteRenderer renderer, float from, float to, float duration)
        {
            if (renderer == null)
            {
                yield break;
            }

            Color color = renderer.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                color.a = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                renderer.color = color;
                yield return null;
            }

            color.a = to;
            renderer.color = color;
        }

        private static bool AdvancePressed()
        {
            return KeyPressed(KeyCode.Space, "spaceKey")
                || KeyPressed(KeyCode.Return, "enterKey")
                || KeyPressed(KeyCode.KeypadEnter, "numpadEnterKey")
                || KeyPressed(KeyCode.E, "eKey");
        }

        private static bool SkipPressed()
        {
            return KeyPressed(KeyCode.Escape, "escapeKey");
        }

        private static bool KeyPressed(KeyCode legacyKey, string inputSystemKey)
        {
            if (!legacyInputBlocked)
            {
                try
                {
                    if (Input.GetKeyDown(legacyKey))
                    {
                        return true;
                    }
                }
                catch (System.InvalidOperationException)
                {
                    legacyInputBlocked = true;
                }
            }

            return ReadInputSystemKey(inputSystemKey);
        }

        private static bool ReadInputSystemKey(string propertyName)
        {
            System.Type keyboardType = System.Type.GetType(
                "UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            if (keyboardType == null)
            {
                return false;
            }

            object keyboard = keyboardType
                .GetProperty("current", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            if (keyboard == null)
            {
                return false;
            }

            object key = keyboardType
                .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(keyboard);
            if (key == null)
            {
                return false;
            }

            object pressed = key.GetType()
                .GetProperty("wasPressedThisFrame", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(key);
            return pressed is bool value && value;
        }
    }
}
