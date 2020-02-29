using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.System.Profile;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

namespace Baconit.HelperControls
{
    public enum SnowyStates
    {
        ClearSkys,
        SuspendedFlakes,
        Flurries
    }

    /// <summary>
    /// A fun little snow control.
    /// </summary>
    public sealed partial class LetItSnow : UserControl
    {
        private const int CMaxQuantum = 6;
        private const int CMinQuantum = 3;
        private const int CMinCompletedStories = 20;

        /// <summary>
        /// Our main random instance
        /// </summary>
        private readonly Random _mRandom;

        /// <summary>
        /// The timer we will use to spawn the snow.
        /// </summary>
        private readonly DispatcherTimer _mFlaker = new DispatcherTimer();

        /// <summary>
        /// The current state of the snow land
        /// </summary>
        public SnowyStates State { get; private set; }

        /// <summary>
        /// The current land size
        /// </summary>
        private Size _mLandSize;

        /// <summary>
        /// How many stories have been completed
        /// </summary>
        private int _mCompletedStories;

        /// <summary>
        /// Used to map storyboards to the flakes
        /// </summary>
        private readonly Dictionary<Storyboard, Ellipse> _mStoryToFlakeMap = new Dictionary<Storyboard, Ellipse>();

        /// <summary>
        /// We use this on phone so our snowflakes overlap the status bar.
        /// </summary>
        private readonly int _mStatusBarOffset;

        public LetItSnow()
        {
            InitializeComponent();

            // Setup Random
            _mRandom = new Random((int)DateTime.Now.Ticks);

            // Setup the timer that will spawn our snow
            _mFlaker.Tick += SnowStarter_Tick;
            _mFlaker.Interval = new TimeSpan(0, 0, 0, 0, 0);

            // Set our state
            State = SnowyStates.ClearSkys;

            // Set the initial size
            _mLandSize = new Size(ActualWidth, ActualHeight);

            // If we are a phone offset by 40 so we also overlap the status bar, it looks a lot better.
            if(AnalyticsInfo.VersionInfo.DeviceFamily.Equals("Windows.Mobile"))
            {
                _mStatusBarOffset = 40;
            }
        }

        private void SnowStarter_Tick(object sender, object e)
        {
            // Pick a quantum for this flake. If the quantum is higher it might be
            // bigger and brighter.
            var flakeQuantum = _mRandom.Next(CMinQuantum, CMaxQuantum);

            // Spawn a new snow flake
            var flake = new Ellipse();
            flake.Width = flakeQuantum * 2;
            flake.Height = flakeQuantum * 2;
            flake.Fill = new SolidColorBrush(Color.FromArgb((byte)_mRandom.Next(20, flakeQuantum * 12), 255, 255, 255));
            Canvas.SetLeft(flake, _mRandom.Next(0, (int)(_mLandSize.Width - flake.Width)));

            // Add the flake to the UI
            ui_snowLand.Children.Add(flake);

            // Setup the animation
            var flakeStory = new Storyboard();
            var animation = new DoubleAnimation();
            flakeStory.Children.Add(animation);
            Storyboard.SetTarget(animation, flake);
            Storyboard.SetTargetProperty(animation, "(Canvas.Top)");
            flakeStory.Completed += FlakeStory_Completed;

            // Add the animation to our list.
            lock (_mStoryToFlakeMap)
            {
                _mStoryToFlakeMap.Add(flakeStory, flake);
            }

            // Set the animation time, for larger flakes we want to have shorter times
            // to make them feel closer to the user. So use the inverse quantum for the
            // max time.
            var inverseQuantom = CMaxQuantum - flakeQuantum + CMinQuantum;
            var seconds = _mRandom.Next(40, inverseQuantom * 20);
            animation.Duration = new Duration(new TimeSpan(0, 0, seconds));

            // Set the animation params
            animation.From = - flake.Height - _mStatusBarOffset;
            animation.To = _mLandSize.Height;

            // Start it!
            flakeStory.Begin();

            // Randomly pick a new time to wake up.
            _mFlaker.Interval = new TimeSpan(0, 0, 0, 0, _mRandom.Next(300, 500));
        }

        /// <summary>
        /// Fired when a flake is done
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlakeStory_Completed(object sender, object e)
        {
            var flakeStory = (Storyboard)sender;

            // If we can get the flak give it a new X cord for looks.
            if(_mStoryToFlakeMap.ContainsKey(flakeStory))
            {
                var flake = _mStoryToFlakeMap[flakeStory];
                Canvas.SetLeft(flake, _mRandom.Next(0, (int)(_mLandSize.Width - flake.Width)));
            }

            // Restart the animation so it loops the flake
            flakeStory.Begin();

            // When we hit our max completed stories stop the spawner.
            if (_mCompletedStories < CMinCompletedStories)
            {
                _mCompletedStories++;
            }
            else
            {
                _mFlaker.Stop();
            }
        }

        /// <summary>
        /// Called to make it snow.
        /// </summary>
        public void MakeItSnow()
        {
            lock(this)
            {
                if(State != SnowyStates.ClearSkys)
                {
                    return;
                }

                // Set the state
                State = SnowyStates.Flurries;
            }

            // Start the snow!
            _mFlaker.Start();
        }

        /// <summary>
        /// Suspends the snow falling.
        /// </summary>
        public void AllOfTheSnowIsNowBlackSlushPlsSuspendIt()
        {
            lock (this)
            {
                if (State == SnowyStates.Flurries)
                {
                    _mFlaker.Stop();
                    ToggleGravity(false);
                    State = SnowyStates.SuspendedFlakes;
                }
            }
        }

        /// <summary>
        /// Resumes the snow fall if it was started.
        /// </summary>
        public void OkNowIWantMoreSnowIfItHasBeenStarted()
        {
            lock (this)
            {
                if (State == SnowyStates.SuspendedFlakes)
                {
                    ToggleGravity(true);

                    // If we don't have enough completes resume the flaker.
                    if (_mCompletedStories < CMinCompletedStories)
                    {
                        _mFlaker.Start();
                    }

                    State = SnowyStates.Flurries;
                }
            }
        }

        /// <summary>
        /// Called to stop the snow when i get too annoying.
        /// </summary>
        public void ToggleGravity(bool turnOn)
        {
            lock (_mStoryToFlakeMap)
            {
                foreach(var storyToFlake in _mStoryToFlakeMap)
                {
                    if(turnOn)
                    {
                        storyToFlake.Key.Resume();
                    }
                    else
                    {
                        storyToFlake.Key.Pause();
                    }
                }
            }
        }

        /// <summary>
        /// Fired when the control size is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SnowLand_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _mLandSize = e.NewSize;
            Clip = new RectangleGeometry { Rect = new Rect(0,-_mStatusBarOffset, _mLandSize.Width,_mLandSize.Height + _mStatusBarOffset) };
        }
    }
}
