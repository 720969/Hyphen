namespace Hyphen
{
    /// <summary>
    /// Abstract font base class (mirrors cocos2dx Font).
    /// </summary>
    public abstract class HyphenFont
    {
        public abstract HyphenFontAtlas CreateFontAtlas();

        public abstract int[] GetHorizontalKerningForText(string text, out int outNumLetters);

        public virtual int GetFontMaxHeight() => 0;
    }
}
