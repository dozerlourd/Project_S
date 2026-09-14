using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectS
{
    public static class RtsSceneFlow
    {
        public const string DefaultMainMenuSceneName = "MainMenu";
        public const string DefaultMatchSceneName = "MapCreate_Scene";

        public static event Action QuitRequested;

        public static bool CanLoadScene(string sceneName)
        {
            return !string.IsNullOrWhiteSpace(sceneName)
                && Application.CanStreamedLevelBeLoaded(sceneName);
        }

        public static bool TryLoadScene(string sceneName)
        {
            if (!CanLoadScene(sceneName))
            {
                Debug.LogWarning($"Cannot load scene '{sceneName}'. Add it to Build Settings or configure another scene name.");
                return false;
            }

            return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single) != null;
        }

        public static void RequestApplicationQuit(bool quitApplication = true)
        {
            QuitRequested?.Invoke();
#if !UNITY_EDITOR
            if (quitApplication)
            {
                Application.Quit();
            }
#endif
        }
    }
}
