using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hyphen.Editor
{
    [CustomEditor(typeof(HyphenLabel))]
    [CanEditMultipleObjects]
    public class HyphenLabelInspector : UnityEditor.Editor
    {
        public override string GetInfoString()
        {
            var label = (HyphenLabel)target;
            return $"Hyphen • Font: {label.FontAsset?.name ?? "None"} • Size: {label.FontSize}";
        }

        private SerializedProperty _text;
        private SerializedProperty _fontAsset;
        private SerializedProperty _fontSize;
        private SerializedProperty _useDistanceField;
        private SerializedProperty _hAlignment;
        private SerializedProperty _vAlignment;
        private SerializedProperty _overflow;
        private SerializedProperty _wrapMode;
        private SerializedProperty _lineSpacing;
        private SerializedProperty _additionalKerning;
        private SerializedProperty _textColor;

        private SerializedProperty _shadowEnabled;
        private SerializedProperty _shadowColor;
        private SerializedProperty _shadowOffset;
        private SerializedProperty _outlineEnabled;
        private SerializedProperty _outlineSize;
        private SerializedProperty _outlineColor;
        private SerializedProperty _glowSize;
        private SerializedProperty _glowColor;
        private SerializedProperty _raycastTarget;
        private SerializedProperty _maskable;

        private static readonly string[] _hLabels = { "L", "C", "R" };
        private static readonly string[] _vLabels = { "T", "M", "B" };

        // Box style cache
        private static GUIStyle _boxHeaderStyle;
        private static GUIStyle _boxBodyStyle;

        private static GUIStyle BoxHeader => _boxHeaderStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            padding = new RectOffset(10, 10, 2, 2)
        };

        private static GUIStyle BoxBody => _boxBodyStyle ??= new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 5, 5)
        };

        private void OnEnable()
        {
            _text = serializedObject.FindProperty("_text");
            _fontAsset = serializedObject.FindProperty("_fontAsset");
            _fontSize = serializedObject.FindProperty("_fontSize");
            _useDistanceField = serializedObject.FindProperty("_useDistanceField");
            _raycastTarget = serializedObject.FindProperty("m_RaycastTarget");
            _maskable = serializedObject.FindProperty("m_Maskable");
            _hAlignment = serializedObject.FindProperty("_hAlignment");
            _vAlignment = serializedObject.FindProperty("_vAlignment");
            _overflow = serializedObject.FindProperty("_overflow");
            _wrapMode = serializedObject.FindProperty("_wrapMode");
            _lineSpacing = serializedObject.FindProperty("_lineSpacing");
            _additionalKerning = serializedObject.FindProperty("_additionalKerning");
            _textColor = serializedObject.FindProperty("_textColor");

            _shadowEnabled = serializedObject.FindProperty("_shadowEnabled");
            _shadowColor = serializedObject.FindProperty("_shadowColor");
            _shadowOffset = serializedObject.FindProperty("_shadowOffset");
            _outlineEnabled = serializedObject.FindProperty("_outlineEnabled");
            _outlineSize = serializedObject.FindProperty("_outlineSize");
            _outlineColor = serializedObject.FindProperty("_outlineColor");
            _glowSize = serializedObject.FindProperty("_glowSize");
            _glowColor = serializedObject.FindProperty("_glowColor");
        }

        private void BeginGroup(string title)
        {
            EditorGUILayout.BeginVertical(BoxBody);
            EditorGUILayout.LabelField(title, BoxHeader);
            EditorGUILayout.Space(2);
        }

        private void EndGroup()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Text — multiline textarea
            EditorGUILayout.LabelField("Text");
            _text.stringValue = EditorGUILayout.TextArea(_text.stringValue, GUILayout.MinHeight(60));
            EditorGUILayout.Space(3);

            // --- Font Settings ---
            BeginGroup("Font Settings");
            EditorGUILayout.PropertyField(_fontAsset, new GUIContent("Font Asset"));
            EditorGUILayout.PropertyField(_fontSize);
            EditorGUILayout.PropertyField(_useDistanceField, new GUIContent("Distance Field Enabled"));
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(_raycastTarget);
                EditorGUILayout.PropertyField(_maskable);
            }
            EditorGUILayout.PropertyField(_textColor, new GUIContent("Text Color"));
            EndGroup();

            // --- Alignment ---
            BeginGroup("Alignment");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Vertical", GUILayout.Width(50));
                int vSel = GUILayout.SelectionGrid(_vAlignment.enumValueIndex, _vLabels, 3,
                    GUILayout.Height(22));
                if (vSel != _vAlignment.enumValueIndex)
                    _vAlignment.enumValueIndex = vSel;

                GUILayout.Space(20);

                GUILayout.Label("Horizontal", GUILayout.Width(60));
                int hSel = GUILayout.SelectionGrid(_hAlignment.enumValueIndex, _hLabels, 3,
                    GUILayout.Height(22));
                if (hSel != _hAlignment.enumValueIndex)
                    _hAlignment.enumValueIndex = hSel;
            }
            EndGroup();

            // --- Layout ---
            BeginGroup("Layout");
            EditorGUILayout.PropertyField(_overflow);
            EditorGUILayout.PropertyField(_wrapMode);
            EditorGUILayout.PropertyField(_lineSpacing);
            EditorGUILayout.PropertyField(_additionalKerning);
            EndGroup();

            // --- Effects ---
            BeginGroup("Effects");

            EditorGUILayout.LabelField("Shadow", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_shadowEnabled);
            if (_shadowEnabled.boolValue)
            {
                EditorGUILayout.PropertyField(_shadowColor);
                EditorGUILayout.PropertyField(_shadowOffset);
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Outline", EditorStyles.miniBoldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_outlineEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                if (_outlineEnabled.boolValue)
                {
                    _useDistanceField.boolValue = false;
                    _glowSize.intValue = 0;
                }
            }

            if (_outlineEnabled.boolValue)
            {
                EditorGUILayout.PropertyField(_outlineColor);

                EditorGUI.BeginChangeCheck();
                float sizeVal = EditorGUILayout.FloatField("Outline Size", _outlineSize.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    _outlineSize.floatValue = Mathf.Max(0f, sizeVal);
                }
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Glow", EditorStyles.miniBoldLabel);

            EditorGUI.BeginChangeCheck();
            bool glowOn = EditorGUILayout.Toggle("Glow Enabled", _glowSize.intValue > 0);
            if (EditorGUI.EndChangeCheck())
            {
                if (glowOn)
                {
                    _glowSize.intValue = 1;
                    _useDistanceField.boolValue = true;
                    _outlineSize.floatValue = 0f;
                }
                else
                {
                    _glowSize.intValue = 0;
                }
            }

            if (_glowSize.intValue > 0)
            {
                EditorGUILayout.PropertyField(_glowColor);
            }

            EndGroup();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
