using Hyphen.FreeType;

namespace Hyphen
{
    /// <summary>
    /// FreeType font implementation.
    /// Port of cocos2dx CCFontFreeType.
    /// </summary>
    public sealed class HyphenFontFreeType : HyphenFont
    {
        public const int DistanceMapSpread = 3;

        private static readonly string s_glyphASCII =
            "\"!#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\u00a1\u00a2\u00a3\u00a4\u00a5\u00a6\u00a7\u00a8\u00a9\u00aa\u00ab\u00ac\u00ad\u00ae\u00af\u00b0\u00b1\u00b2\u00b3\u00b4\u00b5\u00b6\u00b7\u00b8\u00b9\u00ba\u00bb\u00bc\u00bd\u00be\u00bf\u00c0\u00c1\u00c2\u00c3\u00c4\u00c5\u00c6\u00c7\u00c8\u00c9\u00ca\u00cb\u00cc\u00cd\u00ce\u00cf\u00d0\u00d1\u00d2\u00d3\u00d4\u00d5\u00d6\u00d7\u00d8\u00d9\u00da\u00db\u00dc\u00dd\u00de\u00df\u00e0\u00e1\u00e2\u00e3\u00e4\u00e5\u00e6\u00e7\u00e8\u00e9\u00ea\u00eb\u00ec\u00ed\u00ee\u00ef\u00f0\u00f1\u00f2\u00f3\u00f4\u00f5\u00f6\u00f7\u00f8\u00f9\u00fa\u00fb\u00fc\u00fd\u00fe\u00ff ";

        private static readonly string s_glyphNEHE =
            "!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~ ";

        private readonly FreeTypeFace _face;
        private readonly GlyphCollection _usedGlyphs;
        private readonly string _customGlyphs;
        private HyphenFontAtlas _fontAtlas;

        public bool IsDistanceFieldEnabled => _face?.IsDistanceFieldEnabled ?? false;
        public float OutlineSize => _face?.OutlineSize ?? 0;
        public FreeTypeFace FreeTypeFace => _face;

        /// <summary>
        /// Creates a FreeType font.
        /// Port of FontFreeType::create().
        /// </summary>
        public static HyphenFontFreeType Create(string fontName, byte[] fontData, float fontSize,
            GlyphCollection glyphs = GlyphCollection.DYNAMIC, string customGlyphs = null,
            bool distanceFieldEnabled = false, float outline = 0)
        {
            var face = FreeTypeFace.Create(fontName, fontData, fontSize, distanceFieldEnabled, outline);
            if (face == null) return null;

            return new HyphenFontFreeType(face, glyphs, customGlyphs);
        }

        private HyphenFontFreeType(FreeTypeFace face, GlyphCollection glyphs, string customGlyphs)
        {
            _face = face;
            _usedGlyphs = glyphs;
            _customGlyphs = customGlyphs;
        }

        public override HyphenFontAtlas CreateFontAtlas()
        {
            if (_fontAtlas == null)
            {
                _fontAtlas = new HyphenFontAtlas(this);
            }
            return _fontAtlas;
        }

        public override int[] GetHorizontalKerningForText(string text, out int outNumLetters)
        {
            outNumLetters = text?.Length ?? 0;
            if (outNumLetters == 0) return null;
            return _face?.GetHorizontalKerningForText(text);
        }

        public override int GetFontMaxHeight()
        {
            return _face?.GetFontLineHeight() ?? 0;
        }

        public int GetFontAscender()
        {
            return _face?.GetFontAscender() ?? 0;
        }

        /// <summary>
        /// Gets the glyph bitmap for a character.
        /// Port of FontFreeType::getGlyphBitmap().
        /// </summary>
        public GlyphBitmap GetGlyphBitmap(ushort theChar)
        {
            return _face?.GetGlyphBitmap(theChar) ?? default;
        }

        /// <summary>
        /// Renders a character bitmap into the atlas pixel buffer.
        /// Port of FontFreeType::renderCharAt().
        /// </summary>
        public void RenderCharAt(byte[] dest, int destWidth, int posX, int posY,
            byte[] bitmap, int bitmapWidth, int bitmapHeight, bool dualChannel)
        {
            FreeTypeFace.RenderCharAt(dest, destWidth, posX, posY, bitmap, bitmapWidth, bitmapHeight, dualChannel);
        }

        /// <summary>
        /// Generates a distance field map from a glyph bitmap.
        /// Port of makeDistanceMap().
        /// </summary>
        public static byte[] MakeDistanceMap(byte[] img, long width, long height)
        {
            return FreeTypeFace.MakeDistanceMap(img, width, height, DistanceMapSpread);
        }

        public string GetGlyphCollection()
        {
            switch (_usedGlyphs)
            {
                case GlyphCollection.NEHE: return s_glyphNEHE;
                case GlyphCollection.ASCII: return s_glyphASCII;
                case GlyphCollection.CUSTOM: return _customGlyphs ?? "";
                default: return null;
            }
        }

        public static void ReleaseFont(string fontName)
        {
            FreeTypeFace.ReleaseFont(fontName);
        }
    }
}
