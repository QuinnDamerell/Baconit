using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Phone.Devices.Notification;
using Windows.System.Profile;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
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
        const int c_maxQuantum = 6;
        const int c_minQuantum = 3;
        const int c_minCompletedStories = 20;

        /// <summary>
        /// Our main random instance
        /// </summary>
        Random m_random;

        /// <summary>
        /// The timer we will use to spawn the snow.
        /// </summary>
        DispatcherTimer m_flaker = new DispatcherTimer();

        /// <summary>
        /// The current state of the snow land
        /// </summary>
        public SnowyStates State { get; private set; }

        /// <summary>
        /// The current land size
        /// </summary>
        Size m_landSize;

        /// <summary>
        /// How many stories have been completed
        /// </summary>
        int m_completedStories = 0;

        /// <summary>
        /// Used to map storyboards to the flakes
        /// </summary>
        Dictionary<Storyboard, Ellipse> m_storyToFlakeMap = new Dictionary<Storyboard, Ellipse>();

        /// <summary>
        /// We use this on phone so our snowflakes overlap the status bar.
        /// </summary>
        int m_statusBarOffset = 0;

        public LetItSnow()
        {
            this.InitializeComponent();

            // Setup Random
            m_random = new Random((int)DateTime.Now.Ticks);

            // Setup the timer that will spawn our snow
            m_flaker.Tick += SnowStarter_Tick;
            m_flaker.Interval = new TimeSpan(0, 0, 0, 0, 0);

            // Set our state
            State = SnowyStates.ClearSkys;

            // Set the initial size
            m_landSize = new Size(this.ActualWidth, this.ActualHeight);

            // If we are a phone offset by 40 so we also overlap the status bar, it looks a lot better.
            if(AnalyticsInfo.VersionInfo.DeviceFamily.Equals("Windows.Mobile"))
            {
                m_statusBarOffset = 40;
            }
        }

        private void SnowStarter_Tick(object sender, object e)
        {
            // Pick a quantum for this flake. If the quantum is higher it might be
            // bigger and brighter.
            int flakeQuantum = m_random.Next(c_minQuantum, c_maxQuantum);

            // Spawn a new snow flake
            Ellipse flake = new Ellipse();
            flake.Width = flakeQuantum * 2;
            flake.Height = flakeQuantum * 2;
            flake.Fill = new SolidColorBrush(Color.FromArgb((byte)m_random.Next(20, flakeQuantum * 12), 255, 255, 255));
            Canvas.SetLeft(flake, m_random.Next(0, (int)(m_landSize.Width - flake.Width)));

            // Add the flake to the UI
            ui_snowLand.Children.Add(flake);

            // Setup the animation
            Storyboard flakeStory = new Storyboard();
            DoubleAnimation animation = new DoubleAnimation();
            flakeStory.Children.Add(animation);
            Storyboard.SetTarget(animation, flake);
            Storyboard.SetTargetProperty(animation, "(Canvas.Top)");
            flakeStory.Completed += FlakeStory_Completed;

            // Add the animation to our list.
            lock (m_storyToFlakeMap)
            {
                m_storyToFlakeMap.Add(flakeStory, flake);
            }

            // Set the animation time, for larger flakes we want to have shorter times
            // to make them feel closer to the user. So use the inverse quantum for the
            // max time.
            int inverseQuantom = c_maxQuantum - flakeQuantum + c_minQuantum;
            int seconds = m_random.Next(40, inverseQuantom * 20);
            animation.Duration = new Duration(new TimeSpan(0, 0, seconds));

            // Set the animation params
            animation.From = - flake.Height - m_statusBarOffset;
            animation.To = m_landSize.Height;

            // Start it!
            flakeStory.Begin();

            // Randomly pick a new time to wake up.
            m_flaker.Interval = new TimeSpan(0, 0, 0, 0, m_random.Next(300, 500));
        }

        /// <summary>
        /// Fired when a flake is done
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlakeStory_Completed(object sender, object e)
        {
            Storyboard flakeStory = (Storyboard)sender;

            // If we can get the flak give it a new X cord for looks.
            if(m_storyToFlakeMap.ContainsKey(flakeStory))
            {
                Ellipse flake = m_storyToFlakeMap[flakeStory];
                Canvas.SetLeft(flake, m_random.Next(0, (int)(m_landSize.Width - flake.Width)));
            }

            // Restart the animation so it loops the flake
            flakeStory.Begin();

            // When we hit our max completed stories stop the spawner.
            if (m_completedStories < c_minCompletedStories)
            {
                m_completedStories++;
            }
            else
            {
                m_flaker.Stop();
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
            m_flaker.Start();
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
                    m_flaker.Stop();
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
                    if (m_completedStories < c_minCompletedStories)
                    {
                        m_flaker.Start();
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
            lock (m_storyToFlakeMap)
            {
                foreach(KeyValuePair<Storyboard, Ellipse> storyToFlake in m_storyToFlakeMap)
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
            m_landSize = e.NewSize;
            Clip = new RectangleGeometry() { Rect = new Rect(0,-m_statusBarOffset, m_landSize.Width,m_landSize.Height + m_statusBarOffset) };
        }
    }
}
