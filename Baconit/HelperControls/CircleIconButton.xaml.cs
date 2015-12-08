using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

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
            add { m_onTapped.Add(value); }
            remove { m_onTapped.Remove(value); }
        }
        SmartWeakEvent<EventHandler<EventArgs>> m_onTapped = new SmartWeakEvent<EventHandler<EventArgs>>();

        #region Icon Symbol Icon

        /// <summary>
        /// This it how we get the symbol from the xmal binding.
        /// </summary>
        public Symbol SymbolIcon
        {
            get { return (Symbol)GetValue(SymbolIconProperty); }
            set { SetValue(SymbolIconProperty, value); }
        }

        public static readonly DependencyProperty SymbolIconProperty =
            DependencyProperty.Register(
                "SymbolIcon",                     // The name of the DependencyProperty
                typeof(Symbol),                   // The type of the DependencyProperty
                typeof(CircleIconButton), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    false,                      // The default value of the DependencyProperty
                    new PropertyChangedCallback(OnSymbolIconChangedStatic)
                ));

        private static void OnSymbolIconChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as CircleIconButton;
            if (instance != null)
            {
                Symbol? newSymbol = null;
                if(e.NewValue.GetType() == typeof(Symbol))
                {
                    newSymbol = (Symbol)e.NewValue;
                }
                instance.OnSymbolIconChanged(newSymbol);
            }
        }

        #endregion

        #region Font Icon Glyph

        /// <summary>
        /// This it how we get the font glyph from the xmal binding.
        /// </summary>
        public string FontIconGlyph
        {
            get { return (string)GetValue(FontIconGlyphProperty); }
            set { SetValue(FontIconGlyphProperty, value); }
        }

        public static readonly DependencyProperty FontIconGlyphProperty =
            DependencyProperty.Register(
                "FontIconGlyph",                     // The name of the DependencyProperty
                typeof(Symbol),                   // The type of the DependencyProperty
                typeof(CircleIconButton), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    "",                      // The default value of the DependencyProperty
                    new PropertyChangedCallback(OnFontIconGlyphChangedStatic)
                ));

        private static void OnFontIconGlyphChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as CircleIconButton;
            if (instance != null)
            {
                instance.OnFontIconGlyphChanged((string)e.NewValue);
            }
        }

        #endregion

        #region Icon Vote Status

        /// <summary>
        /// This it how we get the vote status from the xmal binding.
        /// </summary>
        public VoteIconStatus VoteStatus
        {
            get { return (VoteIconStatus)GetValue(VoteStatusProperty); }
            set { SetValue(VoteStatusProperty, value); }
        }

        public static readonly DependencyProperty VoteStatusProperty =
            DependencyProperty.Register(
                "VoteStatus",                     // The name of the DependencyProperty
                typeof(VoteIconStatus),                   // The type of the DependencyProperty
                typeof(CircleIconButton), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    false,                      // The default value of the DependencyProperty
                    new PropertyChangedCallback(OnVoteStatusChangedStatic)
                ));

        private static void OnVoteStatusChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as CircleIconButton;
            if (instance != null)
            {
                VoteIconStatus? newStatus = null;
                if (e.NewValue.GetType() == typeof(VoteIconStatus))
                {
                    newStatus = (VoteIconStatus)e.NewValue;
                }
                instance.OnVoteStatusChanged(newStatus);
            }
        }

        #endregion

        #region Icon Vote Brush

        /// <summary>
        /// This it how we get the vote status from the xmal binding.
        /// </summary>
        public SolidColorBrush VoteBrush
        {
            get { return (SolidColorBrush)GetValue(VoteBrushProperty); }
            set { SetValue(VoteBrushProperty, value); }
        }

        public static readonly DependencyProperty VoteBrushProperty =
            DependencyProperty.Register(
                "VoteBrush",                     // The name of the DependencyProperty
                typeof(SolidColorBrush),                   // The type of the DependencyProperty
                typeof(CircleIconButton), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    false,                      // The default value of the DependencyProperty
                    new PropertyChangedCallback(OnVoteBrushChangedStatic)
                ));

        private static void OnVoteBrushChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as CircleIconButton;
            if (instance != null)
            {
                SolidColorBrush newBrush = null;
                if (e.NewValue.GetType() == typeof(SolidColorBrush))
                {
                    newBrush = (SolidColorBrush)e.NewValue;
                }
                instance.OnVoteBrushChanged(newBrush);
            }
        }

        #endregion

        #region Setup logic

        public CircleIconButton()
        {
            this.InitializeComponent();
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

            bool isShowing = !String.IsNullOrWhiteSpace(newGlyph);
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
            m_onTapped.Raise(this, new EventArgs());
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
