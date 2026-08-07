namespace Hyphen
{
    /// <summary>
    /// Glyph collection mode (mirrors cocos2dx GlyphCollection).
    /// </summary>
    public enum GlyphCollection
    {
        DYNAMIC,
        NEHE,
        ASCII,
        CUSTOM
    }

    /// <summary>
    /// Label effect type (mirrors cocos2dx LabelEffect).
    /// </summary>
    public enum LabelEffect
    {
        NORMAL,
        OUTLINE,
        SHADOW,
        GLOW,
        ALL
    }

    /// <summary>
    /// Horizontal text alignment (mirrors cocos2dx TextHAlignment).
    /// </summary>
    public enum TextHAlignment
    {
        LEFT,
        CENTER,
        RIGHT
    }

    /// <summary>
    /// Vertical text alignment (mirrors cocos2dx TextVAlignment).
    /// </summary>
    public enum TextVAlignment
    {
        TOP,
        CENTER,
        BOTTOM
    }

    /// <summary>
    /// Overflow mode (mirrors cocos2dx Label::Overflow).
    /// </summary>
    public enum Overflow
    {
        NONE,
        CLAMP,
        SHRINK,
        RESIZE_HEIGHT
    }

    /// <summary>
    /// Wrap mode for text layout.
    /// </summary>
    public enum WrapMode
    {
        NONE,
        CHAR,
        WORD
    }

    /// <summary>
    /// Label type (mirrors cocos2dx Label::LabelType).
    /// </summary>
    public enum LabelType
    {
        TTF,
        BMFONT,
        CHARMAP,
        STRING_TEXTURE
    }
}
