using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace SaksiTerakhir.EditorTools
{
    public static class ArchiveDocumentFontSetup
    {
        private const string Source = "Assets/_Project/UI/Fonts/Source/NotoSansSundanese-Regular.ttf";
        public const string FontPath = "Assets/_Project/UI/Fonts/TMP/Sundanese Inscription SDF.asset";

        public static TMP_FontAsset Ensure(string originalText)
        {
            // Bake the inscription's Sundanese glyphs only. Latin punctuation uses
            // the existing TMP font, keeping this atlas small and self-contained.
            string characters = new string(originalText.Where(c => c >= '\u1b80' && c <= '\u1bbf')
                .Distinct().OrderBy(c => c).ToArray());
            if (characters.Length == 0) throw new InvalidOperationException("The inscription contains no Sundanese text.");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null && font.HasCharacters(characters)) return font;
            Font source = AssetDatabase.LoadAssetAtPath<Font>(Source);
            if (source == null || TMP_Settings.defaultFontAsset == null)
                throw new InvalidOperationException("Import the Sundanese source font and TMP essential resources first.");
            bool created = font == null;
            if (created)
                font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                    1024, 1024, AtlasPopulationMode.Dynamic, false);
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            if (!font.TryAddCharacters(characters, out string missing, true))
                throw new InvalidOperationException("Missing inscription glyphs: " + string.Join(", ",
                    missing.Select(c => "U+" + ((int)c).ToString("X4"))));
            font.name = "Sundanese Inscription SDF";
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.isMultiAtlasTexturesEnabled = false;
            font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
            if (created)
            {
                AssetDatabase.CreateAsset(font, FontPath);
                foreach (Texture2D texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
                AssetDatabase.AddObjectToAsset(font.material, font);
            }
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (Texture2D texture in font.atlasTextures) EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssetIfDirty(font);
            return font;
        }
    }
}
