using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using NotificationsExtensions.Badges;
using NotificationsExtensions.Tiles;
using NotificationsExtensions.Toasts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Notifications;

namespace BaconBackend.Managers.Background
{
    public class BackgroundMessageUpdater
    {
        public const string c_messageInboxOpenArgument = "goToInbox";

        BaconManager m_baconMan;
        MessageCollector m_collector;
        RefCountedDeferral m_refDeferral;

        public BackgroundMessageUpdater(BaconManager baconMan)
        {
            m_baconMan = baconMan;
        }

        /// <summary>
        /// Checks if we should update and if so does so.
        /// </summary>
        /// <param name="refDeferral"></param>
        /// <param name="force"></param>
        public void RunUpdate(RefCountedDeferral refDeferral, bool force = false)
        {
            if (IsEnabled && m_baconMan.UserMan.IsUserSignedIn)
            {
                TimeSpan timeSinceLastRun = DateTime.Now - LastUpdateTime;
                if (timeSinceLastRun.TotalMinutes > 10 || force)
                {
                    // We should update. Grab the deferral
                    m_refDeferral = refDeferral;
                    m_refDeferral.AddRef();

                    // Make the collector
                    m_collector = new MessageCollector(m_baconMan);

                    // We don't need to sub to the collection update because we will get
                    // called automatically when it updates
                    m_collector.OnCollectorStateChange += Collector_OnCollectorStateChange;

                    // Tell it to update!
                    m_collector.Update(true);
                }
            }
        }

