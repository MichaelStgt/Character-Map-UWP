using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Services;
using CharacterMap.Views;
using CharacterMapCX;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Views;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using CharacterMap.Models;

namespace CharacterMap.ViewModels
{
    public class FontMapViewModel : ViewModelBase
    {
        #region Properties

        private Interop _interop { get; }

        private Debouncer _searchDebouncer { get; }

        private ConcurrencyToken.ConcurrencyTokenGenerator _searchTokenFactory { get; }

        public AppSettings Settings { get; }

        public StorageFile SourceFile { get; set; }

        public ExportStyle BlackColor { get; } = ExportStyle.Black;
        public ExportStyle WhiteColor { get; } = ExportStyle.White;
        public ExportStyle GlyphColor { get; } = ExportStyle.ColorGlyph;

        public IDialogService DialogService { get; }
        public RelayCommand<ExportStyle> CommandSavePng { get; }
        public RelayCommand<ExportStyle> CommandSaveSvg { get; }

        internal bool IsLoadingCharacters { get; private set; }

        public bool IsDarkAccent => Utils.IsAccentColorDark();

        public bool IsExternalFile { get; set; }


        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        private string _titlePrefix;
        public string TitlePrefix
        {
            get => _titlePrefix;
            set => Set(ref _titlePrefix, value);
        }

        private IReadOnlyList<IGlyphData> _searchResults;
        public IReadOnlyList<IGlyphData> SearchResults
        {
            get => _searchResults;
            set => Set(ref _searchResults, value);
        }

        private InstalledFont _selectedFont;
        public InstalledFont SelectedFont
        {
            get => _selectedFont;
            set
            {
                if (value == _selectedFont) return;
                _selectedFont = value;
                TitleBarHelper.SetTitle(value?.Name);
                RaisePropertyChanged();
                if (null != _selectedFont)
                {
                    if (value != null) TitlePrefix = value.Name + " -";
                    SelectedVariant = _selectedFont.DefaultVariant;
                }
                else
                {
                    SelectedVariant = null;
                }
            }
        }

        private FontVariant _selectedVariant;
        public FontVariant SelectedVariant
        {
            get => _selectedVariant;
            set
            {
                if (value != _selectedVariant)
                {
                    Chars = null;
                    _selectedVariant = value;
                    FontFamily = value == null ? null : new FontFamily(value.Source);
                    LoadChars(value);
                    RaisePropertyChanged();
                    SelectedTypography = value?.XamlTypographyFeatures?.FirstOrDefault();
                    SetDefaultChar();
                }
            }
        }

        private IReadOnlyList<Character> _chars;
        public IReadOnlyList<Character> Chars
        {
            get => _chars;
            set => Set(ref _chars, value);
        }

        private FontFamily _fontFamily;
        public FontFamily FontFamily
        {
            get => _fontFamily;
            private set => Set(ref _fontFamily, value);
        }

        private TypographyFeatureInfo _selectedTypography;
        public TypographyFeatureInfo SelectedTypography
        {
            get => _selectedTypography;
            set => Set(ref _selectedTypography, value);
        }

        private CanvasTextLayoutAnalysis _selectedVariantAnalysis;
        public CanvasTextLayoutAnalysis SelectedVariantAnalysis
        {
            get => _selectedVariantAnalysis;
            set => Set(ref _selectedVariantAnalysis, value);
        }

        private CanvasTextLayoutAnalysis _selectedCharAnalysis;
        public CanvasTextLayoutAnalysis SelectedCharAnalysis
        {
            get => _selectedCharAnalysis;
            set => Set(ref _selectedCharAnalysis, value);
        }

        private Character _selectedChar;
        public Character SelectedChar
        {
            get => _selectedChar;
            set
            {
                if (_selectedChar == value) return;
                _selectedChar = value;
                if (null != value)
                {
                    Settings.LastSelectedCharIndex = value.UnicodeIndex;
                }
                RaisePropertyChanged();
                UpdateCharAnalysis();
                UpdateDevValues();
            }
        }

