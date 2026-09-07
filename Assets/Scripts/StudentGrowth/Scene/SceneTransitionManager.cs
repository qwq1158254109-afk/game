using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentGrowth
{
    [DefaultExecutionOrder(-100)]
    public sealed class SceneTransitionManager : MonoBehaviour
    {
        private const string RootName = "SceneTransitionManager";
        private const string StageHubSceneName = "StageHubMapScene";

        private static SceneTransitionManager instance;

        public float fadeDuration = 0.35f;
        public bool IsTransitioning { get; private set; }
        public static bool HasActiveTransition => instance != null && instance.IsTransitioning;

        public static SceneTransitionManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<SceneTransitionManager>();
                }

                if (instance == null)
                {
                    GameObject root = new GameObject(RootName);
                    instance = root.AddComponent<SceneTransitionManager>();
                }

                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExistsOnPlay()
        {
            _ = Instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void TransitionToScene(string sceneName, string spawnPointId)
        {
            TransitionToScene(sceneName, spawnPointId, 0);
        }

        public void TransitionToScene(string sceneName, string spawnPointId, int timeCostMinutes)
        {
            if (AudioSettingsController.DisplayTransitionBlocked || PauseMenuRuntime.BlocksGameplayNavigation) return;
            if (IsTransitioning)
            {
                return;
            }

            StartCoroutine(TransitionRoutine(sceneName, spawnPointId, timeCostMinutes));
        }

        private IEnumerator TransitionRoutine(string sceneName, string spawnPointId, int timeCostMinutes)
        {
            string requestedSceneName = ResolveSceneName(sceneName);
            string resolvedSceneName = ResolveAllowedSceneName(requestedSceneName);
            if (resolvedSceneName != requestedSceneName)
            {
                Debug.LogWarning(
                    $"Blocked direct map transition from '{SceneManager.GetActiveScene().name}' to '{requestedSceneName}'. Returning to '{StageHubSceneName}' instead.");
                spawnPointId = string.Empty;
            }

            if (!Application.CanStreamedLevelBeLoaded(resolvedSceneName))
            {
                Debug.LogWarning($"SceneTransitionManager could not load scene '{sceneName}' (resolved '{resolvedSceneName}'). Add it to Build Settings or fix the SceneDoor target.");
                InteractionPromptUI.Show("目标场景未加入 Build Settings：" + resolvedSceneName, this);
                yield break;
            }

            IsTransitioning = true;
            CaptureSharedSessionState();

            PlayerManager playerManager = PlayerManager.Instance;
            RpgPlayerController player = playerManager.CaptureCurrentPlayer();
            playerManager.SetInputLocked(true);
            InteractionPromptUI.HideAll();

            FadeController fadeController = FadeController.Instance;
            yield return fadeController.FadeIn(fadeDuration);

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(resolvedSceneName, LoadSceneMode.Single);
            if (loadOperation == null)
            {
                Debug.LogWarning($"SceneTransitionManager failed to start loading scene '{resolvedSceneName}'.");
                yield return fadeController.FadeOut(fadeDuration);
                playerManager.SetInputLocked(false);
                IsTransitioning = false;
                yield break;
            }

            while (!loadOperation.isDone)
            {
                yield return null;
            }

            yield return null;

            if (resolvedSceneName != StageHubSceneName)
            {
                player = playerManager.ResolvePlayerAfterSceneLoad();
                MovePlayerToSpawn(player, spawnPointId);
                RebindMapSceneControllers(player);
            }

            yield return fadeController.FadeOut(fadeDuration);
            playerManager.SetInputLocked(false);
            IsTransitioning = false;
        }

        private static void CaptureSharedSessionState()
        {
            GameSessionState state = null;

            SceneTransitionController sceneTransitionController = FindObjectOfType<SceneTransitionController>();
            if (sceneTransitionController != null)
            {
                state = sceneTransitionController.sessionState;
            }

            if (state == null)
            {
                StageController stageController = FindObjectOfType<StageController>();
                if (stageController != null)
                {
                    state = stageController.sessionState;
                }
            }

            if (state != null)
            {
                GameBootstrapper.SetSharedSession(state);
            }
        }

        private static void MovePlayerToSpawn(RpgPlayerController player, string spawnPointId)
        {
            if (player == null)
            {
                Debug.LogWarning("SceneTransitionManager could not position player because no RpgPlayerController exists in the loaded scene.");
                return;
            }

            SpawnPoint spawnPoint = FindSpawnPoint(spawnPointId);
            if (spawnPoint != null)
            {
                player.TeleportTo(spawnPoint.transform.position);
                player.SetFacingDirection(spawnPoint.facingDirection);
                return;
            }

            PlayerSpawnPoint legacySpawnPoint = FindLegacySpawnPoint(spawnPointId);
            if (legacySpawnPoint != null)
            {
                player.TeleportTo(legacySpawnPoint.transform.position);
                SpawnPoint facingSource = legacySpawnPoint.GetComponent<SpawnPoint>();
                if (facingSource != null)
                {
                    player.SetFacingDirection(facingSource.facingDirection);
                }

                return;
            }

            Debug.LogWarning($"SceneTransitionManager could not find SpawnPoint '{spawnPointId}' in scene '{SceneManager.GetActiveScene().name}'. Player kept current position.");
        }

        private static void RebindMapSceneControllers(RpgPlayerController player)
        {
            FullscreenMapSceneController[] controllers = FindObjectsOfType<FullscreenMapSceneController>();
            for (int i = 0; i < controllers.Length; i++)
            {
                FullscreenMapSceneController controller = controllers[i];
                if (controller != null)
                {
                    controller.RebindPlayer(player);
                }
            }
        }

        private static SpawnPoint FindSpawnPoint(string spawnPointId)
        {
            SpawnPoint[] spawnPoints = FindObjectsOfType<SpawnPoint>();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                SpawnPoint spawnPoint = spawnPoints[i];
                if (spawnPoint != null && spawnPoint.Matches(spawnPointId))
                {
                    return spawnPoint;
                }
            }

            return null;
        }

        private static PlayerSpawnPoint FindLegacySpawnPoint(string spawnPointId)
        {
            PlayerSpawnPoint[] spawnPoints = FindObjectsOfType<PlayerSpawnPoint>();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                PlayerSpawnPoint spawnPoint = spawnPoints[i];
                if (spawnPoint != null && spawnPoint.Matches(spawnPointId))
                {
                    return spawnPoint;
                }
            }

            return null;
        }

        private static string ResolveSceneName(string sceneName)
        {
            if (sceneName == "Home")
            {
                return "HomeMapScene";
            }

            if (sceneName == "Shop")
            {
                return "ShopMapScene";
            }

            return sceneName;
        }

        private static string ResolveAllowedSceneName(string requestedSceneName)
        {
            if (!Application.isPlaying
                || requestedSceneName == StageHubSceneName
                || SceneManager.GetActiveScene().name == StageHubSceneName)
            {
                return requestedSceneName;
            }

            return StageHubSceneName;
        }
    }
}
