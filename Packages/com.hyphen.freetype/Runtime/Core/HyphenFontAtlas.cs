using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hyphen
{
    /// <summary>
    /// Dynamic glyph atlas — renders glyphs on demand into texture pages.
    /// Port of cocos2dx CCFontAtlas.
    /// Outline mode uses RGBA32 (R=outline, A=font) to match cocos2dx AI88 (luminance=outline, alpha=font).
    /// Normal/DF mode uses Alpha8 (A=font).
    /// </summary>
    public sealed class HyphenFontAtlas
    {
        public const int CacheTextureWidth = 2048;
        public const int CacheTextureHeight = 2048;

        private readonly HyphenFontFreeType _font;
        private readonly Dictionary<char, HyphenLetterDefinition> _letterDefinitions;
        private readonly List<Texture2D> _atlasTextures;
        private readonly List<byte[]> _pageData;

        private int _currentPage;
        private float _currentPageOrigX;
        private float _currentPageOrigY;
        private int _letterPadding;
        private int _letterEdgeExtend;
        private int _fontAscender;
        private int _currLineHeight;
        private float _lineHeight;
        private readonly bool _hasOutline;
        private float _scaleFactor = 1f;

        public float LineHeight => _lineHeight;
        public IReadOnlyList<Texture2D> Textures => _atlasTextures;
        public HyphenFontFreeType Font => _font;
        public int CurrentPage => _currentPage;

        public void SetScaleFactor(float sf) => _scaleFactor = Mathf.Max(1f, sf);

        public HyphenFontAtlas(HyphenFontFreeType font)
        {
            _font = font;
            _letterDefinitions = new Dictionary<char, HyphenLetterDefinition>();
            _atlasTextures = new List<Texture2D>();
            _pageData = new List<byte[]>();

            _lineHeight = _font.GetFontMaxHeight();
            _fontAscender = _font.GetFontAscender();
            _currentPage = 0;
            _currentPageOrigX = 0;
            _currentPageOrigY = 0;
            _letterEdgeExtend = 2;
            _letterPadding = 0;
            _currLineHeight = 0;

            float outlineSize = _font.OutlineSize;
            _hasOutline = outlineSize > 0;

            if (_font.IsDistanceFieldEnabled)
            {
                _letterPadding += 2 * HyphenFontFreeType.DistanceMapSpread;
            }

            // Match cocos2dx: outline expands glyph bounding box, so lineHeight must grow
            if (_hasOutline)
            {
                _lineHeight += 2 * outlineSize;
            }

            // Create first page
            CreateNewPage();
        }

        private void CreateNewPage()
        {
            int bpp = _hasOutline ? 4 : 1; // RGBA32=4, Alpha8=1
            int pageSize = CacheTextureWidth * CacheTextureHeight * bpp;

            var pageBytes = new byte[pageSize];
            Array.Clear(pageBytes, 0, pageSize);
            _pageData.Add(pageBytes);

            var format = _hasOutline ? TextureFormat.RGBA32 : TextureFormat.Alpha8;
            var texture = new Texture2D(CacheTextureWidth, CacheTextureHeight, format, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            _atlasTextures.Add(texture);
        }

        public void AddLetterDefinition(char utf16Char, HyphenLetterDefinition letterDefinition)
        {
            _letterDefinitions[utf16Char] = letterDefinition;
        }

        public bool GetLetterDefinitionForChar(char utf16Char, out HyphenLetterDefinition letterDefinition)
        {
            if (_letterDefinitions.TryGetValue(utf16Char, out letterDefinition))
            {
                return letterDefinition.validDefinition;
            }
            return false;
        }

        public HyphenLetterDefinition GetLetterDefinition(char utf16Char)
        {
            return _letterDefinitions.TryGetValue(utf16Char, out var def) ? def : default;
        }

        public bool HasLetterDefinition(char utf16Char)
        {
            return _letterDefinitions.ContainsKey(utf16Char);
        }

        public bool PrepareLetterDefinitions(string text)
        {
            if (_font == null || string.IsNullOrEmpty(text))
                return false;

            var newChars = new List<char>();
            foreach (char c in text)
            {
                if (!_letterDefinitions.ContainsKey(c))
                    newChars.Add(c);
            }

            if (newChars.Count == 0)
                return false;

            int adjustForDistanceMap = _letterPadding / 2;
            int adjustForExtend = _letterEdgeExtend / 2;

            float startY = _currentPageOrigY;

            foreach (char c in newChars)
            {
                var glyphBitmap = _font.GetGlyphBitmap((ushort)c);

                HyphenLetterDefinition tempDef;

                if (glyphBitmap.bitmap != null && glyphBitmap.bitmapWidth > 0 && glyphBitmap.bitmapHeight > 0)
                {
                    tempDef.validDefinition = true;
                    tempDef.width = glyphBitmap.width + _letterPadding + _letterEdgeExtend;
                    tempDef.height = glyphBitmap.height + _letterPadding + _letterEdgeExtend;
                    tempDef.offsetX = glyphBitmap.offsetX + adjustForDistanceMap + adjustForExtend;
                    tempDef.offsetY = _fontAscender + glyphBitmap.offsetY - adjustForDistanceMap - adjustForExtend;

                    if (glyphBitmap.bitmapHeight > _currLineHeight)
                    {
                        _currLineHeight = (int)glyphBitmap.bitmapHeight + _letterPadding + _letterEdgeExtend + 1;
                    }

                    if (_currentPageOrigX + tempDef.width > CacheTextureWidth)
                    {
                        _currentPageOrigY += _currLineHeight;
                        _currLineHeight = 0;
                        _currentPageOrigX = 0;

                        if (_currentPageOrigY + _lineHeight >= CacheTextureHeight)
                        {
                            UploadPageData(_currentPage, (int)startY, CacheTextureHeight - (int)startY);

                            startY = 0;
                            _currentPageOrigY = 0;
                            _currentPage++;
                            CreateNewPage();
                            Debug.LogWarning($"[Hyphen] Atlas overflowed to page {_currentPage}. Consider using a smaller font size or fewer characters.");
                        }
                    }

                    byte[] currentPage = _pageData[_currentPage];
                    bool isDistanceField = _font.IsDistanceFieldEnabled;

                    if (isDistanceField)
                    {
                        byte[] distMap = HyphenFontFreeType.MakeDistanceMap(
                            glyphBitmap.bitmap, glyphBitmap.bitmapWidth, glyphBitmap.bitmapHeight);

                        int dfWidth = glyphBitmap.bitmapWidth + 2 * HyphenFontFreeType.DistanceMapSpread;
                        int dfHeight = glyphBitmap.bitmapHeight + 2 * HyphenFontFreeType.DistanceMapSpread;

                        _font.RenderCharAt(currentPage, CacheTextureWidth,
                            (int)(_currentPageOrigX + adjustForExtend),
                            (int)(_currentPageOrigY + adjustForExtend),
                            distMap, dfWidth, dfHeight, false);
                    }
                    else
                    {
                        _font.RenderCharAt(currentPage, CacheTextureWidth,
                            (int)(_currentPageOrigX + adjustForExtend),
                            (int)(_currentPageOrigY + adjustForExtend),
                            glyphBitmap.bitmap, glyphBitmap.bitmapWidth, glyphBitmap.bitmapHeight,
                            _hasOutline);
                    }

                    tempDef.U = _currentPageOrigX;
                    tempDef.V = _currentPageOrigY;
                    tempDef.textureID = _currentPage;
                    _currentPageOrigX += tempDef.width + 1;

                    // Convert from render pixels to layout points (divide by scaleFactor)
                    // This matches cocos2dx: tempDef.width /= scaleFactor; etc.
                    if (_scaleFactor != 1f)
                    {
                        tempDef.width /= _scaleFactor;
                        tempDef.height /= _scaleFactor;
                        tempDef.offsetX /= _scaleFactor;
                        tempDef.offsetY /= _scaleFactor;
                        tempDef.U /= _scaleFactor;
                        tempDef.V /= _scaleFactor;
                    }
                }
                else
                {
                    tempDef.validDefinition = glyphBitmap.xAdvance > 0;
                    tempDef.width = 0;
                    tempDef.height = 0;
                    tempDef.U = 0;
                    tempDef.V = 0;
                    tempDef.offsetX = 0;
                    tempDef.offsetY = 0;
                    tempDef.textureID = 0;
                    tempDef.xAdvance = glyphBitmap.xAdvance;
                    _currentPageOrigX += 1;
                }

                tempDef.xAdvance = _scaleFactor != 1f ? (int)(glyphBitmap.xAdvance / _scaleFactor) : glyphBitmap.xAdvance;
                _letterDefinitions[c] = tempDef;
            }

            int uploadHeight = (int)(_currentPageOrigY - startY + _lineHeight);
            if (uploadHeight > 0)
            {
                UploadPageData(_currentPage, (int)startY, Math.Min(uploadHeight, CacheTextureHeight - (int)startY));
            }

            return true;
        }

        private void UploadPageData(int page, int startY, int height)
        {
            if (page < 0 || page >= _atlasTextures.Count) return;
            if (startY < 0 || height <= 0) return;

            var texture = _atlasTextures[page];
            var pageBytes = _pageData[page];

            if (_hasOutline)
            {
                // RGBA32: SetPixels32 for reliable cross-platform upload
                // (LoadRawTextureData fails silently on RGBA32 on some platforms)
                int pixelCount = CacheTextureWidth * CacheTextureHeight;
                var pixels = new Color32[pixelCount];
                for (int i = 0; i < pixelCount; i++)
                {
                    int bi = i * 4;
                    pixels[i] = new Color32(pageBytes[bi], pageBytes[bi + 1], pageBytes[bi + 2], pageBytes[bi + 3]);
                }
                texture.SetPixels32(pixels);
                texture.Apply(false);
            }
            else
            {
                // Alpha8: LoadRawTextureData works correctly
                texture.LoadRawTextureData(pageBytes);
                texture.Apply(false);
            }
        }

        public void ScaleFontLetterDefinition(float scaleFactor)
        {
            var keys = new List<char>(_letterDefinitions.Keys);
            foreach (var key in keys)
            {
                var def = _letterDefinitions[key];
                def.width *= scaleFactor;
                def.height *= scaleFactor;
                def.offsetX *= scaleFactor;
                def.offsetY *= scaleFactor;
                def.xAdvance = (int)(def.xAdvance * scaleFactor);
                _letterDefinitions[key] = def;
            }
        }

        public Dictionary<char, HyphenLetterDefinition> SnapshotLetterDefinitions()
        {
            return new Dictionary<char, HyphenLetterDefinition>(_letterDefinitions);
        }

        public void RestoreLetterDefinitions(Dictionary<char, HyphenLetterDefinition> snapshot)
        {
            _letterDefinitions.Clear();
            foreach (var kvp in snapshot)
                _letterDefinitions[kvp.Key] = kvp.Value;
        }

        public Texture2D GetTexture(int slot)
        {
            if (slot >= 0 && slot < _atlasTextures.Count)
                return _atlasTextures[slot];
            return null;
        }

        public void SetLineHeight(float newHeight)
        {
            _lineHeight = newHeight;
        }
    }
}
