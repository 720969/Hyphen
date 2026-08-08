using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hyphen
{
    /// <summary>
    /// UGUI text component powered by FreeType.
    /// Port of cocos2dx CCLabel, adapted for Unity MaskableGraphic.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Hyphen Label")]
    [DisallowMultipleComponent]
    [Icon("HyphenLabelIcon")]
    public class HyphenLabel : MaskableGraphic, UnityEngine.UI.ILayoutElement
    {
        public struct LetterInfo
        {
            public char utf16Char;
            public bool valid;
            public float positionX;
            public float positionY;
            public int atlasIndex;
            public int lineIndex;
        }

        // --- Serialized fields ---

        [SerializeField] private string _text = "Text Label";
        [SerializeField] private TextAsset _fontAsset = null;
        [SerializeField] private float _fontSize = 24f;
        [SerializeField] private bool _useDistanceField = false;
        [SerializeField] private TextHAlignment _hAlignment = TextHAlignment.LEFT;
        [SerializeField] private TextVAlignment _vAlignment = TextVAlignment.TOP;
        [SerializeField] private Overflow _overflow = Overflow.NONE;
        [SerializeField] private WrapMode _wrapMode = WrapMode.NONE;
        [SerializeField] private float _maxLineWidth = 0f;
        [SerializeField] private float _lineSpacing = 0f;
        [SerializeField] private float _additionalKerning = 0f;
        [SerializeField] private Color _textColor = Color.white;

        [SerializeField] private bool _shadowEnabled = false;
        [SerializeField] private Color _shadowColor = new Color(0.431f, 0.431f, 0.431f, 1f); // #6E6E6E
        [SerializeField] private Vector2 _shadowOffset = new Vector2(2, -2);
        [SerializeField] private bool _outlineEnabled = false;
        [SerializeField] private float _outlineSize = 1f;
        [SerializeField] private Color _outlineColor = Color.red; // #FF0000
        [SerializeField] private int _glowSize = 0;
        [SerializeField] private Color _glowColor = Color.red; // #FF0000

        // --- Internal state ---

        private HyphenFontAtlas _fontAtlas;
        private bool _contentDirty = true;
        private bool _fontRegistered = false;

        private int _numberOfLines;
        private int _lengthOfString;
        private float _lineHeight;
        private float _textDesiredHeight;
        private float _letterOffsetY;
        private float _tailoredTopY;
        private float _tailoredBottomY;
        private float _originalFontSize;
        private float _originalLineHeight;
        private float _lastScaleFactor = 1f;

        private List<LetterInfo> _lettersInfo = new List<LetterInfo>();
        private List<float> _linesWidth = new List<float>();
        private List<float> _linesOffsetX = new List<float>();
        private int[] _horizontalKernings;

        private HyphenLabelTextFormatter _formatter;
        private float _shrinkScale = 1f;

        internal const float BMFontScale = 1f;

        // --- Public API ---

        public string Text
        {
            get => _text;
            set { if (value != _text) { _text = value ?? ""; _contentDirty = true; SetVerticesDirty(); } }
        }

        public TextAsset FontAsset
        {
            get => _fontAsset;
            set { if (value != _fontAsset) { _fontAsset = value; _fontRegistered = false; _contentDirty = true; SetVerticesDirty(); } }
        }

        public float FontSize
        {
            get => _fontSize;
            set { if (!Mathf.Approximately(_fontSize, value)) { _fontSize = value; _fontRegistered = false; _contentDirty = true; SetVerticesDirty(); } }
        }

        public bool UseDistanceField
        {
            get => _useDistanceField;
            set { if (_useDistanceField != value) { _useDistanceField = value; _fontRegistered = false; _contentDirty = true; SetVerticesDirty(); } }
        }

        public TextHAlignment HAlignment
        {
            get => _hAlignment;
            set { if (_hAlignment != value) { _hAlignment = value; _contentDirty = true; SetVerticesDirty(); } }
        }

        public TextVAlignment VAlignment
        {
            get => _vAlignment;
            set { if (_vAlignment != value) { _vAlignment = value; _contentDirty = true; SetVerticesDirty(); } }
        }

        public Overflow OverflowMode
        {
            get => _overflow;
            set { if (_overflow != value) { _overflow = value; _contentDirty = true; SetVerticesDirty(); } }
        }

        public WrapMode Wrap
        {
            get => _wrapMode;
            set { if (_wrapMode != value) { _wrapMode = value; _contentDirty = true; SetVerticesDirty(); } }
        }

        public float MaxLineWidth
        {
            get => _maxLineWidth;
            set { if (!Mathf.Approximately(_maxLineWidth, value)) { _maxLineWidth = value; _contentDirty = true; SetVerticesDirty(); } }
        }

        public float LineSpacing
        {
            get => _lineSpacing;
            set { if (!Mathf.Approximately(_lineSpacing, value)) { _lineSpacing = value; _contentDirty = true; SetVerticesDirty(); } }
        }

        public float AdditionalKerning
        {
            get => _additionalKerning;
            set { if (!Mathf.Approximately(_additionalKerning, value)) { _additionalKerning = value; _contentDirty = true; SetVerticesDirty(); } }
        }

        public Color TextColor
        {
            get => _textColor;
            set { if (_textColor != value) { _textColor = value; RefreshMaterial(); _contentDirty = true; SetVerticesDirty(); } }
        }

        // --- cocos2dx-style API ---

        public void SetString(string text) => Text = text;
        public string GetString() => _text;
        public void SetTextColor(Color color) => TextColor = color;
        public Color GetTextColor() => _textColor;
        public void SetAlignment(TextHAlignment h) { HAlignment = h; }
        public void SetAlignment(TextHAlignment h, TextVAlignment v) { HAlignment = h; VAlignment = v; }
        public void SetOverflow(Overflow o) => OverflowMode = o;
        public void SetMaxLineWidth(float w) => MaxLineWidth = w;
        public void EnableWrapMode(bool enable) => Wrap = enable ? WrapMode.WORD : WrapMode.NONE;
        public void SetLineSpacing(float spacing) => LineSpacing = spacing;
        public void SetAdditionalKerning(float kerning) => AdditionalKerning = kerning;

        public void EnableShadow(Color shadowColor, Vector2 offset)
        {
            _shadowEnabled = true;
            _shadowColor = shadowColor;
            _shadowOffset = offset;
            _contentDirty = true;
            RefreshMaterial();
            SetVerticesDirty();
        }

        public void EnableOutline(Color outlineColor, float outlineSize)
        {
            _outlineEnabled = true;
            _outlineColor = outlineColor;
            _outlineSize = outlineSize;
            _fontRegistered = false;
            _contentDirty = true;
            RefreshMaterial();
            SetVerticesDirty();
        }

        public void EnableGlow(Color glowColor, int glowSize = 3)
        {
            _glowSize = Mathf.Max(1, glowSize);
            _glowColor = glowColor;
            _useDistanceField = true;
            _outlineSize = 0f;
            _fontRegistered = false;
            _contentDirty = true;
            RefreshMaterial();
            SetVerticesDirty();
        }

        public void DisableEffect()
        {
            _shadowEnabled = false;
            _outlineEnabled = false;
            _glowSize = 0;
            // Preserve remembered color/size values so re-enabling restores them.
            _contentDirty = true;
            RefreshMaterial();
            SetVerticesDirty();
        }

        public void SetTTFConfig(HyphenTTFConfig config)
        {
            _fontSize = config.fontSize;
            _useDistanceField = config.distanceFieldEnabled;
            _outlineSize = config.outlineSize;
            _originalFontSize = config.fontSize;
            _fontRegistered = false;
            _contentDirty = true;
            SetVerticesDirty();
        }

        // --- Internal accessors (for HyphenLabelTextFormatter) ---

        internal int NumberOfLines { get => _numberOfLines; set => _numberOfLines = value; }
        internal int LengthOfString => _lengthOfString;
        internal float LineHeight => _lineHeight;
        internal float LineSpacingVal => _lineSpacing;
        internal float TextDesiredHeight { get => _textDesiredHeight; set => _textDesiredHeight = value; }
        internal float LetterOffsetY { get => _letterOffsetY; set => _letterOffsetY = value; }
        internal List<float> LinesWidth { get => _linesWidth; set => _linesWidth = value; }
        internal List<float> LinesOffsetX { get => _linesOffsetX; set => _linesOffsetX = value; }
        internal float MaxLineWidthVal => _maxLineWidth > 0 ? _maxLineWidth : rectTransform.rect.width;
        internal bool EnableWrapVal => _wrapMode != WrapMode.NONE;
        internal bool LineBreakWithoutSpaces => false;
        internal float BMFontScaleVal => BMFontScale;
        internal int[] HorizontalKernings => _horizontalKernings;
        internal HyphenFontAtlas FontAtlas => _fontAtlas;
        internal float TailoredTopY { get => _tailoredTopY; set => _tailoredTopY = value; }
        internal float TailoredBottomY { get => _tailoredBottomY; set => _tailoredBottomY = value; }
        internal Vector2 ContentSize => rectTransform.rect.size;
        internal float LabelWidth => rectTransform.rect.width;
        internal float LabelHeight => rectTransform.rect.height;
        internal float RenderingFontSize => _fontSize;
        internal float OriginalLineHeight => _originalLineHeight;

        // --- ILayoutElement ---

        private float _preferredWidth;
        private float _preferredHeight;

        public virtual float preferredWidth { get { if (_contentDirty) UpdateContent(); return _preferredWidth; } }
        public virtual float preferredHeight { get { if (_contentDirty) UpdateContent(); return _preferredHeight; } }
        public virtual float minWidth => 0f;
        public virtual float minHeight => _lineHeight;
        public virtual float flexibleWidth => -1f;
        public virtual float flexibleHeight => -1f;
        public virtual int layoutPriority => 1;

        public virtual void CalculateLayoutInputHorizontal() { if (_contentDirty) UpdateContent(); }
        public virtual void CalculateLayoutInputVertical() { if (_contentDirty) UpdateContent(); }

        internal void SetContentSize(Vector2 size)
        {
            _preferredWidth = size.x;
            _preferredHeight = size.y;

            // RESIZE_HEIGHT: resize is actually applied in Update(), which runs
            // outside the Canvas graphic rebuild loop. Setting sizeDelta here would
            // trigger OnRectTransformDimensionsChange inside OnPopulateMesh's rebuild
            // loop → "already inside a graphic rebuild loop" error.
        }

        internal void SetLineHeightInternal(float height) => _lineHeight = height;

        internal void ScaleFontSizeDown(float fontSize)
        {
            if (!Mathf.Approximately(_fontSize, fontSize))
            {
                _fontSize = fontSize;
                _fontRegistered = false;
                _contentDirty = true;
            }
        }

        internal void RecordLetterInfo(float posX, float posY, char utf16Char, int letterIndex, int lineIndex)
        {
            while (_lettersInfo.Count <= letterIndex)
                _lettersInfo.Add(default);
            var info = _lettersInfo[letterIndex];
            info.lineIndex = lineIndex;
            info.utf16Char = utf16Char;
            info.valid = _fontAtlas.HasLetterDefinition(utf16Char) &&
                _fontAtlas.GetLetterDefinitionForChar(utf16Char, out _);
            info.positionX = posX;
            info.positionY = posY;
            _lettersInfo[letterIndex] = info;
        }

        internal void RecordPlaceholderInfo(int letterIndex, char utf16Char)
        {
            while (_lettersInfo.Count <= letterIndex)
                _lettersInfo.Add(default);
            var info = _lettersInfo[letterIndex];
            info.utf16Char = utf16Char;
            info.valid = false;
            _lettersInfo[letterIndex] = info;
        }

        internal LetterInfo GetLetterInfo(int index)
        {
            if (index >= 0 && index < _lettersInfo.Count)
                return _lettersInfo[index];
            return default;
        }

        // --- Unity lifecycle ---

        protected override void OnEnable()
        {
            base.OnEnable();
            _formatter = new HyphenLabelTextFormatter(this);
            var cr = GetComponent<CanvasRenderer>();
            if (cr != null)
                cr.cullTransparentMesh = false;
            EnsureFontRegistered();
            RefreshMaterial();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            _contentDirty = true;
            SetVerticesDirty();
            SetLayoutDirty();
        }

        protected override void OnDisable()
        {
            if (_fontAtlas != null)
            {
                HyphenFontAtlasCache.ReleaseFontAtlas(_fontAtlas);
                _fontAtlas = null;
            }
            base.OnDisable();
        }

        protected override void UpdateGeometry()
        {
            if (_contentDirty)
                UpdateContent();
            base.UpdateGeometry();
        }

        private void Update()
        {
            // Apply RESIZE_HEIGHT outside the Canvas rebuild loop (Update runs between frames)
            if (_overflow == Overflow.RESIZE_HEIGHT && _textDesiredHeight > 0f)
            {
                float currentH = rectTransform.rect.height;
                if (!Mathf.Approximately(currentH, _textDesiredHeight))
                {
                    var sd = rectTransform.sizeDelta;
                    sd.y = _textDesiredHeight;
                    rectTransform.sizeDelta = sd;
                }
            }

            // Detect Canvas scaleFactor change → re-register font for HiDPI
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.rootCanvas != null)
            {
                float currentSf = Mathf.Max(1f, canvas.rootCanvas.scaleFactor);
                if (!Mathf.Approximately(currentSf, _lastScaleFactor))
                {
                    _lastScaleFactor = currentSf;
                    _fontRegistered = false;
                    _contentDirty = true;
                    SetVerticesDirty();
                }
            }
        }

        // --- OnPopulateMesh: 1:1 port of cocos2dx Label::onDraw ---

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_fontAtlas == null || string.IsNullOrEmpty(_text))
                return;

            int textLen = _text.Length;
            if (textLen == 0) return;

            var rt = rectTransform;
            float pivotX = rt.pivot.x;
            float pivotY = rt.pivot.y;
            float rectW = rt.rect.width;
            float rectH = rt.rect.height;
            float minX = -pivotX * rectW;
            float maxX = (1f - pivotX) * rectW;
            float minY = -pivotY * rectH;
            float maxY = (1f - pivotY) * rectH;
            bool clamp = _overflow == Overflow.CLAMP;

            // Determine vertex color for text quads.
            // cocos2dx: v_fragmentColor = node color (white).
            // Outline/Glow shaders: result = v_fragmentColor * color → vertex color = white.
            // Normal/DF shaders: result = v_fragmentColor * textColor * texture.a
            //   → vertex color = _textColor (bake textColor, matching cocos2dx v_fragmentColor * u_textColor).
            bool hasEffect = _outlineEnabled || _glowSize > 0;
            Color textVertColor = hasEffect ? Color.white : _textColor;

            // Shadow pass: draw shadow quads first (behind text).
            // cocos2dx onDrawShadow: u_textColor = u_effectColor = shadowColor,
            //   draws all quads with shadow transform offset.
            // For Normal/DF: vertex color = shadowColor (bake, since shader uses vertex color * textColor * tex).
            // For Outline/Glow: vertex color = shadowColor (shader: result = v_fragmentColor * color,
            //   where color = textColor*font + effectColor*(1-font). With shadow, textColor=effectColor=shadowColor,
            //   so color = shadowColor, result = shadowColor * shadowColor.a. We pass shadowColor as vertex color
            //   and the shader multiplies by it.)
            if (_shadowEnabled)
            {
                for (int i = 0; i < textLen; i++)
                {
                    var info = i < _lettersInfo.Count ? _lettersInfo[i] : default;
                    if (!info.valid) continue;
                    if (!_fontAtlas.GetLetterDefinitionForChar(info.utf16Char, out var letterDef)) continue;
                    if (letterDef.width <= 0 || letterDef.height <= 0) continue;

                    AddQuad(vh, info, letterDef, _shadowColor, _shadowOffset, clamp, minX, maxX, minY, maxY);
                }
            }

            // Text pass.
            // cocos2dx onDraw: u_textColor = textColor, u_effectColor = outlineColor.
            for (int i = 0; i < textLen; i++)
            {
                var info = i < _lettersInfo.Count ? _lettersInfo[i] : default;
                if (!info.valid) continue;
                if (!_fontAtlas.GetLetterDefinitionForChar(info.utf16Char, out var letterDef)) continue;
                if (letterDef.width <= 0 || letterDef.height <= 0) continue;

                AddQuad(vh, info, letterDef, textVertColor, Vector2.zero, clamp, minX, maxX, minY, maxY);
            }
        }

        private void AddQuad(VertexHelper vh, LetterInfo info, HyphenLetterDefinition letterDef,
            Color color, Vector2 offset, bool clamp,
            float clampMinX, float clampMaxX, float clampMinY, float clampMaxY)
        {
            float px = info.positionX + _linesOffsetX[info.lineIndex] + offset.x;
            float py = info.positionY + _letterOffsetY + offset.y;

            float sw = letterDef.width * _shrinkScale;
            float sh = letterDef.height * _shrinkScale;

            var rt = rectTransform;
            float pivotX = rt.pivot.x;
            float pivotY = rt.pivot.y;
            float rectW = ContentSize.x;
            float rectH = ContentSize.y;

            float left = px - pivotX * rectW;
            float right = left + sw;
            float top = py - pivotY * rectH;
            float bottom = top - sh;

            float uL = letterDef.U / HyphenFontAtlas.CacheTextureWidth;
            float uR = (letterDef.U + letterDef.width) / HyphenFontAtlas.CacheTextureWidth;
            float uB = (letterDef.V + letterDef.height) / HyphenFontAtlas.CacheTextureHeight;
            float uT = letterDef.V / HyphenFontAtlas.CacheTextureHeight;

            if (clamp)
            {
                if (right <= clampMinX || left >= clampMaxX || top <= clampMinY || bottom >= clampMaxY)
                    return;

                if (left < clampMinX) { float t = (clampMinX - left) / (right - left); uL = Mathf.Lerp(uL, uR, t); left = clampMinX; }
                if (right > clampMaxX) { float t = (clampMaxX - left) / (right - left); uR = Mathf.Lerp(uL, uR, t); right = clampMaxX; }
                if (bottom < clampMinY) { float t = (clampMinY - bottom) / (top - bottom); uB = Mathf.Lerp(uB, uT, t); bottom = clampMinY; }
                if (top > clampMaxY) { float t = (clampMaxY - bottom) / (top - bottom); uT = Mathf.Lerp(uB, uT, t); top = clampMaxY; }
            }

            int vs = vh.currentVertCount;
            vh.AddVert(new Vector3(left, bottom), color, new Vector2(uL, uB));
            vh.AddVert(new Vector3(left, top), color, new Vector2(uL, uT));
            vh.AddVert(new Vector3(right, top), color, new Vector2(uR, uT));
            vh.AddVert(new Vector3(right, bottom), color, new Vector2(uR, uB));
            vh.AddTriangle(vs, vs + 1, vs + 2);
            vh.AddTriangle(vs, vs + 2, vs + 3);
        }

        // --- Content update ---

        private void UpdateContent()
        {
            if (_formatter == null)
                _formatter = new HyphenLabelTextFormatter(this);
            EnsureFontRegistered();
            if (_fontAtlas == null || string.IsNullOrEmpty(_text))
            {
                _contentDirty = false;
                return;
            }

            ComputeHorizontalKernings(_text);
            _fontAtlas.PrepareLetterDefinitions(_text);
            AlignText();
            _contentDirty = false;
        }

        private void AlignText()
        {
            if (_fontAtlas == null || string.IsNullOrEmpty(_text))
            {
                SetContentSize(Vector2.zero);
                return;
            }

            _lengthOfString = _text.Length;
            _linesWidth.Clear();
            _lettersInfo.Clear();

            switch (_wrapMode)
            {
                case WrapMode.WORD: _formatter.MultilineTextWrapByWord(); break;
                case WrapMode.CHAR: _formatter.MultilineTextWrapByChar(); break;
                default: _formatter.MultilineTextWrapByChar(); break;
            }

            _formatter.ComputeAlignmentOffset();
            _shrinkScale = 1f;

            if (_overflow == Overflow.SHRINK)
            {
                float origLineHeight = _lineHeight;
                float fontSize = _fontSize;
                var origDefs = _fontAtlas.SnapshotLetterDefinitions();
                int i = 0;

                while (_formatter.IsVerticalClamp() || _formatter.IsHorizontalClamp())
                {
                    i++;
                    float newFontSize = fontSize - i;
                    if (newFontSize <= 1f) break;

                    _fontAtlas.RestoreLetterDefinitions(origDefs);
                    float scale = newFontSize / fontSize;
                    _fontAtlas.ScaleFontLetterDefinition(scale);
                    _lineHeight = origLineHeight * scale;

                    _linesWidth.Clear();
                    _lettersInfo.Clear();
                    switch (_wrapMode)
                    {
                        case WrapMode.WORD: _formatter.MultilineTextWrapByWord(); break;
                        case WrapMode.CHAR: _formatter.MultilineTextWrapByChar(); break;
                        default: _formatter.MultilineTextWrapByChar(); break;
                    }
                    _formatter.ComputeAlignmentOffset();
                }

                _shrinkScale = (fontSize - i) / fontSize;
                if (_shrinkScale <= 0f) _shrinkScale = 1f / fontSize;
                _fontAtlas.RestoreLetterDefinitions(origDefs);
                _lineHeight = origLineHeight;
            }
        }

        private void ComputeHorizontalKernings(string text)
        {
            if (_horizontalKernings != null)
                _horizontalKernings = null;
            if (_fontAtlas?.Font == null) return;
            _horizontalKernings = _fontAtlas.Font.GetHorizontalKerningForText(text, out _);
        }

        // --- Font registration ---

        private void EnsureFontRegistered()
        {
            if (_fontRegistered && _fontAtlas != null && HyphenFontAtlasCache.HasFonts)
                return;

            if (_fontAsset == null || _fontAsset.bytes == null || _fontAsset.bytes.Length == 0)
                return;

            string fontName = _fontAsset.name;
            float effectiveOutline = _outlineEnabled ? _outlineSize : 0f;
            bool useDF = _useDistanceField;
            if (effectiveOutline > 0)
                useDF = false;

            // HiDPI: read Canvas scaleFactor for super-sampling (like cocos2dx CC_CONTENT_SCALE_FACTOR)
            float scaleFactor = 1f;
            var canvas = canvasRenderer != null ? GetComponentInParent<Canvas>() : null;
            if (canvas != null && canvas.rootCanvas != null)
            {
                scaleFactor = canvas.rootCanvas.scaleFactor;
                if (scaleFactor < 1f) scaleFactor = 1f;
            }

            _fontAtlas = HyphenFontAtlasCache.GetFontAtlasTTF(
                fontName, _fontAsset.bytes, _fontSize,
                GlyphCollection.DYNAMIC, null, useDF, effectiveOutline, scaleFactor);

            if (_fontAtlas != null)
            {
                _lineHeight = _fontAtlas.LineHeight;
                _originalLineHeight = _lineHeight;
                _contentDirty = true;
                _fontRegistered = true;
                RefreshMaterial();
            }
        }

        // --- Material ---

        public override Texture mainTexture
        {
            get
            {
                if (_fontAtlas != null && _fontAtlas.Textures.Count > 0)
                    return _fontAtlas.GetTexture(0);
                return null;
            }
        }

        private Material _instanceMaterial;

        public override Material material
        {
            get
            {
                string shaderName = GetShaderName();
                if (_instanceMaterial == null || _instanceMaterial.shader.name != shaderName)
                {
                    var shader = Shader.Find(shaderName);
                    if (shader == null)
                    {
                        Debug.LogError($"[Hyphen] Shader '{shaderName}' not found!");
                        shader = Shader.Find("UI/Default");
                    }
                    _instanceMaterial = new Material(shader);
                }
                return _instanceMaterial;
            }
        }

        private string GetShaderName()
        {
            if (_glowSize > 0) return "Hyphen/Label Glow";
            if (_outlineEnabled) return "Hyphen/Label Outline";
            if (_useDistanceField) return "Hyphen/Label DistanceField";
            return "Hyphen/Label Normal";
        }

        private void RefreshMaterial()
        {
            var mat = material;
            if (mat == null) return;

            if (_fontAtlas != null && _fontAtlas.Textures.Count > 0)
                mat.SetTexture("_MainTex", _fontAtlas.GetTexture(0));

            // All shaders use _TextColor. Outline/Glow also use _EffectColor.
            mat.SetColor("_TextColor", _textColor);

            if (_outlineEnabled)
                mat.SetColor("_EffectColor", _outlineColor);
            else if (_glowSize > 0)
                mat.SetColor("_EffectColor", _glowColor);
        }

