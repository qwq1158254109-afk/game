using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace StudentGrowth
{
    /// <summary>
    /// 背包与道具栏的入口：只在地图场景创建一次，负责开合背包、处理 1~5 快捷使用。
    /// 键位：6 或 B 开/关背包；1~5 使用道具栏对应格。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryController : MonoBehaviour
    {
        private static InventoryController instance;

        private GameSessionState session;

        /// <summary>
        /// 工程只启用新 Input System（ProjectSettings 的 activeInputHandler=1）时，
        /// 读旧 <see cref="Input"/> 会抛 InvalidOperationException。抛过一次就把这个标志置上，
        /// 之后只走下面的 Input System 通道 —— 否则 Update 每帧都会抛，实测刷出 33 万条报错。
        /// </summary>
        private bool legacyInputBlocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Object.FindAnyObjectByType<FullscreenMapSceneController>() == null)
            {
                return;
            }

            if (instance == null)
            {
                GameObject host = new GameObject("[Game] InventoryController");
                instance = host.AddComponent<InventoryController>();
            }

            InventoryPanelView.Ensure();
        }

        private void Start()
        {
            EnsureEventSystem();
            session = GameBootstrapper.SharedSession;
            if (session == null)
            {
                FullscreenMapSceneController map = Object.FindAnyObjectByType<FullscreenMapSceneController>();
                session = map != null ? map.sessionState : null;
            }
        }

        private void Update()
        {
            if (session == null)
            {
                return;
            }

            if (IsKeyPressed(KeyCode.Alpha6, "digit6Key") || IsKeyPressed(KeyCode.B, "bKey"))
            {
                InventoryPanelView panel = InventoryPanelView.Ensure();
                panel.Toggle(session);
                if (panel.IsOpen)
                {
                    Sfx.Ui.Open();
                }
                else
                {
                    Sfx.Ui.Close();
                }

                return;
            }

            InventoryPanelView current = InventoryPanelView.Instance;
            if (current != null && current.IsOpen)
            {
                if (IsKeyPressed(KeyCode.Escape, "escapeKey"))
                {
                    current.Close();
                    Sfx.Ui.Close();
                }

                return;
            }
        }

        /// <summary>旧输入优先、被禁则回退新 Input System（与本工程其它输入入口同一套写法）。</summary>
        private bool IsKeyPressed(KeyCode legacyKey, string inputSystemKey)
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

            return ReadInputSystemKey(inputSystemKey, "wasPressedThisFrame");
        }

        /// <summary>
        /// 反射读新 Input System，避免直接引用 Unity.InputSystem 程序集
        /// （与 RpgPlayerController / SceneDoor 等保持一致）。
        /// </summary>
        private static bool ReadInputSystemKey(string keyPropertyName, string statePropertyName)
        {
            System.Type keyboardType = System.Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            if (keyboardType == null)
            {
                return false;
            }

            object keyboard = keyboardType.GetProperty("current")?.GetValue(null, null);
            if (keyboard == null)
            {
                return false;
            }

            object key = keyboardType.GetProperty(keyPropertyName)?.GetValue(keyboard, null);
            if (key == null)
            {
                return false;
            }

            object value = key.GetType().GetProperty(statePropertyName)?.GetValue(key, null);
            return value is bool pressed && pressed;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject host = new GameObject("[UI] EventSystem");
            host.AddComponent<EventSystem>();
            // 工程 activeInputHandler=1（只用新 Input System）：StandaloneInputModule 读旧 Input，
            // 每帧抛 InvalidOperationException。优先反射挂新输入模块，写法与 SettingsMenuController 的 fallback 一致。
            System.Type moduleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType != null)
            {
                Component module = host.AddComponent(moduleType);
                moduleType.GetMethod("AssignDefaultActions")?.Invoke(module, null);
            }
            else
            {
                host.AddComponent<StandaloneInputModule>();
            }
        }
    }
}
