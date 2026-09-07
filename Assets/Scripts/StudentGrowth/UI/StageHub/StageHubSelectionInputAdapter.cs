using System;
using UnityEngine;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class StageHubSelectionInputAdapter : MonoBehaviour
    {
        public StageHubSelectionController controller;

        private bool legacyInputBlocked;

        private void Update()
        {
            if (GameplayInputGate.IsBlocked)
            {
                return;
            }

            if (controller == null)
            {
                return;
            }

            if (Pressed(KeyCode.A, "aKey") || Pressed(KeyCode.LeftArrow, "leftArrowKey"))
            {
                controller.MoveSelection(Vector2Int.left);
            }
            else if (Pressed(KeyCode.D, "dKey") || Pressed(KeyCode.RightArrow, "rightArrowKey"))
            {
                controller.MoveSelection(Vector2Int.right);
            }
            else if (Pressed(KeyCode.W, "wKey") || Pressed(KeyCode.UpArrow, "upArrowKey"))
            {
                controller.MoveSelection(Vector2Int.up);
            }
            else if (Pressed(KeyCode.S, "sKey") || Pressed(KeyCode.DownArrow, "downArrowKey"))
            {
                controller.MoveSelection(Vector2Int.down);
            }

            if (Pressed(KeyCode.E, "eKey") || Pressed(KeyCode.Return, "enterKey"))
            {
                controller.ConfirmSelected();
            }

            if (Pressed(KeyCode.Escape, "escapeKey"))
            {
                controller.Back();
            }
        }

        private bool Pressed(KeyCode legacyKey, string inputSystemKey)
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

            Type keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            object keyboard = keyboardType?.GetProperty("current")?.GetValue(null, null);
            object key = keyboardType?.GetProperty(inputSystemKey)?.GetValue(keyboard, null);
            object value = key?.GetType().GetProperty("wasPressedThisFrame")?.GetValue(key, null);
            return value is bool pressed && pressed;
        }
    }
}