#if UNITY_EDITOR
        private float _lastValidatedFontSize = -1;
        private bool _lastValidatedDF = false;
        private bool _lastValidatedOutlineEnabled = false;
        private float _lastValidatedOutline = -1;
        private int _lastValidatedGlow = -1;
        private TextAsset _lastValidatedFont = null;

        protected override void OnValidate()
        {
            base.OnValidate();
            _originalFontSize = _fontSize;

            bool fontChanged = _fontAsset != _lastValidatedFont ||
                              !Mathf.Approximately(_fontSize, _lastValidatedFontSize) ||
                              _useDistanceField != _lastValidatedDF ||
                              _outlineEnabled != _lastValidatedOutlineEnabled ||
                              _outlineSize != _lastValidatedOutline ||
                              _glowSize != _lastValidatedGlow;

            if (fontChanged)
            {
                _fontRegistered = false;
                _lastValidatedFont = _fontAsset;
                _lastValidatedFontSize = _fontSize;
                _lastValidatedDF = _useDistanceField;
                _lastValidatedOutlineEnabled = _outlineEnabled;
                _lastValidatedOutline = _outlineSize;
                _lastValidatedGlow = _glowSize;
            }

            _contentDirty = true;
            if (isActiveAndEnabled)
            {
                EnsureFontRegistered();
                RefreshMaterial();
                UpdateContent();
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }
#endif
    }
}
