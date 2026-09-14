using UnityEngine;

namespace ProjectS.UI
{
    public sealed class RtsMainMenu : MonoBehaviour
    {
        private const string DefaultBackgroundResourcePath = "UI/MainMenu_Battlefield_Background";
        private const string DefaultButtonResourcePath = "UI/MainMenu_Command_Button";

        [Header("Scene Flow")]
        [SerializeField] private string gameSceneName = RtsSceneFlow.DefaultMatchSceneName;

        [Header("Visuals")]
        [SerializeField] private Texture2D backgroundTexture;
        [SerializeField] private Texture2D buttonTexture;
        [SerializeField] private string backgroundResourcePath = DefaultBackgroundResourcePath;
        [SerializeField] private string buttonResourcePath = DefaultButtonResourcePath;

        private Texture2D fallbackBackgroundTexture;
        private Texture2D fallbackButtonTexture;
        private bool isLoading;

        public string GameSceneName => gameSceneName;
        public bool IsLoading => isLoading;
        public Texture2D BackgroundTexture => backgroundTexture;
        public Texture2D ButtonTexture => buttonTexture;

        private void Awake()
        {
            ResolveTextures();
        }

        private void OnDestroy()
        {
            DestroyFallbackTexture(ref fallbackBackgroundTexture);
            DestroyFallbackTexture(ref fallbackButtonTexture);
        }

        public void Configure(string sceneName, Texture2D background = null, Texture2D button = null)
        {
            gameSceneName = sceneName;
            backgroundTexture = background;
            buttonTexture = button;
            ResolveTextures();
        }

        public bool TryStartMatch()
        {
            if (isLoading || !RtsSceneFlow.TryLoadScene(gameSceneName))
            {
                return false;
            }

            isLoading = true;
            return true;
        }

        public bool TryRequestQuit()
        {
            if (isLoading)
            {
                return false;
            }

            RtsSceneFlow.RequestApplicationQuit();
            return true;
        }

        private void OnGUI()
        {
            ResolveTextures();

            var screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
            GUI.DrawTexture(screenRect, backgroundTexture, ScaleMode.ScaleAndCrop);

            var scale = Mathf.Clamp(Screen.height / 900f, 0.72f, 1.1f);
            var margin = Mathf.Max(24f, Screen.width * 0.07f);
            var contentWidth = Mathf.Min(500f * scale, Screen.width - margin * 2f);
            var contentY = Mathf.Clamp(Screen.height * 0.2f, 72f, 220f);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Mathf.RoundToInt(58f * scale),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.87f, 0.96f, 1f, 1f) }
            };
            var subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Mathf.RoundToInt(17f * scale),
                normal = { textColor = new Color(0.25f, 0.86f, 1f, 1f) }
            };
            var buttonStyle = CreateButtonStyle(scale);

            GUI.Label(new Rect(margin, contentY, contentWidth, 76f * scale), "PROJECT S", titleStyle);
            GUI.Label(
                new Rect(margin + 4f, contentY + 66f * scale, contentWidth, 30f * scale),
                "TACTICAL COMMAND",
                subtitleStyle);

            var buttonWidth = Mathf.Min(430f * scale, contentWidth);
            var buttonHeight = 72f * scale;
            var firstButtonY = contentY + 154f * scale;
            var previousEnabled = GUI.enabled;
            GUI.enabled = !isLoading;
            if (GUI.Button(new Rect(margin, firstButtonY, buttonWidth, buttonHeight), "PLAY", buttonStyle))
            {
                TryStartMatch();
            }

            if (GUI.Button(
                new Rect(margin, firstButtonY + 88f * scale, buttonWidth, buttonHeight),
                "QUIT",
                buttonStyle))
            {
                TryRequestQuit();
            }

            GUI.enabled = previousEnabled;
            if (isLoading)
            {
                GUI.Label(
                    new Rect(margin, firstButtonY + 174f * scale, buttonWidth, 28f * scale),
                    "DEPLOYING...",
                    subtitleStyle);
            }
        }

        private GUIStyle CreateButtonStyle(float scale)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(22f * scale),
                fontStyle = FontStyle.Bold,
                normal = { background = buttonTexture, textColor = Color.white },
                hover = { background = buttonTexture, textColor = new Color(0.5f, 0.95f, 1f, 1f) },
                active = { background = buttonTexture, textColor = new Color(1f, 0.84f, 0.32f, 1f) },
                focused = { background = buttonTexture, textColor = Color.white }
            };
            return style;
        }

        private void ResolveTextures()
        {
            if (backgroundTexture == null && !string.IsNullOrWhiteSpace(backgroundResourcePath))
            {
                backgroundTexture = UnityEngine.Resources.Load<Texture2D>(backgroundResourcePath);
            }

            if (buttonTexture == null && !string.IsNullOrWhiteSpace(buttonResourcePath))
            {
                buttonTexture = UnityEngine.Resources.Load<Texture2D>(buttonResourcePath);
            }

            if (backgroundTexture == null)
            {
                fallbackBackgroundTexture ??= CreateSolidTexture(new Color(0.035f, 0.06f, 0.075f, 1f));
                backgroundTexture = fallbackBackgroundTexture;
            }

            if (buttonTexture == null)
            {
                fallbackButtonTexture ??= CreateSolidTexture(new Color(0.08f, 0.18f, 0.22f, 0.96f));
                buttonTexture = fallbackButtonTexture;
            }
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void DestroyFallbackTexture(ref Texture2D texture)
        {
            if (texture != null)
            {
                Destroy(texture);
                texture = null;
            }
        }
    }
}