        /// <summary>
        /// Updates all of the notifications based on the message list.
        /// </summary>
        /// <param name="newMessages"></param>
        public async void UpdateNotifications(List<Message> newMessages)
        {
            bool updateSliently = !m_baconMan.IsBackgroundTask;

            // Check if we are disabled.
            if (!IsEnabled || !m_baconMan.UserMan.IsUserSignedIn)
            {
                // Clear things out
                ToastNotificationManager.History.Clear();
                // Clear the tile
                BadgeNumericNotificationContent content = new BadgeNumericNotificationContent();
                content.Number = 0;
                BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(new BadgeNotification(content.GetXml()));
                return;
            }

            try
            {
                // Get the current state of the messages.
                IReadOnlyList<ToastNotification> history = ToastNotificationManager.History.GetHistory();

                // We need to keep track of anything new to send to the band.
                List<Tuple<string, string, string>> newNotifications = new List<Tuple<string, string, string>>();

                // We also need to keep track of how many are unread so we can updates our badge.
                int unreadCount = 0;

                if (NotificationType == 0)
                {
                    // For help look here.
                    // http://blogs.msdn.com/b/tiles_and_toasts/archive/2015/07/09/quickstart-sending-a-local-toast-notification-and-handling-activations-from-it-windows-10.aspx

                    // We need to keep track of what notifications we should have so we can remove ones we don't need.
                    Dictionary<string, bool> unReadMessages = new Dictionary<string, bool>();

                    foreach (Message message in newMessages.Reverse<Message>())
                    {
                        // If it is new
                        if (message.IsNew)
                        {
                            unreadCount++;

                            // Add the message to our list
                            unReadMessages.Add(message.Id, true);

                            // Check if it is already been reported but dismissed from the UI
                            if (ShownMessageNotifications.ContainsKey(message.Id))
                            {
                                continue;
                            }

                            // If not add that we are showing it.
                            ShownMessageNotifications[message.Id] = true;

                            // Check if it is already in the UI
                            foreach (ToastNotification oldToast in history)
                            {
                                if (oldToast.Tag.Equals(message.Id))
                                {
                                    continue;
                                }
                            }

                            // Get the post title.
                            string title = "";
                            if (message.WasComment)
                            {
                                string subject = message.Subject;
                                if (subject.Length > 0)
                                {
                                    subject = subject.Substring(0, 1).ToUpper() + subject.Substring(1);
                                }
                                title = subject + " from " + message.Author;
                            }
                            else
                            {
                                title = message.Subject;
                            }

                            // Get the body
                            string body = message.Body;

                            // Add the notification to our list
                            newNotifications.Add(new Tuple<string, string, string>(title, body, message.Id));
                        }
                    }

                    // Make sure that all of the messages in our history are still unread
                    // if not remove them.
                    for(int i = 0; i < history.Count; i++)
                    {
                        if(!unReadMessages.ContainsKey(history[i].Tag))
                        {
                            // This message isn't unread any longer.
                            ToastNotificationManager.History.Remove(history[i].Tag);
                        }
                    }

                    // Save any settings we changed
                    SaveSettings();
                }
                else
                {
                    // Count how many are unread
                    foreach (Message message in newMessages)
                    {
                        if (message.IsNew)
                        {
                            unreadCount++;
                        }
                    }

                    // If we have a different unread count now show the notification.
                    if(LastKnownUnreadCount != unreadCount)
                    {
                        // Update the notification.
                        LastKnownUnreadCount = unreadCount;

                        // Clear anything we have in the notification center already.
                        ToastNotificationManager.History.Clear();

                        if (unreadCount != 0)
                        {
                            newNotifications.Add(new Tuple<string, string, string>($"You have {unreadCount} new inbox message" + (unreadCount == 1 ? "." : "s."), "", "totalCount"));
                        }
                    }              
                }

                // For every notification, show it.
                bool hasShownNote = false;
                foreach(Tuple<string, string, string> newNote in newNotifications)
                {
                    // Make the visual
                    ToastVisual visual = new ToastVisual();
                    visual.TitleText = new ToastText() { Text = newNote.Item1 };
                    if(!String.IsNullOrWhiteSpace(newNote.Item2))
                    {
                        visual.BodyTextLine1 = new ToastText() { Text = newNote.Item2 };
                    }

                    // Make the toast content
                    ToastContent toastContent = new ToastContent();
                    toastContent.Visual = visual;
                    toastContent.Launch = c_messageInboxOpenArgument;
                    toastContent.ActivationType = ToastActivationType.Foreground;
                    toastContent.Duration = ToastDuration.Short;

                    var toast = new ToastNotification(toastContent.GetXml());
                    toast.Tag = newNote.Item3;

                    // Only show if we should and this is the first message to show.
                    toast.SuppressPopup = hasShownNote || updateSliently || AddToNotificationCenterSilently;
                    ToastNotificationManager.CreateToastNotifier().Show(toast);

                    // Mark that we have shown one.
                    hasShownNote = true;
                }

                // Make sure the main tile is an iconic tile.
                m_baconMan.TileMan.EnsureMainTileIsIconic();

                // Update the badge
                BadgeNumericNotificationContent content = new BadgeNumericNotificationContent();
                content.Number = (uint)unreadCount;
                BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(new BadgeNotification(content.GetXml()));

                // Update the band if we have one.
                if(!updateSliently)
                {
                    await m_baconMan.BackgroundMan.BandMan.UpdateInboxMessages(newNotifications, newMessages);
                }

                // If all was successful update the last time we updated
                LastUpdateTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "messageUpdaterFailed", ex);
                m_baconMan.MessageMan.DebugDia("failed to update message notifications", ex);
            }

            // When we are done release the deferral
            ReleaseDeferal();
        }

        /// <summary>
        /// Fired when the collector state changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectorStateChange(object sender, OnCollectorStateChangeArgs e)
        {
            if(e.State == CollectorState.Error)
            {
                // If we get an error we are done.
                ReleaseDeferal();
            }
        }

        /// <summary>
        /// Releases the deferral if it is held.
        /// </summary>
        private void ReleaseDeferal()
        {
            if(m_refDeferral != null)
            {
                m_refDeferral.ReleaseRef();
            }
            m_refDeferral = null;
        }

        #region Vars

