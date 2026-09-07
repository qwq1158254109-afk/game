using System;
using UnityEngine;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class RpgPlayerController : MonoBehaviour
    {
        public float moveSpeed = 3f;
        public bool canMove = true;
        public Rigidbody2D body;
        public Collider2D bodyCollider;
        public SpriteRenderer spriteRenderer;
        public Animator animator;
        public PlayerFacingInteractor facingInteractor;
        public MapInteractionPromptView promptView;
        public MapInteractionFlowController interactionFlowController;
        public bool useMovementBounds;
        public Vector2 movementMin = new Vector2(-3f, -1.45f);
        public Vector2 movementMax = new Vector2(3f, 1.45f);
        public KeyCode interactKey = KeyCode.E;
        public KeyCode alternateInteractKey = KeyCode.Space;
        public KeyCode confirmInteractKey = KeyCode.Return;
        public KeyCode runKey = KeyCode.LeftShift;
        public KeyCode alternateRunKey = KeyCode.RightShift;
        public float runSpeedMultiplier = 2f;

        private Vector2 moveInput;
        private bool isRunning;
        private bool legacyInputBlocked;

        public Vector2 FacingDirection { get; private set; } = Vector2.down;
        public bool IsMoving => canMove && moveInput.sqrMagnitude > 0f;
        public bool IsRunning => IsMoving && isRunning;

        private void Awake()
        {
            EnsureComponents();
            SetFacingDirection(FacingDirection);
        }

        private void Update()
        {
            ReadMovementInput();
            if (GameplayInputGate.IsBlocked)
            {
                UpdateAnimator();
                return;
            }

            RefreshFacingAndPrompt();
            if (IsInteractPressed())
            {
                TryInteract();
            }

            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            if (!canMove || moveInput.sqrMagnitude <= 0f)
            {
                return;
            }

            EnsureComponents();
            float speed = moveSpeed * GetMoveSpeedMultiplier();
            Vector2 nextPosition = ClampMovePosition(body.position + moveInput * speed * Time.fixedDeltaTime);
            body.MovePosition(nextPosition);
        }

        public void SetFacingDirection(Vector2 direction)
        {
            FacingDirection = MapFacingInteractionUtility.ToCardinal(direction, FacingDirection);
            if (facingInteractor != null)
            {
                facingInteractor.SetFacingDirection(FacingDirection);
            }
        }

        public void TeleportTo(Vector2 position)
        {
            EnsureComponents();
            position = ClampMovePosition(position);
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            body.position = position;
            body.linearVelocity = Vector2.zero;
        }

        public Vector2 ClampMovePosition(Vector2 position)
        {
            if (!useMovementBounds)
            {
                return position;
            }

            return new Vector2(
                Mathf.Clamp(position.x, movementMin.x, movementMax.x),
                Mathf.Clamp(position.y, movementMin.y, movementMax.y));
        }

        private void ReadMovementInput()
        {
            moveInput = Vector2.zero;
            isRunning = false;
            if (!canMove || GameplayInputGate.IsBlocked)
            {
                return;
            }

            if (IsKeyHeld(KeyCode.A, "aKey") || IsKeyHeld(KeyCode.LeftArrow, "leftArrowKey"))
            {
                moveInput.x -= 1f;
            }

            if (IsKeyHeld(KeyCode.D, "dKey") || IsKeyHeld(KeyCode.RightArrow, "rightArrowKey"))
            {
                moveInput.x += 1f;
            }

            if (IsKeyHeld(KeyCode.W, "wKey") || IsKeyHeld(KeyCode.UpArrow, "upArrowKey"))
            {
                moveInput.y += 1f;
            }

            if (IsKeyHeld(KeyCode.S, "sKey") || IsKeyHeld(KeyCode.DownArrow, "downArrowKey"))
            {
                moveInput.y -= 1f;
            }

            if (moveInput.sqrMagnitude > 1f)
            {
                moveInput.Normalize();
            }

            isRunning = moveInput.sqrMagnitude > 0f && IsRunHeld();
        }

        private void RefreshFacingAndPrompt()
        {
            if (moveInput.sqrMagnitude > 0f)
            {
                SetFacingDirection(moveInput);
            }

            if (facingInteractor == null)
            {
                return;
            }

            MapInteractionTarget target = facingInteractor.RefreshCurrentTarget(transform.position);
            if (interactionFlowController != null)
            {
                interactionFlowController.RefreshTarget(target);
            }
            else if (promptView != null)
            {
                promptView.ShowTarget(target);
            }
        }

        private void TryInteract()
        {
            if (facingInteractor == null)
            {
                if (promptView != null)
                {
                    promptView.ShowMessage("没有面向可互动对象。");
                }

                return;
            }

            MapInteractionTarget target = facingInteractor.currentTarget;
            if (interactionFlowController != null && interactionFlowController.TryBeginInteraction(target))
            {
                return;
            }

            LocationActionExecutionResult result = facingInteractor.TryInteractCurrent();
            if (promptView != null && result != null)
            {
                promptView.ShowMessage(result.message);
            }
        }

        private void UpdateAnimator()
        {
            if (animator == null)
            {
                return;
            }

            SetAnimatorFloat("MoveX", FacingDirection.x);
            SetAnimatorFloat("MoveY", FacingDirection.y);
            float speed = moveInput.magnitude;
            bool isMoving = speed > 0f;
            SetAnimatorFloat("Speed", isMoving ? GetMoveSpeedMultiplier() : 0f);
            SetAnimatorBool("Moving", isMoving);
            SetAnimatorBool("IsMoving", isMoving);
        }

        private float GetMoveSpeedMultiplier()
        {
            return isRunning ? Mathf.Max(1f, runSpeedMultiplier) : 1f;
        }

        private void SetAnimatorFloat(string parameterName, float value)
        {
            if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Float))
            {
                animator.SetFloat(parameterName, value);
            }
        }

        private void SetAnimatorBool(string parameterName, bool value)
        {
            if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(parameterName, value);
            }
        }

        private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType type)
        {
            if (animator == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == type && parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsInteractPressed()
        {
            return IsKeyPressed(interactKey, "eKey")
                || IsKeyPressed(alternateInteractKey, "spaceKey")
                || IsKeyPressed(confirmInteractKey, "enterKey");
        }

        private bool IsRunHeld()
        {
            return IsKeyHeld(runKey, "leftShiftKey")
                || IsKeyHeld(alternateRunKey, "rightShiftKey");
        }

        private bool IsKeyHeld(KeyCode legacyKey, string inputSystemKey)
        {
            if (!legacyInputBlocked)
            {
                try
                {
                    if (Input.GetKey(legacyKey))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    legacyInputBlocked = true;
                }
            }

            return ReadInputSystemKey(inputSystemKey, "isPressed");
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

        private void EnsureComponents()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
            }

            body.gravityScale = 0f;
            body.freezeRotation = true;

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }

            if (bodyCollider == null)
            {
                CapsuleCollider2D capsule = gameObject.AddComponent<CapsuleCollider2D>();
                capsule.size = new Vector2(0.45f, 0.72f);
                capsule.offset = new Vector2(0f, -0.08f);
                bodyCollider = capsule;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (facingInteractor == null)
            {
                facingInteractor = GetComponent<PlayerFacingInteractor>();
            }

            if (interactionFlowController == null)
            {
                interactionFlowController = FindObjectOfType<MapInteractionFlowController>();
            }
        }
    }
}
