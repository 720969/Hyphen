using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hyphen.Editor
{
    public static class HyphenMenuItems
    {
        private const string DefaultFontPath = "Assets/Hyphen/Resources/Fonts/notosans_cjk_sc_regular.bytes";

        private static void ApplyDefaultSettings(HyphenLabel label)
        {
            label.Text = "Text Label";
            label.FontAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultFontPath);
            label.FontSize = 24f;
            label.Wrap = WrapMode.NONE;
            label.OverflowMode = Overflow.NONE;
            label.TextColor = Color.white;
            label.DisableEffect();
            label.raycastTarget = true;
        }

        private static void CreateLabelInternal(MenuCommand menuCommand)
        {
            // If no Canvas exists in the scene, create one (like Unity's built-in UI menu does)
            Canvas canvas = menuCommand.context as Canvas;
            if (canvas == null)
                canvas = Object.FindFirstObjectByType<Canvas>();

            if (canvas == null)
            {
                // Create Canvas + CanvasScaler + GraphicRaycaster
                var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                // Create EventSystem if none exists
                if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                {
                    var esGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                        typeof(UnityEngine.EventSystems.StandaloneInputModule));
                }

                Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            }

            var go = new GameObject("Text (Hyphen)", typeof(RectTransform), typeof(CanvasRenderer), typeof(HyphenLabel));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 50);

            var label = go.GetComponent<HyphenLabel>();
            ApplyDefaultSettings(label);

            var cr = go.GetComponent<CanvasRenderer>();
            cr.cullTransparentMesh = false;

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject ?? canvas.gameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create Text (Hyphen)");
            Selection.activeGameObject = go;
        }

        // Hierarchy right-click: GameObject/UI/Text (Hyphen)
        [MenuItem("GameObject/UI (Canvas)/Text (Hyphen)", false, 2000)]
        public static void CreateLabel(MenuCommand menuCommand)
        {
            CreateLabelInternal(menuCommand);
        }

        [MenuItem("Assets/Create/Hyphen/Font Asset", false, 100)]
        public static void CreateFontAsset()
        {
            var selected = Selection.activeObject;
            if (selected == null)
            {
                Debug.LogWarning("[Hyphen] Select a .ttf or .otf file first.");
                return;
            }

            var path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path) || (!path.EndsWith(".ttf") && !path.EndsWith(".otf") && !path.EndsWith(".ttc")))
            {
                Debug.LogWarning($"[Hyphen] Selected asset '{path}' is not a .ttf/.otf/.ttc file.");
                return;
            }

            var fontBytes = System.IO.File.ReadAllBytes(path);
            var dir = System.IO.Path.GetDirectoryName(path);
            var fontName = System.IO.Path.GetFileNameWithoutExtension(path);
            var destPath = $"{dir}/{fontName}.bytes";

            if (AssetDatabase.LoadAssetAtPath<TextAsset>(destPath) != null)
            {
                Debug.Log($"[Hyphen] Font asset already exists at {destPath}");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(destPath);
                return;
            }

            System.IO.File.WriteAllBytes(destPath, fontBytes);
            AssetDatabase.ImportAsset(destPath, ImportAssetOptions.ForceUpdate);

            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(destPath);
            if (textAsset != null)
            {
                Debug.Log($"[Hyphen] Created font asset: {destPath} ({fontBytes.Length} bytes)");
                Selection.activeObject = textAsset;
                EditorGUIUtility.PingObject(textAsset);
            }
        }

        [MenuItem("Assets/Create/Hyphen/Font Asset", true)]
        public static bool CreateFontAssetValidate()
        {
            var selected = Selection.activeObject;
            if (selected == null) return false;
            var path = AssetDatabase.GetAssetPath(selected);
            return !string.IsNullOrEmpty(path) &&
                   (path.EndsWith(".ttf") || path.EndsWith(".otf") || path.EndsWith(".ttc"));
        }
    }
}