        private bool _showColorGlyphs = true;
        public bool ShowColorGlyphs
        {
            get => _showColorGlyphs;
            set => Set(ref _showColorGlyphs, value);
        }

        private bool _importButtonEnabled = true;
        public bool ImportButtonEnabled
        {
            get => _importButtonEnabled;
            set => Set(ref _importButtonEnabled, value);
        }

        private bool _hasFontOptions = false;
        public bool HasFontOptions
        {
            get => _hasFontOptions;
            set => Set(ref _hasFontOptions, value);
        }

        private bool _isSvgChar = false;
        public bool IsSvgChar
        {
            get => _isSvgChar;
            set => Set(ref _isSvgChar, value);
        }

        private bool _isLongGeometry = true;
        public bool IsLongGeometry
        {
            get => _isLongGeometry;
            set => Set(ref _isLongGeometry, value);
        }

        private bool _isGlyphMapMode = false;
        public bool IsGlyphMapMode
        {
            get => _isGlyphMapMode;
            set
            {
                if (Set(ref _isGlyphMapMode, value))
                {
                    Chars = null;
                    LoadChars(SelectedVariant);
                }
            }
        }

        private string _xamlPath;
        public string XamlPath
        {
            get => _xamlPath;
            set => Set(ref _xamlPath, value);
        }

        private string _xamlCode;
        public string XamlCode
        {
            get => _xamlCode;
            set => Set(ref _xamlCode, value);
        }

        private string _xamlPathGeom;
        public string XamlPathGeom
        {
            get => _xamlPathGeom;
            set { if (Set(ref _xamlPathGeom, value)) IsLongGeometry = value != null && value.Length > 2048; }
        }

        private string _symbolIcon;
        public string SymbolIcon
        {
            get => _symbolIcon;
            set => Set(ref _symbolIcon, value);
        }

        private string _fontIcon;
        public string FontIcon
        {
            get => _fontIcon;
            set => Set(ref _fontIcon, value);
        }

