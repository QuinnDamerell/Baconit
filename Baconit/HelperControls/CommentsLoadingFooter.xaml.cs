using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    public sealed partial class CommentsLoadingFooter : UserControl
    {
        public CommentsLoadingFooter()
        {
            InitializeComponent();
        }

        #region ShowLoading Logic

        /// <summary>
        /// Get the ShowLoading Visibility from the XAML Binding
        /// </summary>
        public Visibility ShowLoading
        {
            get => (Visibility)GetValue(ShowLoadingProperty);
            set => SetValue(ShowLoadingProperty, value);
        }
        private Visibility _mShowLoading = Visibility.Collapsed;

        public static readonly DependencyProperty ShowLoadingProperty =
            DependencyProperty.Register(
                "ShowLoading",                     // The name of the DependencyProperty
                typeof(Visibility),                   // The type of the DependencyProperty
                typeof(CommentsLoadingFooter), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    Visibility.Collapsed,                      // The default value of the DependencyProperty
                    OnShowLoadingChangedStatic
                ));

        private static void OnShowLoadingChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as CommentsLoadingFooter;
            if (instance != null)
            {
                var newVis = (Visibility)e.NewValue;
                if (instance._mShowLoading != newVis)
                {
                    instance._mShowLoading = newVis;
                    instance.OnShowLoadingChanged(newVis);
                }
            }
        }

        #endregion

        #region ShowError Logic

        /// <summary>
        /// Get the ShowLoading Visibility from the XAML Binding
        /// </summary>
        public string ShowErrorText
        {
            get => (string)GetValue(ShowErrorTextProperty);
            set => SetValue(ShowErrorTextProperty, value);
        }

        public static readonly DependencyProperty ShowErrorTextProperty =
            DependencyProperty.Register(
                "ShowErrorText",                     // The name of the DependencyProperty
                typeof(string),                   // The type of the DependencyProperty
                typeof(CommentsLoadingFooter), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    "",                      // The default value of the DependencyProperty
                    OnShowErrorTextChangedStatic
                ));

        private static void OnShowErrorTextChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as CommentsLoadingFooter;
            instance?.OnShowErrorTextChanged((string)e.NewValue);
        }

        #endregion

        /// <summary>
        /// Animates the loading message in or out
        /// </summary>
        /// <param name="newVisibility"></param>
        private void OnShowLoadingChanged(Visibility newVisibility)
        {
            ui_progressRing.IsActive = newVisibility == Visibility.Visible;
            ui_progressRing.Visibility = Visibility.Visible;
            ui_textBlock.Text = "Loading Comments";
            PlayAnimation(newVisibility);
        }

        /// <summary>
        /// Shows the error message.
        /// </summary>
        /// <param name="newString"></param>
        private void OnShowErrorTextChanged(string newString)
        {
            // Hide the progress bar
            ui_progressRing.IsActive = false;
            ui_progressRing.Visibility = Visibility.Collapsed;

            // Set the text
            ui_textBlock.Text = newString;

            // Animate
            PlayAnimation(string.IsNullOrWhiteSpace(newString) ? Visibility.Collapsed : Visibility.Visible);
        }

        private void PlayAnimation(Visibility newVisibility)
        {
            anim_MoveLoadingFade.To = newVisibility == Visibility.Visible ? 1 : 0;
            anim_MoveLoadingFade.From = newVisibility == Visibility.Visible ? 0 : 1;
            anim_MoveLoadingTranslate.To = newVisibility == Visibility.Visible ? 0 : 80;
            anim_MoveLoadingTranslate.From = newVisibility == Visibility.Visible ? 80 : 0;
            story_MoveLoading.Begin();
        }
    }
}
