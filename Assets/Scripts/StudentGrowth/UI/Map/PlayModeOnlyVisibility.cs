using System;
using UnityEngine;

namespace StudentGrowth
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class PlayModeOnlyVisibility : MonoBehaviour
    {
        [SerializeField] private GameObject[] playModeOnlyObjects = Array.Empty<GameObject>();

        public void Configure(params GameObject[] targets)
        {
            playModeOnlyObjects = targets ?? Array.Empty<GameObject>();
            ApplyVisibility();
        }

        public bool Contains(GameObject target)
        {
            if (target == null || playModeOnlyObjects == null)
            {
                return false;
            }

            for (int i = 0; i < playModeOnlyObjects.Length; i++)
            {
                if (playModeOnlyObjects[i] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private void Awake()
        {
            ApplyVisibility();
        }

        private void OnEnable()
        {
            ApplyVisibility();
        }

#if UNITY_EDITOR
        private void Update()
        {
            ApplyVisibility();
        }
#endif

        private void ApplyVisibility()
        {
            bool shouldBeVisible = Application.isPlaying;
            if (playModeOnlyObjects == null)
            {
                return;
            }

            for (int i = 0; i < playModeOnlyObjects.Length; i++)
            {
                GameObject target = playModeOnlyObjects[i];
                if (target != null && target.activeSelf != shouldBeVisible)
                {
                    target.SetActive(shouldBeVisible);
                }
            }
        }
    }
}