        private string _searchQuery;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (Set(ref _searchQuery, value))
                    DebounceSearch(value, Settings.InstantSearchDelay, SearchSource.AutoProperty);
            }
        }

        #endregion

        public FontMapViewModel(IDialogService dialogService, AppSettings settings)
        {
            DialogService = dialogService;
            Settings = settings;

            CommandSavePng = new RelayCommand<ExportStyle>(async (b) => await SavePngAsync(b));
            CommandSaveSvg = new RelayCommand<ExportStyle>(async (b) => await SaveSvgAsync(b));

            _interop = SimpleIoc.Default.GetInstance<Interop>();

            _searchDebouncer = new Debouncer();
            _searchTokenFactory = new ConcurrencyToken.ConcurrencyTokenGenerator();
        }

        private void LoadChars(FontVariant variant)
        {
            try
            {
                IsLoadingCharacters = true;

                if (variant != null)
                {
                    var chars = TypographyAnalyzer.GetCharString(variant);
                    using (CanvasTextFormat format = new CanvasTextFormat
                    {
                        FontSize = 8,
                        FontFamily = variant.Source,
                        FontStretch = variant.FontFace.Stretch,
                        FontWeight = variant.FontFace.Weight,
                        FontStyle = variant.FontFace.Style,
                        HorizontalAlignment = CanvasHorizontalAlignment.Left,
                    })
                    using (CanvasTextLayout layout = new CanvasTextLayout(
                        Utils.CanvasDevice, chars, format, 1024, 1024))
                    {
                        layout.Options = CanvasDrawTextOptions.EnableColorFont;
                        ApplyEffectiveTypography(layout);
                        SelectedVariantAnalysis = _interop.AnalyzeFontLayout(layout, variant.FontFace);
                    }

                    if (IsGlyphMapMode)
                    {
                        Chars = variant.GetEmoji();
                        HasFontOptions = false;
                    }
                    else
                    {
                        Chars = variant?.GetCharacters();
                        HasFontOptions = SelectedVariantAnalysis.ContainsVectorColorGlyphs || SelectedVariant.HasXamlTypographyFeatures;
                    }

                    ShowColorGlyphs = variant.DirectWriteProperties.IsColorFont;
                }
                else
                {
                    SelectedVariantAnalysis = new CanvasTextLayoutAnalysis();
                    HasFontOptions = false;
                    ShowColorGlyphs = false;
                    ImportButtonEnabled = false;
                }

                SelectedTypography = null;
                SearchResults = null;
                DebounceSearch(SearchQuery, 100);
                IsLoadingCharacters = false;
            }
            catch
            {
                /* 
                 * Hack to avoid crash.
                 * When launching the app by double clicking on a font file when the app is closed,
                 * creating a CanvasTextLayout can fail for some unknown reason. So we retry it.
                 * If we get caught in a never ending loop here, something horrible has occurred.
                 */
                IsLoadingCharacters = false;
                _ = Window.Current.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, async () =>
                {
                    await Task.Delay(100);
                    if (variant == SelectedVariant)
                        LoadChars(variant);
                });
            }
        }

        private void UpdateCharAnalysis()
        {
            if (SelectedChar == null)
            {
                SelectedCharAnalysis = new CanvasTextLayoutAnalysis();
                IsSvgChar = false;
                return;
            }

            using (CanvasTextLayout layout = new CanvasTextLayout(Utils.CanvasDevice, $"{SelectedChar.Char}", new CanvasTextFormat
            {
                FontSize = (float)Core.Converters.GetFontSize(Settings.GridSize),
                FontFamily = SelectedVariant.Source,
                FontStretch = SelectedVariant.FontFace.Stretch,
                FontWeight = SelectedVariant.FontFace.Weight,
                FontStyle = SelectedVariant.FontFace.Style,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
            }, Settings.GridSize, Settings.GridSize))
            {
                layout.Options = CanvasDrawTextOptions.EnableColorFont;
                ApplyEffectiveTypography(layout);
                SelectedCharAnalysis = _interop.AnalyzeCharacterLayout(layout);
            }

            IsSvgChar = SelectedCharAnalysis.GlyphFormats.Contains(GlyphImageFormat.Svg);
        }

        private CanvasTypography GetEffectiveTypography()
        {
            CanvasTypography typo = new CanvasTypography();
            if (SelectedTypography != null && SelectedTypography.Feature != CanvasTypographyFeatureName.None)
            {
                typo.AddFeature(SelectedTypography.Feature, 1u);
            }
            return typo;
        }

        private void ApplyEffectiveTypography(CanvasTextLayout layout)
        {
            using var type = GetEffectiveTypography();
            layout.SetTypography(0, 1, type);
        }

        internal void UpdateDevValues()
        {
            if (SelectedVariant == null || SelectedChar == null || !Settings.ShowDevUtils)
            {
                XamlPath = XamlPathGeom = XamlCode = FontIcon = SymbolIcon = null;
            }
            else
            {
                var data = GlyphService.GetDevValues(SelectedChar, SelectedVariant, SelectedCharAnalysis, GetEffectiveTypography(), Settings.DevToolsLanguage == 0);
                XamlCode = data.Hex;
                FontIcon = data.FontIcon;
                XamlPathGeom = data.Path;
                SymbolIcon = data.Symbol;

                XamlPath = $"{SelectedVariant.FileName}#{SelectedVariant.FamilyName}";
            }
        }

        public void SetDefaultChar()
        {
            if (Chars == null)
                return;

            if (Chars.FirstOrDefault(i => i.UnicodeIndex == Settings.LastSelectedCharIndex)
                            is Character lastSelectedChar
                            && SelectedVariant.FontFace.HasCharacter((uint)lastSelectedChar.UnicodeIndex))
            {
                SelectedChar = lastSelectedChar;
            }
            else
            {
                SelectedChar = Chars?.FirstOrDefault(
                    c => c.UnicodeIndex > -1 && !Windows.Data.Text.UnicodeCharacters.IsWhitespace((uint)c.UnicodeIndex)) ?? Chars.FirstOrDefault();
            }
        }

        public string GetCharName(Character c)
        {
            if (SelectedVariant == null || c == null)
                return null;

            return GlyphService.GetCharacterDescription(c, SelectedVariant, true);
        }

        public string GetCharDescription(Character c)
        {
            if (SelectedVariant == null || c == null)
                return null;

            if (GlyphService.GetCharacterKeystroke(c.UnicodeIndex) is string k)
                return $"{c.UnicodeString} - {k}";
            else
                return c.UnicodeString;
        }

        public void DebounceSearch(string query, int delayMilliseconds = 500, SearchSource from = SearchSource.AutoProperty)
        {
            if (from == SearchSource.AutoProperty && !Settings.UseInstantSearch)
                return;

            if (from == SearchSource.ManualSubmit || delayMilliseconds <= 0)
                Search(query);
            else
                _searchDebouncer.Debounce(delayMilliseconds, () => Search(query));
        }

        internal async void Search(string query)
        {
            var token = _searchTokenFactory.GenerateToken();
            if (await GlyphService.SearchAsync(query, SelectedVariant) is IReadOnlyList<IGlyphData> results
                && token.IsValid())
            {
                SearchResults = results;
            }
        }

        private async Task SavePngAsync(ExportStyle style)
        {
            ExportResult result = await ExportManager.ExportPngAsync(
                style,
                SelectedFont,
                SelectedVariant,
                SelectedChar,
                SelectedCharAnalysis,
                GetEffectiveTypography(),
                Settings);

            if (result.Success)
                MessengerInstance.Send(new AppNotificationMessage(true, result));
        }

        private async Task SaveSvgAsync(ExportStyle style)
        {
            ExportResult result = await ExportManager.ExportSvgAsync(
                style,
                SelectedFont,
                SelectedVariant,
                SelectedChar,
                SelectedCharAnalysis,
                GetEffectiveTypography());

            if (result.Success)
                MessengerInstance.Send(new AppNotificationMessage(true, result));
        }

        public async Task<bool> LoadFromFileArgsAsync(FileActivatedEventArgs args)
        {
            IsExternalFile = true;
            IsLoading = true;
            try
            {
                if (args.Files.FirstOrDefault() is StorageFile file
                    && await FontFinder.LoadFromFileAsync(file) is InstalledFont font)
                {
                    SourceFile = file;
                    IsLoading = false;

                    SelectedFont = font;
                    SetDefaultChar();
                    return true;
                }

                await DialogService.ShowMessage(
                    Localization.Get("InvalidFontMessage"),
                    Localization.Get("InvalidFontTitle"));

                WindowService.CloseForCurrentView();

                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async void ImportFile()
        {
            ImportButtonEnabled = false;

            IsLoading = true;
            try
            {
                var items = new List<StorageFile> { SourceFile };
                if (await FontFinder.ImportFontsAsync(items) is FontImportResult result
                    && (result.Imported.Count > 0 || result.Existing.Count > 0))
                {
                    await WindowService.ActivateMainWindowAsync();
                    await Task.Delay(100);
                    await CoreApplication.MainView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
                    {
                        MessengerInstance.Send(new ImportMessage(result));
                    });
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void OpenSelectedFontInWindow(object sender, RoutedEventArgs e)
        {
            if (SelectedFont is InstalledFont font)
            {
                _ = FontMapView.CreateNewViewForFontAsync(font);
            }
        }
    }

    public enum SearchSource
    {
        AutoProperty,
        ManualSubmit
    }
}
