using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace StudentGrowth
{
    [DefaultExecutionOrder(-90)]
    public sealed class PlayerManager : MonoBehaviour
    {
        private const string RootName = "PlayerManager";

        private static PlayerManager instance;

        public RpgPlayerController currentPlayer;

        private bool inputLocked;
        private readonly List<GameObject> pendingPlayerCleanup = new List<GameObject>();

        public bool HasPendingPlayerCleanup
        {
            get
            {
                for (int i = pendingPlayerCleanup.Count - 1; i >= 0; i--)
                    if (pendingPlayerCleanup[i] == null) pendingPlayerCleanup.RemoveAt(i);
                return pendingPlayerCleanup.Count > 0;
            }
        }

        public static PlayerManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<PlayerManager>();
                }

                if (instance == null)
                {
                    GameObject root = new GameObject(RootName);
                    instance = root.AddComponent<PlayerManager>();
                }

                return instance;
            }
        }

        public RpgPlayerController CurrentPlayer => currentPlayer;
        public void EndSession()
        {
            GameFlowDiagnostics.Trace("PlayerManager.EndSession begin");
            inputLocked=true;
            foreach(var player in FindObjectsByType<RpgPlayerController>(FindObjectsInactive.Include))
            {
                if (pendingPlayerCleanup.Contains(player.gameObject)) continue;
                player.canMove=false;
                player.gameObject.SetActive(false);
                pendingPlayerCleanup.Add(player.gameObject);
                Destroy(player.gameObject);
            }
            currentPlayer=null;
            GameFlowDiagnostics.Trace("PlayerManager.EndSession complete");
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
            SceneManager.sceneLoaded -= OnSceneLoadedHeal;
            SceneManager.sceneLoaded += OnSceneLoadedHeal;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedHeal;
        }

        private void OnSceneLoadedHeal(Scene scene, LoadSceneMode mode)
        {
            ResolvePlayerAfterSceneLoad();
        }

        public RpgPlayerController CaptureCurrentPlayer()
        {
            if (currentPlayer == null)
            {
                currentPlayer = FindBestPlayer();
            }

            MakePersistent(currentPlayer);
            ApplyInputLock();
            return currentPlayer;
        }

        public RpgPlayerController ResolvePlayerAfterSceneLoad()
        {
            if (SceneManager.GetActiveScene().path == GameApplicationRuntime.MainMenuPath) return null;
            RpgPlayerController[] players = FindObjectsByType<RpgPlayerController>(FindObjectsInactive.Exclude);
            RpgPlayerController scenePlayer = FindScenePlayer(players);
            if (currentPlayer == null)
            {
                currentPlayer = scenePlayer != null ? scenePlayer : FindBestPlayer(players);
            }

            if (scenePlayer != null && currentPlayer != null && scenePlayer != currentPlayer)
            {
                CopySceneMovementSettings(scenePlayer, currentPlayer);
            }

            MakePersistent(currentPlayer);
            DestroyDuplicatePlayers(players);
            ApplyInputLock();
            return currentPlayer;
        }

        public void SetInputLocked(bool locked)
        {
            inputLocked = locked;
            ApplyInputLock();
        }

        private void ApplyInputLock()
        {
            if (currentPlayer != null)
            {
                currentPlayer.canMove = !inputLocked;
            }
        }

        private static RpgPlayerController FindBestPlayer()
        {
            return FindBestPlayer(FindObjectsByType<RpgPlayerController>(FindObjectsInactive.Exclude));
        }

        private static RpgPlayerController FindBestPlayer(RpgPlayerController[] players)
        {
            if (players == null || players.Length == 0)
            {
                return null;
            }

            RpgPlayerController scenePlayer = FindScenePlayer(players);
            return scenePlayer != null ? scenePlayer : players[0];
        }

        private static RpgPlayerController FindScenePlayer(RpgPlayerController[] players)
        {
            if (players == null)
            {
                return null;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            for (int i = 0; i < players.Length; i++)
            {
                RpgPlayerController player = players[i];
                if (player != null && player.gameObject.scene == activeScene)
                {
                    return player;
                }
            }

            return null;
        }

        private static void MakePersistent(RpgPlayerController player)
        {
            if (player == null)
            {
                return;
            }

            player.transform.SetParent(null, true);
            DontDestroyOnLoad(player.gameObject);
        }

        private void DestroyDuplicatePlayers(RpgPlayerController[] players)
        {
            if (players == null || currentPlayer == null)
            {
                return;
            }

            for (int i = 0; i < players.Length; i++)
            {
                RpgPlayerController player = players[i];
                if (player == null || player == currentPlayer)
                {
                    continue;
                }

                Destroy(player.gameObject);
            }
        }

        private static void CopySceneMovementSettings(RpgPlayerController source, RpgPlayerController target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.moveSpeed = source.moveSpeed;
            target.useMovementBounds = source.useMovementBounds;
            target.movementMin = source.movementMin;
            target.movementMax = source.movementMax;
            target.runSpeedMultiplier = source.runSpeedMultiplier;
            target.interactKey = source.interactKey;
            target.alternateInteractKey = source.alternateInteractKey;
            target.confirmInteractKey = source.confirmInteractKey;
            target.runKey = source.runKey;
            target.alternateRunKey = source.alternateRunKey;
        }
    }
}