        /// <summary>
        /// Indicates if background message updates are enabled.
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                if (!m_isEnabled.HasValue)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("BackgroundMessageUpdater.IsEnabled"))
                    {
                        m_isEnabled = m_baconMan.SettingsMan.ReadFromRoamingSettings<bool>("BackgroundMessageUpdater.IsEnabled");
                    }
                    else
                    {
                        m_isEnabled = true;
                    }
                }
                return m_isEnabled.Value;
            }
            set
            {
                m_isEnabled = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<bool>("BackgroundMessageUpdater.IsEnabled", m_isEnabled.Value);
            }
        }
        private bool? m_isEnabled = null;

        /// <summary>
        /// Indicates if we should add notification silently.
        /// </summary>
        public bool AddToNotificationCenterSilently
        {
            get
            {
                if (!m_addSilently.HasValue)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("BackgroundMessageUpdater.AddToNotificationCenterSilently"))
                    {
                        m_addSilently = m_baconMan.SettingsMan.ReadFromRoamingSettings<bool>("BackgroundMessageUpdater.AddToNotificationCenterSilently");
                    }
                    else
                    {
                        m_addSilently = false;
                    }
                }
                return m_addSilently.Value;
            }
            set
            {
                m_addSilently = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<bool>("BackgroundMessageUpdater.AddToNotificationCenterSilently", m_addSilently.Value);
            }
        }
        private bool? m_addSilently = null;

        /// <summary>
        /// Indicates what type of notifications the user wants.
        /// </summary>
        public int NotificationType
        {
            get
            {
                if (!m_notificationType.HasValue)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("BackgroundMessageUpdater.NotificationType"))
                    {
                        m_notificationType = m_baconMan.SettingsMan.ReadFromRoamingSettings<int>("BackgroundMessageUpdater.NotificationType");
                    }
                    else
                    {
                        m_notificationType = 0;
                    }
                }
                return m_notificationType.Value;
            }
            set
            {
                m_notificationType = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<int>("BackgroundMessageUpdater.NotificationType", m_notificationType.Value);
            }
        }
        private int? m_notificationType = null;


        /// <summary>
        /// Last known unread count
        /// </summary>
        public int LastKnownUnreadCount
        {
            get
            {
                if (!m_lastKnownUnreadCount.HasValue)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundMessageUpdater.LastKnownUnreadCount"))
                    {
                        m_lastKnownUnreadCount = m_baconMan.SettingsMan.ReadFromLocalSettings<int>("BackgroundMessageUpdater.LastKnownUnreadCount");
                    }
                    else
                    {
                        m_lastKnownUnreadCount = 0;
                    }
                }
                return m_lastKnownUnreadCount.Value;
            }
            set
            {
                m_lastKnownUnreadCount = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<int>("BackgroundMessageUpdater.LastKnownUnreadCount", m_lastKnownUnreadCount.Value);
            }
        }
        private int? m_lastKnownUnreadCount = null;

        /// <summary>
        /// The last time we checked for messages
        /// </summary>
        public DateTime LastUpdateTime
        {
            get
            {
                if (m_lastUpdateTime.Equals(new DateTime(0)))
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundMessageUpdater.LastUpdateTime"))
                    {
                        m_lastUpdateTime = m_baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("BackgroundMessageUpdater.LastUpdateTime");
                    }
                }
                return m_lastUpdateTime;
            }
            private set
            {
                m_lastUpdateTime = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<DateTime>("BackgroundMessageUpdater.LastUpdateTime", m_lastUpdateTime);
            }
        }
        private DateTime m_lastUpdateTime = new DateTime(0);

        /// <summary>
        /// This is an auto length caped list that has fast look up because it is also a map.
        /// If a message has been shown this it will be in this list.
        /// </summary>
        private HashList<string, bool> ShownMessageNotifications
        {
            get
            {
                if (m_ShownMessageNotifications == null)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("BackgroundMessageUpdater.ShownMessageNotifications"))
                    {
                        m_ShownMessageNotifications = m_baconMan.SettingsMan.ReadFromRoamingSettings<HashList<string, bool>>("BackgroundMessageUpdater.ShownMessageNotifications");
                    }
                    else
                    {
                        m_ShownMessageNotifications = new HashList<string, bool>(150);
                    }
                }
                return m_ShownMessageNotifications;
            }
            set
            {
                m_ShownMessageNotifications = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<HashList<string, bool>>("BackgroundMessageUpdater.ShownMessageNotifications", m_ShownMessageNotifications);
            }
        }
        private HashList<string, bool> m_ShownMessageNotifications = null;


        private void SaveSettings()
        {
            // We have to manually set this so we invoke the set {}
            ShownMessageNotifications = ShownMessageNotifications;
        }

        #endregion
    }
}
