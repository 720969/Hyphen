using System.Collections.Generic;
using UnityEngine;

namespace Hyphen
{
    /// <summary>
    /// Font atlas cache — caches atlases by font name + size + options.
    /// Port of cocos2dx CCFontAtlasCache.
    /// </summary>
    public static class HyphenFontAtlasCache
    {
        private static readonly Dictionary<string, HyphenFontAtlas> s_atlasMap = new Dictionary<string, HyphenFontAtlas>();
        private static readonly Dictionary<string, int> s_refCount = new Dictionary<string, int>();

        /// <summary>
        /// Gets or creates a TTF font atlas.
        /// Port of FontAtlasCache::getFontAtlasTTF.
        /// </summary>
        public static HyphenFontAtlas GetFontAtlasTTF(string fontName, byte[] fontData, float fontSize,
            GlyphCollection glyphs = GlyphCollection.DYNAMIC, string customGlyphs = null,
            bool distanceFieldEnabled = false, float outlineSize = 0)
        {
            bool useDistanceField = distanceFieldEnabled;
            if (outlineSize > 0)
                useDistanceField = false;

            string atlasName = GenerateFontName(fontName, fontSize, useDistanceField);
            atlasName += "_outline_" + outlineSize;

            if (s_atlasMap.TryGetValue(atlasName, out var atlas))
            {
                s_refCount[atlasName]++;
                return atlas;
            }

            var font = HyphenFontFreeType.Create(fontName, fontData, fontSize, glyphs, customGlyphs,
                useDistanceField, outlineSize);
            if (font == null)
            {
                Debug.LogError($"[Hyphen] Failed to create FreeType font '{fontName}'");
                return null;
            }

            var newAtlas = font.CreateFontAtlas();
            if (newAtlas != null)
            {
                s_atlasMap[atlasName] = newAtlas;
                s_refCount[atlasName] = 1;
                return newAtlas;
            }

            return null;
        }

        /// <summary>
        /// Releases a font atlas reference.
        /// Port of FontAtlasCache::releaseFontAtlas.
        /// </summary>
        public static bool ReleaseFontAtlas(HyphenFontAtlas atlas)
        {
            if (atlas == null) return false;

            foreach (var kvp in s_atlasMap)
            {
                if (kvp.Value == atlas)
                {
                    string name = kvp.Key;
                    if (s_refCount.TryGetValue(name, out int count))
                    {
                        count--;
                        if (count <= 0)
                        {
                            s_atlasMap.Remove(name);
                            s_refCount.Remove(name);
                            // Free the FreeType face
                            if (atlas.Font != null)
                            {
                                atlas.Font.FreeTypeFace?.Dispose();
                            }
                        }
                        else
                        {
                            s_refCount[name] = count;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Purges all cached data.
        /// </summary>
        public static void PurgeCachedData()
        {
            s_atlasMap.Clear();
            s_refCount.Clear();
        }

        /// <summary>
        /// Checks if any fonts are registered.
        /// </summary>
        public static bool HasFonts => s_atlasMap.Count > 0;

        private static string GenerateFontName(string fontFileName, float size, bool useDistanceField)
        {
            string name = fontFileName;
            if (useDistanceField)
                name += "df";
            name += size.ToString("F2");
            return name;
        }
    }
}
