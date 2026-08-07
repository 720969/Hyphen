namespace Hyphen
{
    /// <summary>
    /// TTF font configuration (mirrors cocos2dx TTFConfig / _ttfConfig).
    /// </summary>
    public struct HyphenTTFConfig
    {
        public string fontFilePath;
        public float fontSize;
        public GlyphCollection glyphs;
        public string customGlyphs;
        public bool distanceFieldEnabled;
        public float outlineSize;

        public HyphenTTFConfig(string filePath, float size = 12,
            GlyphCollection glyphCollection = GlyphCollection.DYNAMIC,
            string customGlyphCollection = null,
            bool useDistanceField = false,
            float outline = 0)
        {
            fontFilePath = filePath;
            fontSize = size;
            glyphs = glyphCollection;
            customGlyphs = customGlyphCollection;
            distanceFieldEnabled = useDistanceField;
            outlineSize = outline;

            if (outline > 0)
            {
                distanceFieldEnabled = false;
            }
        }
    }
}
