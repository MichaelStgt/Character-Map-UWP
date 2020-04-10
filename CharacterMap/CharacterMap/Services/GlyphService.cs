//#define GENERATE_DATA

using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Provider;
using CharacterMapCX;
using GalaSoft.MvvmLight.Ioc;
using Microsoft.Graphics.Canvas.Text;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Services
{
    public interface IGlyphData
    {
        [Column("Ix")]
        int UnicodeIndex { get; }
        [Column("Hx")]
        string UnicodeHex { get; }
        string Description { get; }
    }

    public class GlyphDescription : IGlyphData
    {
        [PrimaryKey, Column("Ix")]
        public int UnicodeIndex { get; set; }

        [Indexed, MaxLength(5), Column("Hx")]
        public string UnicodeHex { get; set; }

        public string Description { get; set; }
    }

    public class EmojiGlyph
    { 
        [PrimaryKey]
        public string Codepoints { get; set; }
        public string Description { get; set; }
        public string Utf32 { get; set; }
    }


    public class MDL2Glyph : GlyphDescription { }
    public class MaterialDesignIconsLegacyGlyph : GlyphDescription { }
    public class MaterialDesignIconsGlyph : GlyphDescription { }
    public class WebdingsGlyph : GlyphDescription { }
    public class WingdingsGlyph : GlyphDescription { }
    public class Wingdings2Glyph : GlyphDescription { }
    public class Wingdings3Glyph : GlyphDescription { }
    public class FontAwesomeGlyph : GlyphDescription { }
    public class IcoFontGlyph : GlyphDescription { }

    public interface IGlyphDataProvider
    {
        void Initialise();
        string GetCharacterDescription(int unicodeIndex, FontVariant variant);
        string GetEmojiDescription(Character c);
        EmojiGlyph GetEmoji(Character c);
        Task<IReadOnlyList<IGlyphData>> SearchAsync(string query, FontVariant variant);
    }

    public static class GlyphService
    {
        private static IGlyphDataProvider _provider { get; set; }

        private static Task _init { get; set; }

        public static IReadOnlyList<IGlyphData> EMPTY_SEARCH = new List<IGlyphData>();

        static GlyphService()
        {
            _provider = new SQLiteGlyphProvider();
            _provider.Initialise();
        }

        public static Task InitializeAsync()
        {
            return _init ?? (_init = InitializeInternalAsync());
        }

        private static Task InitializeInternalAsync()
        {
#if DEBUG //&& GENERATE_DATABASE
            if (_provider is SQLiteGlyphProvider p)
            {
                return p.InitialiseDatabaseAsync();
            }
#endif
            return Task.CompletedTask;
        }

        internal static string GetCharacterDescription(Character c, FontVariant variant, bool isEmoji = false)
        {
            if (variant == null || _provider == null)
                return null;

            if (isEmoji)
                return _provider.GetEmojiDescription(c);
            else
                return  _provider.GetCharacterDescription(c.UnicodeIndex, variant);
        }

        internal static string GetCharacterKeystroke(int unicodeIndex)
        {
            if (unicodeIndex >= 128 && unicodeIndex <= 255)
                return Localization.Get("CharacterKeystrokeLabel",  unicodeIndex);

            return null;
        }

        internal static UInt32[] GetEmojiSequence(Character c)
        {
            if (_provider.GetEmoji(c) is EmojiGlyph e)
            {
                return e.Utf32.Split(',').Select(s => Convert.ToUInt32(s, 10)).ToArray();
            }

            return null;
        }

        internal static Task<IReadOnlyList<IGlyphData>> SearchAsync(string query, FontVariant variant)
        {
            if (variant == null)
                return Task.FromResult(EMPTY_SEARCH);

            return _provider.SearchAsync(query, variant);
        }

        public static (string Hex, string FontIcon, string Path, string Symbol) GetDevValues(
            Character c, FontVariant v, CanvasTextLayoutAnalysis a, CanvasTypography t, bool isXaml)
        {
            if (v == FontFinder.DefaultFont.DefaultVariant)
                return (string.Empty, string.Empty, string.Empty, string.Empty);

            Interop interop = SimpleIoc.Default.GetInstance<Interop>();

            string h, f, p, s = null;
            bool hasSymbol = FontFinder.IsMDL2(v) && Enum.IsDefined(typeof(Symbol), c.UnicodeIndex);
            
            string pathData;
            using (var geom = ExportManager.CreateGeometry(ResourceHelper.AppSettings.GridSize, v, c, a, t))
            {
                pathData = interop.GetPathData(geom).Path;
            }

            var hex = c.UnicodeIndex.ToString("x4").ToUpper();
            if (isXaml)
            {
                h = $"&#x{hex};";
                f = $@"<FontIcon FontFamily=""{v.XamlFontSource}"" Glyph=""&#x{hex};"" />";
                p = $"<Path Data=\"{pathData}\" Fill=\"{{ThemeResource SystemControlForegroundBaseHighBrush}}\" Stretch=\"Uniform\" />";

                if (hasSymbol)
                    s = $@"<SymbolIcon Symbol=""{(Symbol)c.UnicodeIndex}"" />";
            }
            else
            {
                h = $"\\u{hex}";
                f = $"new FontIcon {{ FontFamily = new Windows.UI.Xaml.Media.FontFamily(\"{v.XamlFontSource}\") , Glyph = \"\\u{hex}\" }};";
                p = $"new Windows.UI.Xaml.Shapes.Path {{ Data = (Windows.UI.Xaml.Media.Geometry)Windows.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry), \"{pathData}\"), Fill = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Black), Stretch = Windows.UI.Xaml.Media.Stretch.Uniform }};";

                if (hasSymbol)
                    s = $"new SymbolIcon {{ Symbol = Symbol.{(Symbol)c.UnicodeIndex} }};";
            }

            return (h, f, p, s);
        }

    }
}
