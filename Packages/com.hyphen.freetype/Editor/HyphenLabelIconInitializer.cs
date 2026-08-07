using UnityEditor;
using UnityEngine;

namespace Hyphen.Editor
{
    [InitializeOnLoad]
    public static class HyphenLabelIconInitializer
    {
        static HyphenLabelIconInitializer()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Editor Resources/HyphenLabelIcon.png");
            if (icon == null) return;

            var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Hyphen/Runtime/Text/HyphenLabel.cs");
            if (script == null) return;

            // Check if icon is already set correctly
            var currentIcon = EditorGUIUtility.GetIconForObject(script);
            if (currentIcon != icon)
            {
                EditorGUIUtility.SetIconForObject(script, icon);
                EditorUtility.SetDirty(script);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
