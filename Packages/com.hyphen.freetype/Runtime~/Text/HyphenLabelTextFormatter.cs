using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hyphen
{
    /// <summary>
    /// Text layout logic — word/char wrapping, alignment, overflow shrinking.
    /// Port of cocos2dx CCLabelTextFormatter.
    /// </summary>
    internal sealed class HyphenLabelTextFormatter
    {
        private readonly HyphenLabel _label;

        public HyphenLabelTextFormatter(HyphenLabel label)
        {
            _label = label;
        }

        /// <summary>
        /// Computes alignment offsets (horizontal per-line, vertical total).
        /// Port of Label::computeAlignmentOffset.
        /// </summary>
        public void ComputeAlignmentOffset()
        {
            _label.LinesOffsetX.Clear();
            switch (_label.HAlignment)
            {
                case TextHAlignment.LEFT:
                    for (int i = 0; i < _label.NumberOfLines; i++)
                        _label.LinesOffsetX.Add(0);
                    break;
                case TextHAlignment.CENTER:
                    foreach (var lineWidth in _label.LinesWidth)
                        _label.LinesOffsetX.Add((_label.ContentSize.x - lineWidth) / 2f);
                    break;
                case TextHAlignment.RIGHT:
                    foreach (var lineWidth in _label.LinesWidth)
                        _label.LinesOffsetX.Add(_label.ContentSize.x - lineWidth);
                    break;
            }

            switch (_label.VAlignment)
            {
                case TextVAlignment.TOP:
                    _label.LetterOffsetY = _label.ContentSize.y;
                    break;
                case TextVAlignment.CENTER:
                    _label.LetterOffsetY = (_label.ContentSize.y + _label.TextDesiredHeight) / 2f;
                    break;
                case TextVAlignment.BOTTOM:
                    _label.LetterOffsetY = _label.TextDesiredHeight;
                    break;
            }
        }

        /// <summary>
        /// Wraps text by characters (one char per token).
        /// Port of Label::multilineTextWrapByChar.
        /// </summary>
        public bool MultilineTextWrapByChar()
        {
            return MultilineTextWrap((text, startIndex, textLen) => 1);
        }

        /// <summary>
        /// Wraps text by words (CJK-aware).
        /// Port of Label::multilineTextWrapByWord.
        /// </summary>
        public bool MultilineTextWrapByWord()
        {
            return MultilineTextWrap(GetFirstWordLen);
        }

        /// <summary>
        /// Gets the first word length starting at startIndex.
        /// CJK characters are treated as single-char words.
        /// Port of getFirstWordLen.
        /// </summary>
        private int GetFirstWordLen(string text, int startIndex, int textLen)
        {
            char character = text[startIndex];
            if (IsCJKUnicode(character) || char.IsWhiteSpace(character) || character == '\n')
                return 1;

            int len = 1;
            for (int index = startIndex + 1; index < textLen; index++)
            {
                character = text[index];
                if (character == '\n' || char.IsWhiteSpace(character) || IsCJKUnicode(character))
                    break;
                len++;
            }
            return len;
        }

        /// <summary>
        /// Checks if a character is CJK (Chinese/Japanese/Korean).
        /// Port of StringUtils::isCJKUnicode.
        /// </summary>
        public static bool IsCJKUnicode(char c)
        {
            // CJK Unified Ideographs
            if (c >= 0x4E00 && c <= 0x9FFF) return true;
            // CJK Extension A
            if (c >= 0x3400 && c <= 0x4DBF) return true;
            // CJK Compatibility Ideographs
            if (c >= 0xF900 && c <= 0xFAFF) return true;
            // Hiragana
            if (c >= 0x3040 && c <= 0x309F) return true;
            // Katakana
            if (c >= 0x30A0 && c <= 0x30FF) return true;
            // Hangul Syllables
            if (c >= 0xAC00 && c <= 0xD7AF) return true;
            // CJK Symbols and Punctuation
            if (c >= 0x3000 && c <= 0x303F) return true;
            return false;
        }

        /// <summary>
        /// Generic text wrapping with a token length function.
        /// Port of Label::multilineTextWrap.
        /// </summary>
        public bool MultilineTextWrap(Func<string, int, int, int> nextTokenLen)
        {
            int textLen = _label.LengthOfString;
            int lineIndex = 0;
            float nextTokenX = 0f;
            float nextTokenY = 0f;
            float longestLine = 0f;
            float letterRight = 0f;

            float lineSpacing = _label.LineSpacing;
            float highestY = 0f;
            float lowestY = 0f;

            float lineHeight = _label.LineHeight;
            float bmfontScale = _label.BMFontScaleVal;
            float additionalKerning = _label.AdditionalKerning;
            int[] kernings = _label.HorizontalKernings;
            HyphenFontAtlas atlas = _label.FontAtlas;

            for (int index = 0; index < textLen;)
            {
                char character = _label.Text[index];
                if (character == '\n')
                {
                    _label.LinesWidth.Add(letterRight);
                    letterRight = 0f;
                    lineIndex++;
                    nextTokenX = 0f;
                    nextTokenY -= lineHeight * bmfontScale + lineSpacing;
                    _label.RecordPlaceholderInfo(index, character);
                    index++;
                    continue;
                }

                int tokenLen = nextTokenLen(_label.Text, index, textLen);
                float tokenHighestY = highestY;
                float tokenLowestY = lowestY;
                float tokenRight = letterRight;
                float nextLetterX = nextTokenX;
                bool newLine = false;

                for (int tmp = 0; tmp < tokenLen; tmp++)
                {
                    int letterIndex = index + tmp;
                    character = _label.Text[letterIndex];
                    if (character == '\r')
                    {
                        _label.RecordPlaceholderInfo(letterIndex, character);
                        continue;
                    }

                    if (!atlas.GetLetterDefinitionForChar(character, out HyphenLetterDefinition letterDef))
                    {
                        _label.RecordPlaceholderInfo(letterIndex, character);
                        continue;
                    }

                    float letterX = nextLetterX + letterDef.offsetX * bmfontScale;
                    if (_label.EnableWrapVal && nextTokenX > 0f &&
                        letterX + letterDef.width * bmfontScale > _label.MaxLineWidthVal &&
                        !char.IsWhiteSpace(character))
                    {
                        _label.LinesWidth.Add(letterRight);
                        letterRight = 0f;
                        lineIndex++;
                        nextTokenX = 0f;
                        nextTokenY -= (lineHeight * bmfontScale + lineSpacing);
                        newLine = true;
                        break;
                    }

                    float letterPosY = nextTokenY - letterDef.offsetY * bmfontScale;
                    _label.RecordLetterInfo(letterX, letterPosY, character, letterIndex, lineIndex);

                    if (kernings != null && letterIndex < textLen - 1)
                        nextLetterX += kernings[letterIndex + 1];
                    nextLetterX += letterDef.xAdvance * bmfontScale + additionalKerning;

                    tokenRight = letterX + letterDef.width * bmfontScale;

                    if (tokenHighestY < letterPosY)
                        tokenHighestY = letterPosY;
                    if (tokenLowestY > letterPosY - letterDef.height * bmfontScale)
                        tokenLowestY = letterPosY - letterDef.height * bmfontScale;
                }

                if (newLine)
                    continue;

                nextTokenX = nextLetterX;
                letterRight = tokenRight;
                if (highestY < tokenHighestY)
                    highestY = tokenHighestY;
                if (lowestY > tokenLowestY)
                    lowestY = tokenLowestY;
                if (longestLine < letterRight)
                    longestLine = letterRight;

                index += tokenLen;
            }

            _label.LinesWidth.Add(letterRight);
            _label.NumberOfLines = lineIndex + 1;
            _label.TextDesiredHeight = _label.NumberOfLines * lineHeight * bmfontScale;
            if (_label.NumberOfLines > 1)
                _label.TextDesiredHeight += (_label.NumberOfLines - 1) * lineSpacing;

            // contentSize = text box size (rect), or auto-size to text if rect is too small
            var contentSize = new Vector2(_label.LabelWidth, _label.LabelHeight);
            // Always provide preferred sizes = actual text dimensions
            contentSize.x = Mathf.Max(longestLine, contentSize.x);
            contentSize.y = Mathf.Max(_label.TextDesiredHeight, contentSize.y);
            _label.SetContentSize(contentSize);

            _label.TailoredTopY = contentSize.y;
            _label.TailoredBottomY = 0f;
            if (highestY > 0f)
                _label.TailoredTopY = contentSize.y + highestY;
            if (lowestY < -_label.TextDesiredHeight)
                _label.TailoredBottomY = _label.TextDesiredHeight + lowestY;

            return true;
        }

        /// <summary>
        /// Checks if text overflows vertically.
        /// Port of Label::isVerticalClamp.
        /// </summary>
        public bool IsVerticalClamp()
        {
            return _label.TextDesiredHeight > _label.ContentSize.y;
        }

        /// <summary>
        /// Checks if text overflows horizontally.
        /// Port of Label::isHorizontalClamp.
        /// </summary>
        public bool IsHorizontalClamp()
        {
            for (int ctr = 0; ctr < _label.LengthOfString; ctr++)
            {
                var letterInfo = _label.GetLetterInfo(ctr);
                if (!letterInfo.valid) continue;

                if (!atlas_GetLetterDefinition(ctr, out HyphenLetterDefinition letterDef)) continue;

                float px = letterInfo.positionX + letterDef.width / 2f * _label.BMFontScaleVal;
                int lineIndex = letterInfo.lineIndex;

                if (_label.LabelWidth > 0f)
                {
                    if (!_label.EnableWrapVal)
                    {
                        if (px > _label.ContentSize.x)
                            return true;
                    }
                    else
                    {
                        float wordWidth = _label.LinesWidth[lineIndex];
                        if (wordWidth > _label.ContentSize.x && px > _label.ContentSize.x)
                            return true;
                    }
                }
            }
            return false;
        }

        private bool atlas_GetLetterDefinition(int letterIndex, out HyphenLetterDefinition def)
        {
            var charCode = _label.GetLetterInfo(letterIndex).utf16Char;
            return _label.FontAtlas.GetLetterDefinitionForChar(charCode, out def);
        }

        /// <summary>
        /// Shrinks label font size until it fits the content area.
    }
}
