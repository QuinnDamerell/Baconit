using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using NotificationsExtensions.Badges;
using NotificationsExtensions.Toasts;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Notifications;

namespace BaconBackend.Managers.Background
{
    public class BackgroundMessageUpdater
    {
        public const string CMessageInboxOpenArgument = "goToInbox";

        private readonly BaconManager _baconMan;
        private MessageCollector _collector;
        private RefCountedDeferral _refDeferral;

        public BackgroundMessageUpdater(BaconManager baconMan)
        {
            _baconMan = baconMan;
        }

        /// <summary>
        /// Checks if we should update and if so does so.
        /// </summary>
        /// <param name="refDeferral"></param>
        /// <param name="force"></param>
        public void RunUpdate(RefCountedDeferral refDeferral, bool force = false)
        {
            if (!IsEnabled || !_baconMan.UserMan.IsUserSignedIn) return;
            var timeSinceLastRun = DateTime.Now - LastUpdateTime;
            if (!(timeSinceLastRun.TotalMinutes > 10) && !force) return;
            // We should update. Grab the deferral
            _refDeferral = refDeferral;
            _refDeferral.AddRef();

            // Make the collector
            _collector = new MessageCollector(_baconMan);

            // We don't need to sub to the collection update because we will get
            // called automatically when it updates
            _collector.OnCollectorStateChange += Collector_OnCollectorStateChange;

            // Tell it to update!
            _collector.Update(true);
        }

        /// <summary>
        /// Updates all of the notifications based on the message list.
        /// </summary>
        /// <param name="newMessages"></param>
        public async void UpdateNotifications(List<Message> newMessages)
        {
            var updateSilently = !_baconMan.IsBackgroundTask;

            // Check if we are disabled.
            if (!IsEnabled || !_baconMan.UserMan.IsUserSignedIn)
            {
                // Clear things out
                ToastNotificationManager.History.Clear();
                // Clear the tile
                var content = new BadgeNumericNotificationContent();
                content.Number = 0;
                BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(new BadgeNotification(content.GetXml()));
                return;
            }

            try
            {
                // Get the current state of the messages.
                var history = ToastNotificationManager.History.GetHistory();

                // We need to keep track of anything new to send to the band.
                var newNotifications = new List<Tuple<string, string, string>>();

                // We also need to keep track of how many are unread so we can updates our badge.
                var unreadCount = 0;

                if (NotificationType == 0)
                {
                    // For help look here.
                    // http://blogs.msdn.com/b/tiles_and_toasts/archive/2015/07/09/quickstart-sending-a-local-toast-notification-and-handling-activations-from-it-windows-10.aspx

                    // We need to keep track of what notifications we should have so we can remove ones we don't need.
                    var unReadMessages = new Dictionary<string, bool>();

                    foreach (var message in newMessages.Reverse<Message>())
                    {
                        // If it is new
                        if (!message.IsNew) continue;
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
                        foreach (var oldToast in history)
                        {
                            if (oldToast.Tag.Equals(message.Id)) {  }
                        }

                        // Get the post title.
                        string title;
                        if (message.WasComment)
                        {
                            var subject = message.Subject;
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
                        var body = message.Body;

                        // Add the notification to our list
                        newNotifications.Add(new Tuple<string, string, string>(title, body, message.Id));
                    }

                    // Make sure that all of the messages in our history are still unread
                    // if not remove them.
                    foreach (var t in history)
                    {
                        if(!unReadMessages.ContainsKey(t.Tag))
                        {
                            // This message isn't unread any longer.
                            ToastNotificationManager.History.Remove(t.Tag);
                        }
                    }

                    // Save any settings we changed
                    SaveSettings();
                }
                else
                {
                    // Count how many are unread
                    unreadCount += newMessages.Count(message => message.IsNew);

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
                var hasShownNote = false;
                foreach(var newNote in newNotifications)
                {
                    // Make the visual
                    var visual = new ToastVisual();
                    visual.TitleText = new ToastText { Text = newNote.Item1 };
                    if(!string.IsNullOrWhiteSpace(newNote.Item2))
                    {
                        visual.BodyTextLine1 = new ToastText { Text = newNote.Item2 };
                    }

                    // Make the toast content
                    var toastContent = new ToastContent();
                    toastContent.Visual = visual;
                    toastContent.Launch = CMessageInboxOpenArgument;
                    toastContent.ActivationType = ToastActivationType.Foreground;
                    toastContent.Duration = ToastDuration.Short;

                    var toast = new ToastNotification(toastContent.GetXml());
                    toast.Tag = newNote.Item3;

                    // Only show if we should and this is the first message to show.
                    toast.SuppressPopup = hasShownNote || updateSilently || AddToNotificationCenterSilently;
                    ToastNotificationManager.CreateToastNotifier().Show(toast);

                    // Mark that we have shown one.
                    hasShownNote = true;
                }

                // Make sure the main tile is an iconic tile.
                _baconMan.TileMan.UpdateMainTile(unreadCount);

                // Update the band if we have one.
                if(!updateSilently)
                {
                    await _baconMan.BackgroundMan.BandMan.UpdateInboxMessages(newNotifications, newMessages);
                }

                // If all was successful update the last time we updated
                LastUpdateTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                TelemetryManager.ReportUnexpectedEvent(this, "messageUpdaterFailed", ex);
                _baconMan.MessageMan.DebugDia("failed to update message notifications", ex);
            }

            // When we are done release the deferral
            ReleaseDeferral();
        }

        /// <summary>
        /// Fired when the collector state changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectorStateChange(object sender, CollectorStateChangeArgs e)
        {
            if(e.State == CollectorState.Error)
            {
                // If we get an error we are done.
                ReleaseDeferral();
            }
        }

        /// <summary>
        /// Releases the deferral if it is held.
        /// </summary>
        private void ReleaseDeferral()
        {
            _refDeferral?.ReleaseRef();
            _refDeferral = null;
        }

        #region Vars

        /// <summary>
        /// Indicates if background message updates are enabled.
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                if (_isEnabled.HasValue) return _isEnabled.Value;
                _isEnabled = !_baconMan.SettingsMan.RoamingSettings.ContainsKey("BackgroundMessageUpdater.IsEnabled") || _baconMan.SettingsMan.ReadFromRoamingSettings<bool>("BackgroundMessageUpdater.IsEnabled");
                return _isEnabled.Value;
            }
            set
            {
                _isEnabled = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("BackgroundMessageUpdater.IsEnabled", _isEnabled.Value);
            }
        }
        private bool? _isEnabled;

