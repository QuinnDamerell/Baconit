using BaconBackend.Helpers;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    public enum VoteIconStatus
    {
        Up,
        Down
    }

    public sealed partial class CircleIconButton : UserControl
    {
        /// <summary>
        /// Fired when the end of the list is detected.
        /// </summary>
        public event EventHandler<EventArgs> OnIconTapped
        {
            add => _mOnTapped.Add(value);
            remove => _mOnTapped.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<EventArgs>> _mOnTapped = new SmartWeakEvent<EventHandler<EventArgs>>();

        #region Icon Symbol Icon

        /// <summary>
        /// This it how we get the symbol from the xmal binding.
        /// </summary>
        public Symbol SymbolIcon
        {
            get => (Symbol)GetValue(SymbolIconProperty);
            set => SetValue(SymbolIconProperty, value);
        }

        public static readonly DependencyProperty SymbolIconProperty =
            DependencyProperty.Register(
                "SymbolIcon",                     // The name of the DependencyProperty
                typeof(Symbol),                   // The type of the DependencyProperty
                typeof(CircleIconButton), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    Symbol.Emoji2,                      // The default value of the DependencyProperty
                    OnSymbolIconChangedStatic
                ));

        private static void OnSymbolIconChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as CircleIconButton;
            instance?.OnSymbolIconChanged((Symbol)e.NewValue);
        }

        #endregion

        #region Font Icon Glyph

        /// <summary>
        /// This it how we get the font glyph from the xmal binding.
        /// </summary>
        public string FontIconGlyph
        {
            get => (string)GetValue(FontIconGlyphProperty);
            set => SetValue(FontIconGlyphProperty, value);
        }

        public static readonly DependencyProperty FontIconGlyphProperty =
            DependencyProperty.Register(
                "FontIconGlyph",                     // The name of the DependencyProperty
                typeof(string),                   // The type of the DependencyProperty
                typeof(CircleIconButton), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    "",                      // The default value of the DependencyProperty
                    OnFontIconGlyphChangedStatic
                ));

        private static void OnFontIconGlyphChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as CircleIconButton;
            instance?.OnFontIconGlyphChanged((string)e.NewValue);
        }

        #endregion

        #region Icon Vote Status

        /// <summary>
        /// This it how we get the vote status from the xmal binding.
        /// </summary>
        public VoteIconStatus VoteStatus
        {
            get => (VoteIconStatus)GetValue(VoteStatusProperty);
            set => SetValue(VoteStatusProperty, value);
        }

        public static readonly DependencyProperty VoteStatusProperty =
            DependencyProperty.Register(
                "VoteStatus",                     // The name of the DependencyProperty
                typeof(VoteIconStatus),                   // The type of the DependencyProperty
                typeof(CircleIconButton), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    null,                      // The default value of the DependencyProperty
                    OnVoteStatusChangedStatic
                ));

        private static void OnVoteStatusChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as CircleIconButton;
            instance?.OnVoteStatusChanged((VoteIconStatus)e.NewValue);
        }

        #endregion

        #region Icon Vote Brush

        /// <summary>
        /// This it how we get the vote status from the xmal binding.
        /// </summary>
        public SolidColorBrush VoteBrush
        {
            get => (SolidColorBrush)GetValue(VoteBrushProperty);
            set => SetValue(VoteBrushProperty, value);
        }

        public static readonly DependencyProperty VoteBrushProperty =
            DependencyProperty.Register(
                "VoteBrush",                     // The name of the DependencyProperty
                typeof(SolidColorBrush),                   // The type of the DependencyProperty
                typeof(CircleIconButton), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    null,                      // The default value of the DependencyProperty
                    OnVoteBrushChangedStatic
                ));

        private static void OnVoteBrushChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as CircleIconButton;
            instance?.OnVoteBrushChanged((SolidColorBrush)e.NewValue);
        }

        #endregion

        #region Setup logic

        public CircleIconButton()
        {
            InitializeComponent();
            VisualStateManager.GoToState(this, "ButtonReleased", false);
        }

        private void OnVoteBrushChanged(SolidColorBrush newBrush)
        {
            if (newBrush != null)
            {
                ui_voteIconRect.Fill = newBrush;
                ui_voteIconTri.Fill = newBrush;
            }
        }

        private void OnVoteStatusChanged(VoteIconStatus? newStatus)
        {
            ClearIcon();

            if (newStatus.HasValue)
            {
                ui_voteIconGrid.Visibility = Visibility.Visible;
                ui_voteIconAngle.Angle = newStatus.Value == VoteIconStatus.Up ? 0 : 180;
            }
        }

        private void OnSymbolIconChanged(Symbol? newText)
        {
            ClearIcon();

            if(newText.HasValue)
            {
                ui_symbolTextBlock.Visibility = Visibility.Visible;
                ui_symbolTextBlock.Symbol = newText.Value;
            }
        }

        private void OnFontIconGlyphChanged(string newGlyph)
        {
            ClearIcon();

            var isShowing = !string.IsNullOrWhiteSpace(newGlyph);
            if (isShowing)
            {
                ui_fontIcon.Visibility = Visibility.Visible;
                ui_fontIcon.Glyph = newGlyph;
            }
        }

        private void ClearIcon()
        {
            ui_voteIconGrid.Visibility = Visibility.Collapsed;
            ui_symbolTextBlock.Visibility = Visibility.Collapsed;
            ui_fontIcon.Visibility = Visibility.Collapsed;
            ui_fontIcon.Glyph = "";
        }

        private void Icon_Tapped(object sender, TappedRoutedEventArgs e)
        {
            _mOnTapped.Raise(this, new EventArgs());
        }

        #endregion

        #region Animation Logic

        private void Icon_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "ButtonPressed", true);
        }

        private void Icon_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "ButtonReleased", true);
        }

        private void Icon_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "ButtonReleased", true);
        }

        private void Icon_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "ButtonReleased", true);
        }

        private void Icon_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "ButtonReleased", true);
        }

        #endregion

    }
}
