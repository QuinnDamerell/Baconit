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

        #region Icon Image

        /// <summary>
        /// This it how we get the symbol text from the xmal binding.
        /// </summary>
        public string IconImageSource
        {
            get { return (string)GetValue(IconImageSourceProperty); }
            set { SetValue(IconImageSourceProperty, value); }
        }

        public static readonly DependencyProperty IconImageSourceProperty =
            DependencyProperty.Register(
                "IconImageSource",                     // The name of the DependencyProperty
                typeof(string),                   // The type of the DependencyProperty
                typeof(CircleIconButton), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    false,                      // The default value of the DependencyProperty
                    new PropertyChangedCallback(OnIconImageSourceChangedStatic)
                ));

        private static void OnIconImageSourceChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as CircleIconButton;
            if (instance != null)
            {
                instance.OnSymbolIconSourceChanged(e.NewValue.GetType() == typeof(string) ? (string)e.NewValue : "");
            }
        }

        #endregion

        #region Setup logic

        public CircleIconButton()
        {
            this.InitializeComponent();
            VisualStateManager.GoToState(this, "ButtonReleased", false);
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

        private void OnSymbolIconSourceChanged(string newText)
        {
            ClearIcon();

            bool isShowing = !String.IsNullOrWhiteSpace(newText);
            if (isShowing)
            {
                ui_symbolImage.Visibility = Visibility.Visible;
                ui_symbolImage.Source = new BitmapImage(new Uri(newText, UriKind.Absolute));
            }
        }

        private void ClearIcon()
        {
            ui_symbolTextBlock.Visibility = Visibility.Collapsed;
            ui_symbolImage.Visibility = Visibility.Visible;
            ui_symbolImage.Source = null;
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
