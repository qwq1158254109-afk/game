using System;
using UnityEngine;

namespace StudentGrowth
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class SceneDoor : MonoBehaviour
    {
        public string doorId;
        public string targetSceneName;
        public string targetSpawnPointId;
        public string interactionText = "按 E 进入";
        public bool requireConfirm;
        public bool isLocked;
        public string lockedMessage = "门被锁住了。";
        public int timeCostMinutes;

        private RpgPlayerController playerInside;
        private bool confirmArmed;
        private float confirmExpiresAt;
        private bool legacyInputBlocked;

        private void Reset()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger == null)
            {
                trigger = gameObject.AddComponent<BoxCollider2D>();
            }

            trigger.isTrigger = true;
        }

        private void Awake()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void Update()
        {
            if (GameplayInputGate.IsBlocked)
            {
                return;
            }

            if (playerInside == null)
            {
                return;
            }

            if (SceneTransitionManager.Instance.IsTransitioning)
            {
                return;
            }

            if (IsInteractPressed())
            {
                TryUseDoor();
            }

            if (playerInside != null)
            {
                InteractionPromptUI.Show(GetPromptText(), this);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            RpgPlayerController player = other.GetComponentInParent<RpgPlayerController>();
            if (player == null)
            {
                return;
            }

            playerInside = player;
            confirmArmed = false;
            InteractionPromptUI.Show(GetPromptText(), this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            RpgPlayerController player = other.GetComponentInParent<RpgPlayerController>();
            if (player == null || player != playerInside)
            {
                return;
            }

            playerInside = null;
            confirmArmed = false;
            InteractionPromptUI.Hide(this);
        }

        private void TryUseDoor()
        {
            if (isLocked)
            {
                InteractionPromptUI.Show(string.IsNullOrEmpty(lockedMessage) ? "门被锁住了。" : lockedMessage, this);
                return;
            }

            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogWarning($"SceneDoor '{name}' has no targetSceneName.");
                InteractionPromptUI.Show("目标场景未配置。", this);
                return;
            }

            if (requireConfirm && (!confirmArmed || Time.unscaledTime > confirmExpiresAt))
            {
                confirmArmed = true;
                confirmExpiresAt = Time.unscaledTime + 3f;
                InteractionPromptUI.Show("再次按 E 确认：" + GetPromptText(), this);
                return;
            }

            confirmArmed = false;
            SceneTransitionManager.Instance.TransitionToScene(targetSceneName, targetSpawnPointId, timeCostMinutes);
        }

        private string GetPromptText()
        {
            if (isLocked && !string.IsNullOrEmpty(lockedMessage))
            {
                return lockedMessage;
            }

            return string.IsNullOrEmpty(interactionText) ? "按 E 进入" : interactionText;
        }

        private bool IsInteractPressed()
        {
            return IsKeyPressed(KeyCode.E, "eKey") || IsKeyPressed(KeyCode.Space, "spaceKey") || IsKeyPressed(KeyCode.Return, "enterKey");
        }

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
                catch (InvalidOperationException)
                {
                    legacyInputBlocked = true;
                }
            }

            return ReadInputSystemKey(inputSystemKey, "wasPressedThisFrame");
        }

        private static bool ReadInputSystemKey(string keyPropertyName, string statePropertyName)
        {
            Type keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
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
    }
}
