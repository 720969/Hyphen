using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hyphen
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Hyphen Label")]
    [DisallowMultipleComponent]
    [Icon("HyphenLabelIcon")]
    public class HyphenLabel : MaskableGraphic, ILayoutElement
    {
        public struct LetterInfo
        {
            public char utf16Char;
            public bool valid;
            public float positionX;
            public float positionY;
            public int lineIndex;
            public float glyphU;
            public float glyphV;
            public float glyphWidth;
            public float glyphHeight;
            public int textureID;
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
        [SerializeField] private Color _shadowColor = new Color(0.431f, 0.431f, 0.431f, 1f);
        [SerializeField] private Vector2 _shadowOffset = new Vector2(2, -2);
        [SerializeField] private bool _outlineEnabled = false;
        [SerializeField] private float _outlineSize = 1f;
        [SerializeField] private Color _outlineColor = Color.red;
        [SerializeField] private int _glowSize = 0;
        [SerializeField] private Color _glowColor = Color.red;

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
        private Canvas _cachedRootCanvas;

        private List<LetterInfo> _lettersInfo = new List<LetterInfo>();
        private List<float> _linesWidth = new List<float>();
        private List<float> _linesOffsetX = new List<float>();
        private int[] _horizontalKernings;

        private HyphenLabelTextFormatter _formatter;
        private float _shrinkScale = 1f;

        internal const float BMFontScale = 1f;

        // --- Multi-page sub-renderer ---

        private class SubLabel
        {
            public GameObject gameObject;
            public CanvasRenderer canvasRenderer;
            public Mesh mesh;
        }

        private readonly List<SubLabel> _subLabels = new List<SubLabel>();
        private Material _sharedSubMaterial;

        // --- VertexHelper direct list access (cached reflection) ---

        private static System.Reflection.FieldInfo s_vhPositions;
        private static System.Reflection.FieldInfo s_vhColors;
        private static System.Reflection.FieldInfo s_vhUv0;
        private static System.Reflection.FieldInfo s_vhIndices;

        private static void InitVHFields()
        {
            if (s_vhPositions != null) return;
            var t = typeof(VertexHelper);
            var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            s_vhPositions = t.GetField("m_Positions", bf);
            s_vhColors = t.GetField("m_Colors", bf);
            s_vhUv0 = t.GetField("m_Uv0S", bf);
            s_vhIndices = t.GetField("m_Indices", bf);
        }

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

        // --- Internal accessors ---

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

        public virtual float preferredWidth
        {
            get { if (_contentDirty) UpdateContent(); return _overflow == Overflow.SHRINK ? 0f : _preferredWidth; }
        }
        public virtual float preferredHeight
        {
            get { if (_contentDirty) UpdateContent(); return _overflow == Overflow.SHRINK ? 0f : _preferredHeight; }
        }
        public virtual float minWidth => 0f;
        public virtual float minHeight => _overflow == Overflow.SHRINK ? 0f : _lineHeight;
        public virtual float flexibleWidth => -1f;
        public virtual float flexibleHeight => -1f;
        public virtual int layoutPriority => 1;

        public virtual void CalculateLayoutInputHorizontal() { if (_contentDirty) UpdateContent(); }
        public virtual void CalculateLayoutInputVertical() { if (_contentDirty) UpdateContent(); }

        internal void SetContentSize(Vector2 size)
        {
            _preferredWidth = size.x;
            _preferredHeight = size.y;
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
            if (_lettersInfo.Count <= letterIndex)
            {
                int needed = letterIndex + 1 - _lettersInfo.Count;
                for (int i = 0; i < needed; i++) _lettersInfo.Add(default);
            }

            var info = _lettersInfo[letterIndex];
            info.lineIndex = lineIndex;
            info.utf16Char = utf16Char;
            HyphenLetterDefinition def = default;
            info.valid = _fontAtlas != null &&
                _fontAtlas.HasLetterDefinition(utf16Char) &&
                _fontAtlas.GetLetterDefinitionForChar(utf16Char, out def);
            if (info.valid && def.width > 0 && def.height > 0)
            {
                info.glyphU = def.U;
                info.glyphV = def.V;
                info.glyphWidth = def.width;
                info.glyphHeight = def.height;
                info.textureID = def.textureID;
            }
            else
            {
                info.glyphWidth = 0;
                info.glyphHeight = 0;
                info.textureID = 0;
            }
            info.positionX = posX;
            info.positionY = posY;
            _lettersInfo[letterIndex] = info;
        }

        internal void RecordPlaceholderInfo(int letterIndex, char utf16Char)
        {
            if (_lettersInfo.Count <= letterIndex)
            {
                int needed = letterIndex + 1 - _lettersInfo.Count;
                for (int i = 0; i < needed; i++) _lettersInfo.Add(default);
            }
            var info = _lettersInfo[letterIndex];
            info.utf16Char = utf16Char;
            info.valid = false;
            info.positionX = 0;
            info.positionY = 0;
            info.lineIndex = 0;
            _lettersInfo[letterIndex] = info;
        }

        internal LetterInfo GetLetterInfo(int index)
        {
            return (index >= 0 && index < _lettersInfo.Count) ? _lettersInfo[index] : default;
        }

        // --- Unity lifecycle ---

        protected override void OnEnable()
        {
            base.OnEnable();
            _formatter = new HyphenLabelTextFormatter(this);
            var cr = GetComponent<CanvasRenderer>();
            if (cr != null) cr.cullTransparentMesh = false;
            _cachedRootCanvas = null;
            EnsureFontRegistered();
            RefreshMaterial();
            Canvas.willRenderCanvases += OnWillRenderCanvases;
        }

        private bool _rectChanged = false;

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            // Don't rebuild here — CanvasScaler hasn't updated scaleFactor yet.
            // Mark for deferred rebuild in willRenderCanvases (after CanvasScaler updates).
            _rectChanged = true;
            SyncSubLabelRects();
            _contentDirty = true;
            SetVerticesDirty();
            SetLayoutDirty();
        }

        private void OnWillRenderCanvases()
        {
            // RESIZE_HEIGHT
            if (_overflow == Overflow.RESIZE_HEIGHT && _textDesiredHeight > 0f)
            {
                if (!Mathf.Approximately(rectTransform.rect.height, _textDesiredHeight))
                {
                    var sd = rectTransform.sizeDelta;
                    sd.y = _textDesiredHeight;
                    rectTransform.sizeDelta = sd;
                }
            }

            // scaleFactor change detection (runs after CanvasScaler has updated)
            if (_rectChanged)
            {
                _rectChanged = false;
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    _cachedRootCanvas = canvas.rootCanvas;
                    float currentSf = Mathf.Max(1f, _cachedRootCanvas.scaleFactor);
                    currentSf = Mathf.Round(currentSf * 100f) / 100f;
                    if (!Mathf.Approximately(currentSf, _lastScaleFactor))
                    {
                        _lastScaleFactor = currentSf;
                        _fontRegistered = false;
                        _contentDirty = true;
                        SetVerticesDirty();
                    }
                }
            }
        }

        protected override void OnDisable()
        {
            Canvas.willRenderCanvases -= OnWillRenderCanvases;
            if (_fontAtlas != null)
            {
                HyphenFontAtlasCache.ReleaseFontAtlas(_fontAtlas);
                _fontAtlas = null;
            }
            _horizontalKernings = null;
            _cachedRootCanvas = null;
            CleanupSubLabels();
            DestroySharedSubMaterial();
#if UNITY_EDITOR
            _lastValidatedFontSize = -1;
            _lastValidatedDF = false;
            _lastValidatedOutlineEnabled = false;
            _lastValidatedOutline = -1;
            _lastValidatedGlow = -1;
            _lastValidatedFont = null;
#endif
            base.OnDisable();
        }

        protected override void UpdateGeometry()
        {
            if (_contentDirty) UpdateContent();
            base.UpdateGeometry();
        }

        // --- OnPopulateMesh ---

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_fontAtlas == null || string.IsNullOrEmpty(_text)) return;

            int pageCount = _fontAtlas.Textures.Count;
            if (pageCount <= 1)
            {
                PopulateVertices(vh);
                return;
            }

            // Multi-page: page 0 on self, pages 1+ on sub-labels
            CleanupOrphanedChildren();
            EnsureSubLabels(pageCount);
            SyncSubLabelProperties();

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
            float shrinkScale = _shrinkScale;
            float sf = _lastScaleFactor;
            float invTexW = 1f / HyphenFontAtlas.CacheTextureWidth;
            float invTexH = 1f / HyphenFontAtlas.CacheTextureHeight;
            float letterOffsetY = _letterOffsetY;
            bool hasShadow = _shadowEnabled;
            Color32 shadowCol = _shadowColor;
            Vector2 shadowOff = _shadowOffset;
            Color32 textCol = Color.white;

            var pageVerts = new List<List<Vector3>>(pageCount);
            var pageColors = new List<List<Color32>>(pageCount);
            var pageUVs = new List<List<Vector2>>(pageCount);
            for (int i = 0; i < pageCount; i++)
            {
                pageVerts.Add(new List<Vector3>());
                pageColors.Add(new List<Color32>());
                pageUVs.Add(new List<Vector2>());
            }

            int textLen = _text.Length;
            var letters = _lettersInfo;
            int letterCount = letters.Count;

            for (int i = 0; i < textLen; i++)
            {
                if (i >= letterCount) break;
                var info = letters[i];
                if (!info.valid || info.glyphWidth <= 0) continue;

                float lineOff = _linesOffsetX[info.lineIndex];
                float px = info.positionX + lineOff;
                float py = info.positionY + letterOffsetY;
                float sw = info.glyphWidth * shrinkScale;
                float sh = info.glyphHeight * shrinkScale;
                float left = px - pivotX * rectW;
                float right = left + sw;
                float top = py - pivotY * rectH;
                float bottom = top - sh;

                float uL = info.glyphU * invTexW;
                float uR = (info.glyphU + info.glyphWidth * sf) * invTexW;
                float uB = (info.glyphV + info.glyphHeight * sf) * invTexH;
                float uT = info.glyphV * invTexH;

                if (clamp)
                {
                    if (right <= minX || left >= maxX || top <= minY || bottom >= maxY) continue;
                    if (left < minX) { float t = (minX - left) / (right - left); uL = Mathf.Lerp(uL, uR, t); left = minX; }
                    if (right > maxX) { float t = (maxX - left) / (right - left); uR = Mathf.Lerp(uL, uR, t); right = maxX; }
                    if (bottom < minY) { float t = (minY - bottom) / (top - bottom); uB = Mathf.Lerp(uB, uT, t); bottom = minY; }
                    if (top > maxY) { float t = (maxY - bottom) / (top - bottom); uT = Mathf.Lerp(uB, uT, t); top = maxY; }
                }

                int page = info.textureID;
                var pv = pageVerts[page];
                var pc = pageColors[page];
                var pu = pageUVs[page];

                // Shadow quad
                if (hasShadow)
                {
                    float sLeft = left + shadowOff.x, sRight = right + shadowOff.x;
                    float sTop = top + shadowOff.y, sBottom = bottom + shadowOff.y;
                    pv.Add(new Vector3(sLeft, sBottom)); pc.Add(shadowCol); pu.Add(new Vector2(uL, uB));
                    pv.Add(new Vector3(sLeft, sTop)); pc.Add(shadowCol); pu.Add(new Vector2(uL, uT));
                    pv.Add(new Vector3(sRight, sTop)); pc.Add(shadowCol); pu.Add(new Vector2(uR, uT));
                    pv.Add(new Vector3(sRight, sBottom)); pc.Add(shadowCol); pu.Add(new Vector2(uR, uB));
                    int vs = pv.Count - 4;
                    var pi = pv.Count;
                    // Will add triangles after vert count is known
                }

                // Text quad
                {
                    int vs = pv.Count;
                    pv.Add(new Vector3(left, bottom)); pc.Add(textCol); pu.Add(new Vector2(uL, uB));
                    pv.Add(new Vector3(left, top)); pc.Add(textCol); pu.Add(new Vector2(uL, uT));
                    pv.Add(new Vector3(right, top)); pc.Add(textCol); pu.Add(new Vector2(uR, uT));
                    pv.Add(new Vector3(right, bottom)); pc.Add(textCol); pu.Add(new Vector2(uR, uB));
                }
            }

            // Add triangles for each page's quads
            for (int p = 0; p < pageCount; p++)
            {
                var pv = pageVerts[p];
                for (int i = 0; i < pv.Count; i += 4)
                {
                    // shadow + text quads are interleaved, each 4 verts
                    // We'll add triangles at the end for all quads in this page
                }
            }

            // Page 0 → VertexHelper
            var p0v = pageVerts[0];
            InitVHFields();
            if (s_vhPositions.GetValue(vh) == null)
            {
                vh.AddVert(Vector3.zero, Color.clear, Vector4.zero);
                vh.Clear();
            }
            var positions = (List<Vector3>)s_vhPositions.GetValue(vh);
            var colors = (List<Color32>)s_vhColors.GetValue(vh);
            var uvs = (List<Vector4>)s_vhUv0.GetValue(vh);
            var indices = (List<int>)s_vhIndices.GetValue(vh);
            positions.Clear(); colors.Clear(); uvs.Clear(); indices.Clear();

            for (int i = 0; i < p0v.Count; i++)
            {
                positions.Add(p0v[i]);
                colors.Add(pageColors[0][i]);
                uvs.Add(new Vector4(pageUVs[0][i].x, pageUVs[0][i].y, 0, 0));
            }
            for (int i = 0; i < p0v.Count; i += 4)
            {
                indices.Add(i); indices.Add(i + 1); indices.Add(i + 2);
                indices.Add(i); indices.Add(i + 2); indices.Add(i + 3);
            }

            // Pages 1+ → sub-labels
            for (int p = 1; p < pageCount; p++)
            {
                var pv = pageVerts[p];
                if (pv.Count == 0) continue;
                int subIdx = p - 1;
                if (subIdx >= _subLabels.Count) continue;
                var sub = _subLabels[subIdx];
                sub.mesh.Clear();
                sub.mesh.vertices = pv.ToArray();
                sub.mesh.colors32 = pageColors[p].ToArray();
                sub.mesh.uv = pageUVs[p].ToArray();
                int triCount = pv.Count / 4;
                var tris = new int[triCount * 6];
                for (int i = 0, t = 0; i < pv.Count; i += 4, t += 6)
                {
                    tris[t] = i; tris[t + 1] = i + 1; tris[t + 2] = i + 2;
                    tris[t + 3] = i; tris[t + 4] = i + 2; tris[t + 5] = i + 3;
                }
                sub.mesh.triangles = tris;
                sub.canvasRenderer.SetMesh(sub.mesh);
            }
        }

        private void PopulateVertices(VertexHelper vh)
        {
            int textLen = _text.Length;
            if (textLen == 0) return;

            var rt = rectTransform;
            float pivotXRectW = rt.pivot.x * rt.rect.width;
            float pivotYRectH = rt.pivot.y * rt.rect.height;
            float minX = -pivotXRectW;
            float maxX = rt.rect.width - pivotXRectW;
            float minY = -pivotYRectH;
            float maxY = rt.rect.height - pivotYRectH;
            bool clamp = _overflow == Overflow.CLAMP;
            float shrinkScale = _shrinkScale;
            float sf = _lastScaleFactor;
            float invTexW = 1f / HyphenFontAtlas.CacheTextureWidth;
            float invTexH = 1f / HyphenFontAtlas.CacheTextureHeight;
            float letterOffsetY = _letterOffsetY;
            bool hasShadow = _shadowEnabled;
            Color32 shadowCol = _shadowColor;
            Vector2 shadowOff = _shadowOffset;
            Color32 textCol = Color.white;
            var letters = _lettersInfo;
            int letterCount = letters.Count;

            InitVHFields();
            if (s_vhPositions.GetValue(vh) == null)
            {
                vh.AddVert(Vector3.zero, Color.clear, Vector4.zero);
                vh.Clear();
            }
            var positions = (List<Vector3>)s_vhPositions.GetValue(vh);
            var colors = (List<Color32>)s_vhColors.GetValue(vh);
            var uvs = (List<Vector4>)s_vhUv0.GetValue(vh);
            var indices = (List<int>)s_vhIndices.GetValue(vh);
            positions.Clear(); colors.Clear(); uvs.Clear(); indices.Clear();

            for (int i = 0; i < textLen; i++)
            {
                if (i >= letterCount) break;
                var info = letters[i];
                if (!info.valid || info.glyphWidth <= 0) continue;

                float lineOff = _linesOffsetX[info.lineIndex];
                float px = info.positionX + lineOff;
                float py = info.positionY + letterOffsetY;
                float sw = info.glyphWidth * shrinkScale;
                float sh = info.glyphHeight * shrinkScale;
                float left = px - pivotXRectW;
                float right = left + sw;
                float top = py - pivotYRectH;
                float bottom = top - sh;

                float uL = info.glyphU * invTexW;
                float uR = (info.glyphU + info.glyphWidth * sf) * invTexW;
                float uB = (info.glyphV + info.glyphHeight * sf) * invTexH;
                float uT = info.glyphV * invTexH;

                if (clamp)
                {
                    if (right <= minX || left >= maxX || top <= minY || bottom >= maxY) continue;
                    if (left < minX) { float t = (minX - left) / (right - left); uL = Mathf.Lerp(uL, uR, t); left = minX; }
                    if (right > maxX) { float t = (maxX - left) / (right - left); uR = Mathf.Lerp(uL, uR, t); right = maxX; }
                    if (bottom < minY) { float t = (minY - bottom) / (top - bottom); uB = Mathf.Lerp(uB, uT, t); bottom = minY; }
                    if (top > maxY) { float t = (maxY - bottom) / (top - bottom); uT = Mathf.Lerp(uB, uT, t); top = maxY; }
                }

                if (hasShadow)
                {
                    float sLeft = left + shadowOff.x, sRight = right + shadowOff.x;
                    float sTop = top + shadowOff.y, sBottom = bottom + shadowOff.y;
                    int vs = positions.Count;
                    positions.Add(new Vector3(sLeft, sBottom)); colors.Add(shadowCol); uvs.Add(new Vector4(uL, uB, 0, 0));
                    positions.Add(new Vector3(sLeft, sTop)); colors.Add(shadowCol); uvs.Add(new Vector4(uL, uT, 0, 0));
                    positions.Add(new Vector3(sRight, sTop)); colors.Add(shadowCol); uvs.Add(new Vector4(uR, uT, 0, 0));
                    positions.Add(new Vector3(sRight, sBottom)); colors.Add(shadowCol); uvs.Add(new Vector4(uR, uB, 0, 0));
                    indices.Add(vs); indices.Add(vs + 1); indices.Add(vs + 2);
                    indices.Add(vs); indices.Add(vs + 2); indices.Add(vs + 3);
                }

                {
                    int vs = positions.Count;
                    positions.Add(new Vector3(left, bottom)); colors.Add(textCol); uvs.Add(new Vector4(uL, uB, 0, 0));
                    positions.Add(new Vector3(left, top)); colors.Add(textCol); uvs.Add(new Vector4(uL, uT, 0, 0));
                    positions.Add(new Vector3(right, top)); colors.Add(textCol); uvs.Add(new Vector4(uR, uT, 0, 0));
                    positions.Add(new Vector3(right, bottom)); colors.Add(textCol); uvs.Add(new Vector4(uR, uB, 0, 0));
                    indices.Add(vs); indices.Add(vs + 1); indices.Add(vs + 2);
                    indices.Add(vs); indices.Add(vs + 2); indices.Add(vs + 3);
                }
            }
        }

        // --- Multi-page sub-renderer management ---

        private void EnsureSubLabels(int pageCount)
        {
            int needed = pageCount - 1;
            while (_subLabels.Count > needed)
            {
                var sub = _subLabels[_subLabels.Count - 1];
                _subLabels.RemoveAt(_subLabels.Count - 1);
                DestroySubLabel(sub);
            }
            while (_subLabels.Count < needed)
                _subLabels.Add(CreateSubLabel(_subLabels.Count + 1));
        }

        private SubLabel CreateSubLabel(int pageIndex)
        {
            var go = new GameObject($"Hyphen Page {pageIndex}", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;
            go.hideFlags = HideFlags.HideInHierarchy;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = rectTransform.pivot;

            var cr = go.GetComponent<CanvasRenderer>();
            cr.cullTransparentMesh = false;
            cr.materialCount = 1;

            EnsureSharedSubMaterial();
            cr.SetMaterial(_sharedSubMaterial, 0);

            return new SubLabel
            {
                gameObject = go,
                canvasRenderer = cr,
                mesh = new Mesh()
            };
        }

        private void EnsureSharedSubMaterial()
        {
            if (_sharedSubMaterial != null && _sharedSubMaterial.shader.name == GetShaderName()) return;
            DestroySharedSubMaterial();
            var shader = Shader.Find(GetShaderName()) ?? Shader.Find("UI/Default");
            _sharedSubMaterial = new Material(shader);
        }

        private void DestroySharedSubMaterial()
        {
            if (_sharedSubMaterial == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(_sharedSubMaterial);
            else
#endif
                Destroy(_sharedSubMaterial);
            _sharedSubMaterial = null;
        }

        private void DestroySubLabel(SubLabel sub)
        {
            if (sub.gameObject != null)
            {
                sub.gameObject.SetActive(false);
                sub.gameObject.transform.SetParent(null, false);
            }
            if (sub.canvasRenderer != null) sub.canvasRenderer.SetMesh(null);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (sub.mesh != null) { DestroyImmediate(sub.mesh); sub.mesh = null; }
                if (sub.gameObject != null)
                {
                    var goRef = sub.gameObject;
                    sub.gameObject = null;
                    UnityEditor.EditorApplication.delayCall += () => { if (goRef != null) DestroyImmediate(goRef); };
                }
            }
            else
#endif
            {
                if (sub.mesh != null) { Destroy(sub.mesh); sub.mesh = null; }
                if (sub.gameObject != null) { Destroy(sub.gameObject); sub.gameObject = null; }
            }
        }

        private void CleanupSubLabels()
        {
            foreach (var sub in _subLabels) DestroySubLabel(sub);
            _subLabels.Clear();
        }

        private void CleanupOrphanedChildren()
        {
            var tracked = new HashSet<GameObject>();
            foreach (var sub in _subLabels)
                if (sub.gameObject != null) tracked.Add(sub.gameObject);

            var toRemove = new List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!tracked.Contains(child.gameObject) && child.name.StartsWith("Hyphen Page"))
                    toRemove.Add(child);
            }
            foreach (var child in toRemove)
            {
                child.SetParent(null, false);
                child.gameObject.SetActive(false);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.EditorApplication.delayCall += () => { if (child != null) DestroyImmediate(child.gameObject); };
                else
                    Destroy(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }
        }

        private void SyncSubLabelRects()
        {
            var parentRT = rectTransform;
            for (int i = 0; i < _subLabels.Count; i++)
            {
                var sub = _subLabels[i];
                if (sub.gameObject == null) continue;
                var rt = sub.gameObject.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.pivot = parentRT.pivot;
            }
        }

        private void SyncSubLabelProperties()
        {
            EnsureSharedSubMaterial();
            if (_fontAtlas == null) return;
            // Use MaterialPropertyBlock to set per-page texture without creating per-page Material
            for (int i = 0; i < _subLabels.Count; i++)
            {
                var sub = _subLabels[i];
                if (sub.canvasRenderer == null) continue;
                int pageIdx = i + 1;

                // CanvasRenderer doesn't support MaterialPropertyBlock directly.
                // Use SetMaterial with shared material + texture override via SetTexture.
                sub.canvasRenderer.SetMaterial(_sharedSubMaterial, 0);
                if (pageIdx < _fontAtlas.Textures.Count)
                    sub.canvasRenderer.SetTexture(_fontAtlas.GetTexture(pageIdx));
            }

            // Sync uniforms on shared material
            _sharedSubMaterial.SetColor("_TextColor", _textColor);
            if (_outlineEnabled)
                _sharedSubMaterial.SetColor("_EffectColor", _outlineColor);
            else if (_glowSize > 0)
                _sharedSubMaterial.SetColor("_EffectColor", _glowColor);
        }

        // --- Content update ---

        private void UpdateContent()
        {
            if (_formatter == null) _formatter = new HyphenLabelTextFormatter(this);
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
            if (_lettersInfo.Capacity < _text.Length) _lettersInfo.Capacity = _text.Length;
            for (int i = 0; i < _text.Length; i++) _lettersInfo.Add(default);

            switch (_wrapMode)
            {
                case WrapMode.WORD: _formatter.MultilineTextWrapByWord(); break;
                case WrapMode.CHAR: _formatter.MultilineTextWrapByChar(); break;
                default: _formatter.MultilineTextWrapByChar(); break;
            }

            _formatter.ComputeAlignmentOffset();
            _shrinkScale = 1f;

            if (_overflow == Overflow.SHRINK &&
                (_formatter.IsVerticalClamp() || _formatter.IsHorizontalClamp()))
            {
                float origLineHeight = _lineHeight;
                var snapshot = _fontAtlas.SnapshotLetterDefinitions();

                float lo = 0.05f, hi = 1f, best = 0.05f;
                for (int i = 0; i < 20; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    ApplyShrinkScale(snapshot, mid, origLineHeight);
                    if (_formatter.IsVerticalClamp() || _formatter.IsHorizontalClamp())
                        hi = mid;
                    else { best = mid; lo = mid; }
                }

                ApplyShrinkScale(snapshot, best, origLineHeight);
                _shrinkScale = best;
                _fontAtlas.RestoreLetterDefinitions(snapshot);
                _lineHeight = origLineHeight;
            }
        }

        private void ApplyShrinkScale(
            Dictionary<char, HyphenLetterDefinition> snapshot, float scale, float origLineHeight)
        {
            _fontAtlas.RestoreLetterDefinitions(snapshot);
            _fontAtlas.ScaleFontLetterDefinition(scale);
            _lineHeight = origLineHeight * scale;

            _linesWidth.Clear();
            _lettersInfo.Clear();
            if (_lettersInfo.Capacity < _text.Length) _lettersInfo.Capacity = _text.Length;
            for (int i = 0; i < _text.Length; i++) _lettersInfo.Add(default);

            switch (_wrapMode)
            {
                case WrapMode.WORD: _formatter.MultilineTextWrapByWord(); break;
                case WrapMode.CHAR: _formatter.MultilineTextWrapByChar(); break;
                default: _formatter.MultilineTextWrapByChar(); break;
            }
            _formatter.ComputeAlignmentOffset();
        }

        private void ComputeHorizontalKernings(string text)
        {
            if (_fontAtlas?.Font == null || string.IsNullOrEmpty(text))
            {
                _horizontalKernings = Array.Empty<int>();
                return;
            }
            var kernings = _fontAtlas.Font.GetHorizontalKerningForText(text, out _);
            _horizontalKernings = kernings ?? Array.Empty<int>();
        }

        // --- Font registration ---

        private void EnsureFontRegistered()
        {
            if (_fontRegistered && _fontAtlas != null && HyphenFontAtlasCache.HasFonts) return;
            if (_fontAsset == null || _fontAsset.bytes == null || _fontAsset.bytes.Length == 0) return;

            if (_fontAtlas != null)
            {
                HyphenFontAtlasCache.ReleaseFontAtlas(_fontAtlas);
                _fontAtlas = null;
            }

            string fontName = _fontAsset.name;
            float effectiveOutline = _outlineEnabled ? _outlineSize : 0f;
            bool useDF = _useDistanceField;
            if (effectiveOutline > 0) useDF = false;

            float scaleFactor = 1f;
            var canvas = canvasRenderer != null ? GetComponentInParent<Canvas>() : null;
            if (canvas != null)
            {
                _cachedRootCanvas = canvas.rootCanvas;
                scaleFactor = Mathf.Max(1f, _cachedRootCanvas.scaleFactor);
                _lastScaleFactor = Mathf.Round(scaleFactor * 100f) / 100f;
            }

            CleanupSubLabels();

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
                    if (_instanceMaterial != null)
                    {
#if UNITY_EDITOR
                        if (!Application.isPlaying) DestroyImmediate(_instanceMaterial);
                        else
#endif
                            Destroy(_instanceMaterial);
                        _instanceMaterial = null;
                    }
                    var shader = Shader.Find(shaderName) ?? Shader.Find("UI/Default");
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
            mat.SetColor("_TextColor", _textColor);
            if (_outlineEnabled) mat.SetColor("_EffectColor", _outlineColor);
            else if (_glowSize > 0) mat.SetColor("_EffectColor", _glowColor);
            SyncSubLabelProperties();
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
                CleanupSubLabels();
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
