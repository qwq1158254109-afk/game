using System.Collections;
using UnityEngine;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class UiThemeRuntimeAuditReporter : MonoBehaviour
    {
        [SerializeField] private UIThemeConfig theme;
        [SerializeField] private string scopeLabel = "Runtime";
        [SerializeField] private bool runOnStart;
        [SerializeField] private bool logWhenNoIssues = true;
        [SerializeField, Min(0)] private int startDelayFrames = 1;
        [SerializeField] private bool scanChildCanvases = true;

        private IEnumerator Start()
        {
            if (!runOnStart)
            {
                yield break;
            }

            for (int i = 0; i < startDelayFrames; i++)
            {
                yield return null;
            }

            RunAudit();
        }

        [ContextMenu("Run UI Theme Audit")]
        public void RunAudit()
        {
            UiThemeAuditSummary summary = CreateAuditSummary();
            if (summary.Issues.Count == 0 && !logWhenNoIssues)
            {
                return;
            }

            Debug.Log(summary.BuildLog(), this);
        }

        public UiThemeAuditSummary CreateAuditSummary()
        {
            UiThemeAuditSummary summary = new UiThemeAuditSummary
            {
                themeFound = GameUiStyle.ResolveTheme(theme) != null
            };

            if (!scanChildCanvases)
            {
                UiThemeAuditUtility.AuditUiRoot(summary, gameObject, ResolveScope(gameObject));
                return summary;
            }

            Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
            int auditedCanvasCount = 0;
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || HasAncestorCanvasInsideAuditRoot(canvas))
                {
                    continue;
                }

                auditedCanvasCount++;
                UiThemeAuditUtility.AuditUiRoot(summary, canvas.gameObject, ResolveScope(canvas.gameObject));
            }

            if (auditedCanvasCount == 0)
            {
                UiThemeAuditUtility.AuditUiRoot(summary, gameObject, ResolveScope(gameObject));
            }

            return summary;
        }

        public void Configure(
            UIThemeConfig auditTheme,
            string scope,
            bool shouldRunOnStart,
            bool shouldLogWhenNoIssues,
            int delayFrames,
            bool shouldScanChildCanvases)
        {
            theme = auditTheme;
            scopeLabel = string.IsNullOrEmpty(scope) ? "Runtime" : scope;
            runOnStart = shouldRunOnStart;
            logWhenNoIssues = shouldLogWhenNoIssues;
            startDelayFrames = Mathf.Max(0, delayFrames);
            scanChildCanvases = shouldScanChildCanvases;
        }

        private string ResolveScope(GameObject root)
        {
            string label = string.IsNullOrEmpty(scopeLabel) ? "Runtime" : scopeLabel;
            if (root == null)
            {
                return label;
            }

            return label + ":" + UiThemeAuditUtility.GetHierarchyPath(root.transform);
        }

        private bool HasAncestorCanvasInsideAuditRoot(Canvas canvas)
        {
            if (canvas == null || canvas.transform == transform)
            {
                return false;
            }

            Transform current = canvas.transform.parent;
            while (current != null)
            {
                if (current == transform)
                {
                    return false;
                }

                if (current.GetComponent<Canvas>() != null)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
