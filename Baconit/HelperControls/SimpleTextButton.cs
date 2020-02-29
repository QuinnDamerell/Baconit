using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;


namespace Baconit.HelperControls
{
    internal enum SimpleTextState
    {
        Pressed,
        Idle
    }

    /// <summary>
    /// A very simple, yet very performant text button that changes color when tapped.
    /// </summary>
    public sealed class SimpleTextButton : Control
    {
        //
        // UI Elements
        //
        private TextBlock _uiButtonText;
        private Grid _uiContentRoot;
        private Storyboard _mColorStoryboard;
        private ColorAnimation _mColorAnimation;

        //
        // Control Vars
        //
        private SimpleTextState _mState = SimpleTextState.Idle;
        private string _mCurrentButtonText;
        private Color _mAccentColor;
        private readonly Color _mDefaultTextColor = Color.FromArgb(255, 153, 153, 153);
        private Color _mNormalTextColor = Color.FromArgb(255, 153, 153, 153);
        private Duration _mAnimateInDuration;
        private Duration _mAnimateOutDuration;

        public SimpleTextButton()
        {
            DefaultStyleKey = typeof(SimpleTextButton);

            // Register for the loaded and unlaoded events
            Loaded += SimpleTextButton_Loaded;
        }

        #region UI Setup Logic

        private void SimpleTextButton_Loaded(object sender, RoutedEventArgs e)
        {
            // Unregister for loaded events so we don't do this many times.
            Loaded -= SimpleTextButton_Loaded;

            // First, try to get the main root grid.
            var uiElements = new List<DependencyObject>();
            UiControlHelpers<Grid>.RecursivelyFindElement(this, ref uiElements);

            if(uiElements.Count != 1)
            {
                throw new Exception("Found too many or too few grids!");
            }

            // Grab it
            _uiContentRoot = (Grid)uiElements[0];

            // Next try to find the text block
            uiElements.Clear();
            UiControlHelpers<TextBlock>.RecursivelyFindElement(this, ref uiElements);

            if (uiElements.Count != 1)
            {
                throw new Exception("Found too many or too few textblocks!");
            }

            // Grab it
            _uiButtonText = (TextBlock)uiElements[0];

            // If the desired text already exists set it
            if(_mCurrentButtonText != null)
            {
                _uiButtonText.Text = _mCurrentButtonText;                
            }

            // Set the normal text color
            _uiButtonText.Foreground = new SolidColorBrush(_mNormalTextColor);

            // Grab the current accent color
            _mAccentColor = ((SolidColorBrush)Application.Current.Resources["SystemControlBackgroundAccentBrush"]).Color;

            // Next create our storyboards and animations
            _mColorStoryboard = new Storyboard();
            _mColorAnimation = new ColorAnimation();
            _mColorStoryboard.Children.Add(_mColorAnimation);

            // Set them up.
            Storyboard.SetTarget(_mColorStoryboard, _uiButtonText);
            Storyboard.SetTargetProperty(_mColorStoryboard, "(TextBlock.Foreground).(SolidColorBrush.Color)");
            _mAnimateInDuration = new Duration(new TimeSpan(0, 0, 0, 0, 200));
            _mAnimateOutDuration = new Duration(new TimeSpan(0, 0, 0, 0, 400));

            // Last add our events to the grid
            _uiContentRoot.PointerPressed += ContentRoot_PointerPressed;
            _uiContentRoot.Tapped += ContentRoot_Tapped;
            _uiContentRoot.PointerCanceled += ContentRoot_PointerCanceled;
            _uiContentRoot.PointerExited += ContentRoot_PointerExited;
            _uiContentRoot.PointerReleased += ContentRoot_PointerReleased;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Fired when the text is actually tapped.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentRoot_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Raise the event
            _mOnButtonTapped.Raise(this, new EventArgs());

            // Show the animation
            DoAnimation(false);
        }

        private void ContentRoot_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            DoAnimation(false);
        }