        /// <summary>
        /// Indicates if we should add notification silently.
        /// </summary>
        public bool AddToNotificationCenterSilently
        {
            get
            {
                if (_addSilently.HasValue) return _addSilently.Value;
                _addSilently = _baconMan.SettingsMan.RoamingSettings.ContainsKey("BackgroundMessageUpdater.AddToNotificationCenterSilently") && _baconMan.SettingsMan.ReadFromRoamingSettings<bool>("BackgroundMessageUpdater.AddToNotificationCenterSilently");
                return _addSilently.Value;
            }
            set
            {
                _addSilently = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("BackgroundMessageUpdater.AddToNotificationCenterSilently", _addSilently.Value);
            }
        }
        private bool? _addSilently;

        /// <summary>
        /// Indicates what type of notifications the user wants.
        /// </summary>
        public int NotificationType
        {
            get
            {
                if (_notificationType.HasValue) return _notificationType.Value;
                _notificationType = _baconMan.SettingsMan.RoamingSettings.ContainsKey("BackgroundMessageUpdater.NotificationType") ? _baconMan.SettingsMan.ReadFromRoamingSettings<int>("BackgroundMessageUpdater.NotificationType") : 0;
                return _notificationType.Value;
            }
            set
            {
                _notificationType = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("BackgroundMessageUpdater.NotificationType", _notificationType.Value);
            }
        }
        private int? _notificationType;


        /// <summary>
        /// Last known unread count
        /// </summary>
        public int LastKnownUnreadCount
        {
            get
            {
                if (_lastKnownUnreadCount.HasValue) return _lastKnownUnreadCount.Value;
                _lastKnownUnreadCount = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundMessageUpdater.LastKnownUnreadCount") ? _baconMan.SettingsMan.ReadFromLocalSettings<int>("BackgroundMessageUpdater.LastKnownUnreadCount") : 0;
                return _lastKnownUnreadCount.Value;
            }
            set
            {
                _lastKnownUnreadCount = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundMessageUpdater.LastKnownUnreadCount", _lastKnownUnreadCount.Value);
            }
        }
        private int? _lastKnownUnreadCount;

        /// <summary>
        /// The last time we checked for messages
        /// </summary>
        public DateTime LastUpdateTime
        {
            get
            {
                if (!_lastUpdateTime.Equals(new DateTime(0))) return _lastUpdateTime;
                if (_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundMessageUpdater.LastUpdateTime"))
                {
                    _lastUpdateTime = _baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("BackgroundMessageUpdater.LastUpdateTime");
                }
                return _lastUpdateTime;
            }
            private set
            {
                _lastUpdateTime = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundMessageUpdater.LastUpdateTime", _lastUpdateTime);
            }
        }
        private DateTime _lastUpdateTime = new DateTime(0);

        /// <summary>
        /// This is an auto length caped list that has fast look up because it is also a map.
        /// If a message has been shown this it will be in this list.
        /// </summary>
        private HashList<string, bool> ShownMessageNotifications
        {
            get
            {
                if (_shownMessageNotifications != null) return _shownMessageNotifications;
                _shownMessageNotifications = _baconMan.SettingsMan.RoamingSettings.ContainsKey("BackgroundMessageUpdater.ShownMessageNotifications") 
                    ? _baconMan.SettingsMan.ReadFromRoamingSettings<HashList<string, bool>>("BackgroundMessageUpdater.ShownMessageNotifications") 
                    : new HashList<string, bool>(150);
                return _shownMessageNotifications;
            }
            set
            {
                _shownMessageNotifications = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("BackgroundMessageUpdater.ShownMessageNotifications", _shownMessageNotifications);
            }
        }
        private HashList<string, bool> _shownMessageNotifications;


        private void SaveSettings()
        {
            // We have to manually set this so we invoke the set {}
            ShownMessageNotifications = ShownMessageNotifications;
        }

        #endregion
    }
}
