using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;


namespace Baconit.HelperControls
{
    enum SimpleTextState
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
        TextBlock ui_buttonText = null;
        Grid ui_contentRoot = null;
        Storyboard m_colorStoryboard;
        ColorAnimation m_colorAnimation;

        //
        // Control Vars
        //
        SimpleTextState m_state = SimpleTextState.Idle;
        string m_currentButtonText = null;
        Color m_accentColor;
        Color m_defaultTextColor = Color.FromArgb(255, 153, 153, 153);
        Color m_normalTextColor = Color.FromArgb(255, 153, 153, 153);
        Duration m_animateInDuration;
        Duration m_animateOutDuration;

        public SimpleTextButton()
        {
            this.DefaultStyleKey = typeof(SimpleTextButton);

            // Register for the loaded and unlaoded events
            Loaded += SimpleTextButton_Loaded;
        }

        #region UI Setup Logic

        private void SimpleTextButton_Loaded(object sender, RoutedEventArgs e)
        {
            // Unregister for loaded events so we don't do this many times.
            Loaded -= SimpleTextButton_Loaded;

            // First, try to get the main root grid.
            List<DependencyObject> uiElements = new List<DependencyObject>();
            UiControlHelpers<Grid>.RecursivelyFindElement(this, ref uiElements);

            if(uiElements.Count != 1)
            {
                throw new Exception("Found too many or too few grids!");
            }

            // Grab it
            ui_contentRoot = (Grid)uiElements[0];

            // Next try to find the text block
            uiElements.Clear();
            UiControlHelpers<TextBlock>.RecursivelyFindElement(this, ref uiElements);

            if (uiElements.Count != 1)
            {
                throw new Exception("Found too many or too few textblocks!");
            }

            // Grab it
            ui_buttonText = (TextBlock)uiElements[0];

            // If the desired text already exists set it
            if(m_currentButtonText != null)
            {
                ui_buttonText.Text = m_currentButtonText;                
            }

            // Set the normal text color
            ui_buttonText.Foreground = new SolidColorBrush(m_normalTextColor);

            // Grab the current accent color
            m_accentColor = ((SolidColorBrush)Application.Current.Resources["SystemControlBackgroundAccentBrush"]).Color;

            // Next create our storyboards and animations
            m_colorStoryboard = new Storyboard();
            m_colorAnimation = new ColorAnimation();
            m_colorStoryboard.Children.Add(m_colorAnimation);

            // Set them up.
            Storyboard.SetTarget(m_colorStoryboard, ui_buttonText);
            Storyboard.SetTargetProperty(m_colorStoryboard, "(TextBlock.Foreground).(SolidColorBrush.Color)");
            m_animateInDuration = new Duration(new TimeSpan(0, 0, 0, 0, 200));
            m_animateOutDuration = new Duration(new TimeSpan(0, 0, 0, 0, 400));

            // Last add our events to the grid
            ui_contentRoot.PointerPressed += ContentRoot_PointerPressed;
            ui_contentRoot.Tapped += ContentRoot_Tapped;
            ui_contentRoot.PointerCanceled += ContentRoot_PointerCanceled;
            ui_contentRoot.PointerExited += ContentRoot_PointerExited;
            ui_contentRoot.PointerReleased += ContentRoot_PointerReleased;
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
            m_onButtonTapped.Raise(this, new EventArgs());

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
            if(isPress && m_state == SimpleTextState.Pressed)
            {
                return;
            }
            if(!isPress && m_state == SimpleTextState.Idle)
            {
                return;
            }

            // Set the state
            m_state = isPress ? SimpleTextState.Pressed : SimpleTextState.Idle;

            // Animate, the animate out duration is a bit longer to give the user more time to see it
            m_colorAnimation.To = isPress ? m_accentColor : m_normalTextColor;
            m_colorAnimation.From = isPress ? m_normalTextColor : m_accentColor;
            m_colorStoryboard.Duration = isPress ? m_animateInDuration : m_animateOutDuration;
            m_colorAnimation.Duration = isPress ? m_animateInDuration : m_animateOutDuration;
            m_colorStoryboard.Begin();
        }

        #endregion

        #region Button Text Logic

        /// <summary>
        /// This is how we get the text for the button.
        /// </summary>
        public string ButtonText
        {
            get { return (string)GetValue(ButtonTextProperty); }
            set { SetValue(ButtonTextProperty, value); }
        }

        public static readonly DependencyProperty ButtonTextProperty =
            DependencyProperty.Register(
                "ButtonText",                     // The name of the DependencyProperty
                typeof(string),                   // The type of the DependencyProperty
                typeof(SimpleTextButton), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    false,                      // The default value of the DependencyProperty
                    new PropertyChangedCallback(OnButtonTextChangedStatic)
                ));

        private static void OnButtonTextChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (SimpleTextButton)d;
            if (instance != null)
            {
                instance.OnTextButtonChanged(e.NewValue.GetType() == typeof(string) ? (string)e.NewValue : "");
            }
        }

        /// <summary>
        /// Fired when the text is changed
        /// </summary>
        /// <param name="newText"></param>
        private void OnTextButtonChanged(string newText)
        {
            // Save the text
            m_currentButtonText = newText;

            // If the button already exists set the text
            if (ui_buttonText != null)
            {
                ui_buttonText.Text = m_currentButtonText;
            }
        }

        #endregion

        #region Forground Color Logic

        /// <summary>
        /// This is how we get the text color for the button.
        /// </summary>
        public SolidColorBrush ForgroundColor
        {
            get { return (SolidColorBrush)GetValue(ForgroundColorProperty); }
            set { SetValue(ForgroundColorProperty, value); }
        }

        public static readonly DependencyProperty ForgroundColorProperty =
            DependencyProperty.Register(
                "ForgroundColor",                     // The name of the DependencyProperty
                typeof(SolidColorBrush),                   // The type of the DependencyProperty
                typeof(SimpleTextButton), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    false,                      // The default value of the DependencyProperty
                    new PropertyChangedCallback(OnForgroundColorChangedStatic)
                ));

        private static void OnForgroundColorChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (SimpleTextButton)d;
            if (instance != null)
            {
                instance.OnForgroundColorChanged(e.NewValue.GetType() == typeof(SolidColorBrush) ? (SolidColorBrush)e.NewValue : null);
            }
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
                newBrush = new SolidColorBrush(m_defaultTextColor);
            }

            // Save the color
            m_normalTextColor = newBrush.Color;

            // If the text already exists set the color
            if (ui_buttonText != null)
            {
                // Stop the animation if playing
                m_colorStoryboard.Stop();

                // Set the color
                ui_buttonText.Foreground = newBrush;
            }
        }

        #endregion

        #region Tap Handler

        /// <summary>
        /// Fired when the text is tapped
        /// </summary>
        public event EventHandler<EventArgs> OnButtonTapped
        {
            add { m_onButtonTapped.Add(value); }
            remove { m_onButtonTapped.Remove(value); }
        }
        SmartWeakEvent<EventHandler<EventArgs>> m_onButtonTapped = new SmartWeakEvent<EventHandler<EventArgs>>();

        #endregion
    }
}
