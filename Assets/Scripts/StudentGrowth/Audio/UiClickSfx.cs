using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StudentGrowth
{
    /// <summary>
    /// 给场景里所有 Button 自动挂一个点击音，不需要改任何预制体。
    /// 每次场景加载后扫描一遍，已经挂过的跳过。
    /// </summary>
    public sealed class UiClickSfx : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            Button button = GetComponent<Button>();
            if (button != null && !button.interactable)
            {
                return;
            }

            Sfx.Ui.Click();
        }
    }

    internal static class UiClickSfxInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || button.GetComponent<UiClickSfx>() != null)
                {
                    continue;
                }

                button.gameObject.AddComponent<UiClickSfx>();
            }
        }
    }
}