        private void ContentRoot_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            DoAnimation(false);
        }

        private void ContentRoot_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            DoAnimation(false);
        }
        private void ContentRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            DoAnimation(true);
        }

        #endregion

        #region Animation

        public void DoAnimation(bool isPress)
        {
            // Check the state
            if(isPress && _mState == SimpleTextState.Pressed)
            {
                return;
            }
            if(!isPress && _mState == SimpleTextState.Idle)
            {
                return;
            }

            // Set the state
            _mState = isPress ? SimpleTextState.Pressed : SimpleTextState.Idle;

            // Animate, the animate out duration is a bit longer to give the user more time to see it
            _mColorAnimation.To = isPress ? _mAccentColor : _mNormalTextColor;
            _mColorAnimation.From = isPress ? _mNormalTextColor : _mAccentColor;
            _mColorStoryboard.Duration = isPress ? _mAnimateInDuration : _mAnimateOutDuration;
            _mColorAnimation.Duration = isPress ? _mAnimateInDuration : _mAnimateOutDuration;
            _mColorStoryboard.Begin();
        }

        #endregion

        #region Button Text Logic

        /// <summary>
        /// This is how we get the text for the button.
        /// </summary>
        public string ButtonText
        {
            get => (string)GetValue(ButtonTextProperty);
            set => SetValue(ButtonTextProperty, value);
        }

        public static readonly DependencyProperty ButtonTextProperty =
            DependencyProperty.Register(
                "ButtonText",                     // The name of the DependencyProperty
                typeof(string),                   // The type of the DependencyProperty
                typeof(SimpleTextButton), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    "",                      // The default value of the DependencyProperty
                    OnButtonTextChangedStatic
                ));

        private static void OnButtonTextChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (SimpleTextButton)d;
            instance?.OnTextButtonChanged((string)e.NewValue);
        }

        /// <summary>
        /// Fired when the text is changed
        /// </summary>
        /// <param name="newText"></param>
        private void OnTextButtonChanged(string newText)
        {
            // Save the text
            _mCurrentButtonText = newText;

            // If the button already exists set the text
            if (_uiButtonText != null)
            {
                _uiButtonText.Text = _mCurrentButtonText;
            }
        }

        #endregion

        #region Forground Color Logic

        /// <summary>
        /// This is how we get the text color for the button.
        /// </summary>
        public SolidColorBrush ForgroundColor
        {
            get => (SolidColorBrush)GetValue(ForgroundColorProperty);
            set => SetValue(ForgroundColorProperty, value);
        }

        public static readonly DependencyProperty ForgroundColorProperty =
            DependencyProperty.Register(
                "ForgroundColor",                     // The name of the DependencyProperty
                typeof(SolidColorBrush),                   // The type of the DependencyProperty
                typeof(SimpleTextButton), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    null,                      // The default value of the DependencyProperty
                    OnForgroundColorChangedStatic
                ));

        private static void OnForgroundColorChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (SimpleTextButton)d;
            instance?.OnForgroundColorChanged((SolidColorBrush)e.NewValue);
        }

        /// <summary>
        /// Fired when the text is changed
        /// </summary>
        /// <param name="newText"></param>
        private void OnForgroundColorChanged(SolidColorBrush newBrush)
        {
            // If the brush is being cleared go with the default.
            if(newBrush == null)
            {
                newBrush = new SolidColorBrush(_mDefaultTextColor);
            }

            // Save the color
            _mNormalTextColor = newBrush.Color;

            // If the text already exists set the color
            if (_uiButtonText != null)
            {
                // Stop the animation if playing
                _mColorStoryboard.Stop();

                // Set the color
                _uiButtonText.Foreground = newBrush;
            }
        }

        #endregion

        #region Tap Handler

        /// <summary>
        /// Fired when the text is tapped
        /// </summary>
        public event EventHandler<EventArgs> OnButtonTapped
        {
            add => _mOnButtonTapped.Add(value);
            remove => _mOnButtonTapped.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<EventArgs>> _mOnButtonTapped = new SmartWeakEvent<EventHandler<EventArgs>>();

        #endregion
    }
}
