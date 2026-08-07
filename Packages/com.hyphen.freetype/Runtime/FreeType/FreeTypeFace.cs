using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Hyphen.FreeType
{
    /// <summary>
    /// Manages a FreeType face (font file + size).
    /// Port of CCFontFreeType — font loading, glyph rendering, kerning, outline.
    /// </summary>
    public sealed class FreeTypeFace : IDisposable
    {
        private IntPtr _face;
        private readonly byte[] _fontData;
        private readonly string _fontName;
        private readonly float _fontSize;
        private readonly float _outlineSize;
        private readonly bool _distanceFieldEnabled;
        private IntPtr _stroker;
        private bool _disposed;

        // Font data cache with ref counting (mirrors cocos2dx s_cacheFontData)
        private static readonly Dictionary<string, CachedFontData> s_cache = new Dictionary<string, CachedFontData>();

        private struct CachedFontData
        {
            public byte[] data;
            public int refCount;
        }

        public IntPtr Handle => _face;
        public bool IsValid => _face != IntPtr.Zero;
        public float FontSize => _fontSize;
        public bool IsDistanceFieldEnabled => _distanceFieldEnabled;
        public float OutlineSize => _outlineSize;
        public IntPtr Stroker => _stroker;

        /// <summary>
        /// Creates a FreeType face from a font file.
        /// Port of FontFreeType::create + createFontObject.
        /// </summary>
        public static FreeTypeFace Create(string fontName, byte[] fontData, float fontSize,
            bool distanceFieldEnabled = false, float outlineSize = 0)
        {
            if (fontData == null || fontData.Length == 0)
            {
                Debug.LogError($"[Hyphen] Font data is null for '{fontName}'");
                return null;
            }

            var face = new FreeTypeFace(fontName, fontData, fontSize, distanceFieldEnabled, outlineSize);
            if (!face.IsValid)
            {
                face.Dispose();
                return null;
            }
            return face;
        }

        private FreeTypeFace(string fontName, byte[] fontData, float fontSize,
            bool distanceFieldEnabled, float outlineSize)
        {
            _fontName = fontName;
            _fontSize = fontSize;
            _distanceFieldEnabled = distanceFieldEnabled;
            _outlineSize = outlineSize;

            // Cache font data with ref counting (same as cocos2dx s_cacheFontData)
            if (s_cache.TryGetValue(fontName, out var cached))
            {
                cached.refCount++;
                s_cache[fontName] = cached;
                _fontData = cached.data;
            }
            else
            {
                _fontData = fontData;
                s_cache[fontName] = new CachedFontData { data = fontData, refCount = 1 };
            }

            var library = FreeTypeLibrary.Instance;

            int err = FreeTypeNative.FT_New_Memory_Face(library.Handle, _fontData, _fontData.Length, 0, out _face);
            if (err != 0)
            {
                Debug.LogError($"[Hyphen] FT_New_Memory_Face failed for '{fontName}' with error {err}");
                _face = IntPtr.Zero;
                return;
            }

            // Select Unicode charmap (fall back to first available)
            err = FreeTypeNative.FT_Select_Charmap(_face, FreeTypeNative.FT_ENCODING_UNICODE);
            if (err != 0)
            {
                // Try to find any non-None charmap
                Debug.LogWarning($"[Hyphen] FT_Select_Charmap(Unicode) failed for '{fontName}', using default");
            }

            // Set char size: fontSize * 64 (26.6 fixed point), 72 DPI
            int fontSizePoints = (int)(64f * fontSize);
            err = FreeTypeNative.FT_Set_Char_Size(_face, fontSizePoints, fontSizePoints, 72, 72);
            if (err != 0)
            {
                Debug.LogError($"[Hyphen] FT_Set_Char_Size failed for '{fontName}' size {fontSize} with error {err}");
                FreeTypeNative.FT_Done_Face(_face);
                _face = IntPtr.Zero;
                return;
            }

            // Create stroker if outline size > 0
            if (outlineSize > 0)
            {
                err = FreeTypeNative.FT_Stroker_New(library.Handle, out _stroker);
                if (err != 0)
                {
                    Debug.LogError($"[Hyphen] FT_Stroker_New failed with error {err}");
                }
                else
                {
                    long radius = (long)(outlineSize * 64f);
                    FreeTypeNative.FT_Stroker_Set(_stroker, radius,
                        FreeTypeNative.FT_STROKER_LINECAP_ROUND,
                        FreeTypeNative.FT_STROKER_LINEJOIN_ROUND,
                        0);
                }
            }
        }

        /// <summary>
        /// Gets the font ascender (baseline-to-top) in pixels.
        /// Port of FontFreeType::getFontAscender().
        /// </summary>
        public int GetFontAscender()
        {
            if (!IsValid) return 0;
            return FreeTypeNative.hyphen_get_ascender(_face);
        }

        /// <summary>
        /// Gets the font line height (metrics.height) in pixels.
        /// Port of FontFreeType: _lineHeight = size->metrics.height >> 6.
        /// </summary>
        public int GetFontLineHeight()
        {
            if (!IsValid) return 0;
            return FreeTypeNative.hyphen_get_line_height(_face);
        }

        /// <summary>
        /// Gets the horizontal kerning between two characters.
        /// Port of FontFreeType::getHorizontalKerningForChars.
        /// </summary>
        public int GetHorizontalKerning(ushort firstChar, ushort secondChar)
        {
            if (!IsValid) return 0;

            uint glyphIndex1 = FreeTypeNative.FT_Get_Char_Index(_face, firstChar);
            if (glyphIndex1 == 0) return 0;

            uint glyphIndex2 = FreeTypeNative.FT_Get_Char_Index(_face, secondChar);
            if (glyphIndex2 == 0) return 0;

            int err = FreeTypeNative.FT_Get_Kerning(_face, glyphIndex1, glyphIndex2,
                FreeTypeNative.FT_KERNING_DEFAULT, out FT_Vector kerning);
            if (err != 0) return 0;

            return (kerning.x >> 6);
        }

        /// <summary>
        /// Gets kerning array for a UTF-16 string.
        /// Port of FontFreeType::getHorizontalKerningForTextUTF16.
        /// </summary>
        public int[] GetHorizontalKerningForText(string text)
        {
            if (!IsValid || string.IsNullOrEmpty(text))
                return null;

            int len = text.Length;
            int[] sizes = new int[len];

            for (int c = 1; c < len; c++)
            {
                sizes[c] = GetHorizontalKerning((ushort)text[c - 1], (ushort)text[c]);
            }

            return sizes;
        }

        /// <summary>
        /// Renders a glyph and returns its bitmap and metrics.
        /// Port of FontFreeType::getGlyphBitmap.
        /// Returns the bitmap buffer pointer, dimensions, and advance.
        /// </summary>
        public GlyphBitmap GetGlyphBitmap(ushort theChar)
        {
            if (!IsValid)
                return default;

            int loadFlags = _distanceFieldEnabled
                ? FreeTypeNative.FT_LOAD_RENDER | FreeTypeNative.FT_LOAD_NO_HINTING | FreeTypeNative.FT_LOAD_NO_AUTOHINT
                : FreeTypeNative.FT_LOAD_RENDER | FreeTypeNative.FT_LOAD_NO_AUTOHINT;

            int err = FreeTypeNative.FT_Load_Char(_face, theChar, loadFlags);
            if (err != 0)
            {
                Debug.LogWarning($"[Hyphen] FT_Load_Char failed for char {(char)theChar} (U+{theChar:X4}) with error {err}");
                return default;
            }

            // Read glyph metrics via native helper (avoids struct marshalling issues)
            FreeTypeNative.hyphen_get_glyph_metrics(_face,
                out int mWidth, out int mHeight,
                out int mBearingX, out int mBearingY,
                out int mAdvance);

            GlyphBitmap result = default;
            result.offsetX = (int)(mBearingX >> 6);
            result.offsetY = -(int)(mBearingY >> 6);
            result.width = (int)(mWidth >> 6);
            result.height = (int)(mHeight >> 6);
            result.xAdvance = (int)(mAdvance >> 6);

            // Read bitmap info via native helper
            FreeTypeNative.hyphen_get_bitmap_info(_face,
                out int bmpWidth, out int bmpRows, out IntPtr bmpBuffer);

            result.bitmapWidth = bmpWidth;
            result.bitmapHeight = bmpRows;

            // Copy bitmap data
            if (result.bitmapWidth > 0 && result.bitmapHeight > 0 && bmpBuffer != IntPtr.Zero)
            {
                int bufSize = result.bitmapWidth * result.bitmapHeight;
                result.bitmap = new byte[bufSize];
                Marshal.Copy(bmpBuffer, result.bitmap, 0, bufSize);
            }
            else
            {
                result.bitmap = null;
            }

            // Handle outline: blend outline bitmap + glyph bitmap into dual-channel
            if (_outlineSize > 0 && result.bitmap != null)
            {
                result = GetGlyphBitmapWithOutline(theChar, result);
            }

            return result;
        }

        private GlyphBitmap GetGlyphBitmapWithOutline(ushort theChar, GlyphBitmap glyphBitmap)
        {
            // Load glyph without rendering (for outline extraction)
            int err = FreeTypeNative.FT_Load_Char(_face, theChar, FreeTypeNative.FT_LOAD_NO_BITMAP);
            if (err != 0) return glyphBitmap;

            var library = FreeTypeLibrary.Instance;

            // Use native helper to render outline bitmap (avoids struct marshalling issues)
            err = FreeTypeNative.hyphen_render_outline_bitmap(
                library.Handle, _face, _stroker,
                out int outWidth, out int outRows, out IntPtr outBuffer,
                out int bboxXMin, out int bboxYMin,
                out int bboxXMax, out int bboxYMax);

            if (err != 0 || outBuffer == IntPtr.Zero || outWidth <= 0 || outRows <= 0)
            {
                if (outBuffer != IntPtr.Zero) FreeTypeNative.hyphen_free(outBuffer);
                return glyphBitmap;
            }

            // Copy outline bitmap from native buffer
            long outlineWidth = outWidth;
            long outlineHeight = outRows;
            byte[] outlineBuffer = new byte[outlineWidth * outlineHeight];
            Marshal.Copy(outBuffer, outlineBuffer, 0, outlineBuffer.Length);
            FreeTypeNative.hyphen_free(outBuffer);

            // Bbox in pixels (26.6 >> 6)
            long outlineMinX = bboxXMin >> 6;
            long outlineMinY = bboxYMin >> 6;
            long outlineMaxX = bboxXMax >> 6;
            long outlineMaxY = bboxYMax >> 6;

            // Blend outline + glyph into dual-channel bitmap (R=outline, A=font)
            // Matches cocos2dx AI88: luminance=outline (byte 0), alpha=font (byte 1)
            // In RGBA32: R=outline, A=font
            long glyphMinX = glyphBitmap.offsetX;
            long glyphMaxX = glyphBitmap.offsetX + glyphBitmap.bitmapWidth;
            long glyphMinY = -glyphBitmap.bitmapHeight - glyphBitmap.offsetY;
            long glyphMaxY = -glyphBitmap.offsetY;

            long blendMinX = Math.Min(outlineMinX, glyphMinX);
            long blendMaxY = Math.Max(outlineMaxY, glyphMaxY);
            long blendWidth = Math.Max(outlineMaxX, glyphMaxX) - blendMinX;
            long blendHeight = blendMaxY - Math.Min(outlineMinY, glyphMinY);

            if (blendWidth <= 0 || blendHeight <= 0)
                return glyphBitmap;

            // 2 bytes per pixel: [0]=outline (R), [1]=font (A)
            byte[] blendImage = new byte[blendWidth * blendHeight * 2];

            // Place outline into byte 0 (R channel)
            long px = outlineMinX - blendMinX;
            long py = blendMaxY - outlineMaxY;
            for (long x = 0; x < outlineWidth; x++)
            {
                for (long y = 0; y < outlineHeight; y++)
                {
                    long idx = px + x + ((py + y) * blendWidth);
                    long srcIdx = x + (y * outlineWidth);
                    if (idx >= 0 && idx < blendImage.Length / 2)
                        blendImage[2 * idx] = outlineBuffer[srcIdx];
                }
            }

            // Place glyph into byte 1 (A channel)
            px = glyphMinX - blendMinX;
            py = blendMaxY - glyphMaxY;
            for (long x = 0; x < glyphBitmap.bitmapWidth; x++)
            {
                for (long y = 0; y < glyphBitmap.bitmapHeight; y++)
                {
                    long idx = px + x + ((y + py) * blendWidth);
                    long srcIdx = x + (y * glyphBitmap.bitmapWidth);
                    if (idx >= 0 && idx < blendImage.Length / 2)
                        blendImage[2 * idx + 1] = glyphBitmap.bitmap[srcIdx];
                }
            }

            GlyphBitmap result = glyphBitmap;
            result.offsetX = (int)blendMinX;
            result.offsetY = (int)(-blendMaxY + _outlineSize);
            result.width = (int)blendWidth;
            result.height = (int)blendHeight;
            result.bitmapWidth = (int)blendWidth;
            result.bitmapHeight = (int)blendHeight;
            result.bitmap = blendImage;
            result.isDualChannel = true;

            return result;
        }

        /// <summary>
        /// Renders a glyph bitmap into a destination buffer at a position.
        /// Port of FontFreeType::renderCharAt.
        /// </summary>
        public static void RenderCharAt(byte[] dest, int destWidth, int posX, int posY,
            byte[] bitmap, int bitmapWidth, int bitmapHeight, bool dualChannel)
        {
            if (bitmap == null) return;

            if (dualChannel)
            {
                // RGBA32 mode: R=outline, G=0, B=0, A=font
                // Input bitmap is 2 bytes/pixel: [0]=outline, [1]=font
                for (long y = 0; y < bitmapHeight; y++)
                {
                    long bitmap_y = y * bitmapWidth;
                    for (int x = 0; x < bitmapWidth; x++)
                    {
                        byte r = bitmap[(bitmap_y + x) * 2];       // outline
                        byte a = bitmap[(bitmap_y + x) * 2 + 1];   // font
                        int destIdx = posX + x + ((posY + (int)y) * destWidth);
                        int di = destIdx * 4;
                        if (di >= 0 && di + 3 < dest.Length)
                        {
                            dest[di] = r;
                            dest[di + 1] = 0;
                            dest[di + 2] = 0;
                            dest[di + 3] = a;
                        }
                    }
                }
            }
            else
            {
                // Alpha8 mode: 1 byte per pixel
                for (long y = 0; y < bitmapHeight; y++)
                {
                    long bitmap_y = y * bitmapWidth;
                    for (int x = 0; x < bitmapWidth; x++)
                    {
                        byte c = bitmap[bitmap_y + x];
                        int destIdx = posX + x + ((posY + (int)y) * destWidth);
                        if (destIdx >= 0 && destIdx < dest.Length)
                        {
                            dest[destIdx] = c;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generates a distance field map from a glyph bitmap.
        /// Port of makeDistanceMap() in CCFontFreeType.cpp.
        /// Uses edtaa3 (compiled into the same freetype.dll).
        /// </summary>
        public static byte[] MakeDistanceMap(byte[] img, long width, long height, int spread)
        {
            long outWidth = width + 2 * spread;
            long outHeight = height + 2 * spread;
            long pixelAmount = outWidth * outHeight;

            double[] data = new double[pixelAmount];
            double[] gx = new double[pixelAmount];
            double[] gy = new double[pixelAmount];
            double[] outside = new double[pixelAmount];
            double[] inside = new double[pixelAmount];
            short[] xdist = new short[pixelAmount];
            short[] ydist = new short[pixelAmount];

            // Convert img into double (data) rescaled [0,1], with padding of spread
            for (long i = 0; i < width; i++)
            {
                for (long j = 0; j < height; j++)
                {
                    data[j * outWidth + spread + i] = img[j * width + i] / 255.0;
                }
            }

            // Transform background (outside contour)
            FreeTypeNative.computegradient(data, (int)outWidth, (int)outHeight, gx, gy);
            FreeTypeNative.edtaa3(data, gx, gy, (int)outWidth, (int)outHeight, xdist, ydist, outside);
            for (long i = 0; i < pixelAmount; i++)
                if (outside[i] < 0.0) outside[i] = 0.0;

            // Transform foreground (inside contour)
            for (long i = 0; i < pixelAmount; i++)
                data[i] = 1.0 - data[i];
            // Recompute gradient for inverted data
            gx = new double[pixelAmount];
            gy = new double[pixelAmount];
            FreeTypeNative.computegradient(data, (int)outWidth, (int)outHeight, gx, gy);
            FreeTypeNative.edtaa3(data, gx, gy, (int)outWidth, (int)outHeight, xdist, ydist, inside);
            for (long i = 0; i < pixelAmount; i++)
                if (inside[i] < 0.0) inside[i] = 0.0;

            // Bipolar distance field: outside - inside
            byte[] outMap = new byte[pixelAmount];
            for (long i = 0; i < pixelAmount; i++)
            {
                double dist = outside[i] - inside[i];
                dist = 128.0 - dist * 16.0;
                if (dist < 0) dist = 0;
                if (dist > 255) dist = 255;
                outMap[i] = (byte)dist;
            }

            return outMap;
        }

        /// <summary>
        /// Releases cached font data when no more references exist.
        /// Port of FontFreeType::releaseFont.
        /// </summary>
        public static void ReleaseFont(string fontName)
        {
            s_cache.Remove(fontName);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_stroker != IntPtr.Zero)
                {
                    FreeTypeNative.FT_Stroker_Done(_stroker);
                    _stroker = IntPtr.Zero;
                }
                if (_face != IntPtr.Zero)
                {
                    FreeTypeNative.FT_Done_Face(_face);
                    _face = IntPtr.Zero;
                }

                // Decrement ref count and free data if last reference
                if (s_cache.TryGetValue(_fontName, out var cached))
                {
                    cached.refCount--;
                    if (cached.refCount <= 0)
                    {
                        s_cache.Remove(_fontName);
                    }
                    else
                    {
                        s_cache[_fontName] = cached;
                    }
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Glyph bitmap data and metrics.
    /// </summary>
    public struct GlyphBitmap
    {
        public byte[] bitmap;
        public int bitmapWidth;
        public int bitmapHeight;
        public int width;
        public int height;
        public int offsetX;
        public int offsetY;
        public int xAdvance;
        public bool isDualChannel;
    }
}
