namespace Hyphen
{
    /// <summary>
    /// Per-character definition in the font atlas.
    /// Port of cocos2dx FontLetterDefinition.
    /// </summary>
    public struct HyphenLetterDefinition
    {
        public float U;
        public float V;
        public float width;
        public float height;
        public float offsetX;
        public float offsetY;
        public int textureID;
        public bool validDefinition;
        public int xAdvance;
    }
}
