using BaconBackend.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaconBackend.Helpers;
using BaconBackend.Managers;

namespace BaconBackend.Collectors
{
    public class MessageCollector : Collector<Message>
    {
        private readonly BaconManager _mBaconMan;

        public MessageCollector(BaconManager baconMan) :
            base(baconMan, "messageInbox")
        {
            _mBaconMan = baconMan;

            // Set up the list helper
            InitListHelper("/message/inbox/.json");

            // Sub ourselves to the on updated event.
            OnCollectionUpdated += MessageCollector_OnCollectionUpdated;

            _mBaconMan.UserMan.OnUserUpdated += OnUserUpdated;
        }

        /// <summary>
        /// Fired when the messages should be formatted.
        /// </summary>
        /// <param name="messages"></param>
        protected override void ApplyCommonFormatting(ref List<Message> messages)
        {
            foreach(var message in messages)
            {
                // Set the first line
                message.HeaderFirst = message.Subject;

                // Set the UI strings
                if (message.WasComment)
                {
                    message.HeaderSecond = message.Author + " via " + message.Subreddit;
                }
                else
                {
                    message.HeaderSecond = message.Author;
                }
            }
        }

        protected override List<Message> ParseElementList(List<Element<Message>> elements)
        {
            // Converts the elements into a list.
            return elements.Select(element => element.Data).ToList();
        }
        
        #region Message Actions

        /// <summary>
        /// Called by the consumer when a massage should be changed
        /// </summary>
        public void ChangeMessageReadStatus(Message message, bool isRead, int messagePosition = 0)
        {
            // Using the post and suggested index, find the real post and index
            var collectionMessage = message;
            FindMessageInCurrentCollection(ref collectionMessage, ref messagePosition);

            if (collectionMessage == null || messagePosition == -1)
            {
                // We didn't find it.
                return;
            }

            // Update the status
            collectionMessage.IsNew = isRead;

            // Fire off that a update happened.
            FireCollectionUpdated(messagePosition, new List<Message> { collectionMessage }, false, false);

            // Start a task to make the vote
            Task.Run(async () =>
            {
                try
                {
                    // Build the data
                    var request = collectionMessage.IsNew ? "/api/unread_message" : "/api/read_message";
                    var postData = new List<KeyValuePair<string, string>>();
                    postData.Add(new KeyValuePair<string, string>("id", collectionMessage.GetFullName()));

                    // Make the call
                    var str = await _mBaconMan.NetworkMan.MakeRedditPostRequestAsString(request, postData);

                    // Do some super simple validation
                    if (str != "{}")
                    {
                        throw new Exception("Failed to set message status! The response indicated a failure");
                    }
                }
                catch (Exception ex)
                {
                    _mBaconMan.MessageMan.DebugDia("failed to set message status!", ex);
                    TelemetryManager.ReportUnexpectedEvent(this, "failedToSetMessageRead", ex);
                }
            });
        }

        /// <summary>
        /// Given a post and a starting index this function will return the collection message
        /// object and the true index of the item. If not found it will return null and -1
        /// </summary>
        private void FindMessageInCurrentCollection(ref Message message, ref int index)
        {
            // Get the current list
            var messages = GetCurrentPostsInternal();

            // Find the message starting at the possible index
            for (; index < messages.Count; index++)
            {
                if (messages[index].Id.Equals(message.Id))
                {
                    // Grab the message and break;
                    message = messages[index];
                    return;
                }
            }

            // If we didn't find it kill them.
            index = -1;
            message = null;
        }

        #endregion

        /// <summary>
        /// Fired when the collection is updated. We need to update the background notification manager and the sidebar UI.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MessageCollector_OnCollectionUpdated(object sender, CollectionUpdatedArgs<Message> e)
        {
            UpdateMessageCounts(GetCurrentPostsInternal());            
        }

        private void OnUserUpdated(object sender, UserUpdatedArgs e)
        {
            switch (e.Action)
            {
                // If the user is now signed in update.
                case UserCallbackAction.Added:
                    Update();
                    break;
                case UserCallbackAction.Removed:
                    // If the user is logged out clear out the message ui.
                    UpdateMessageCounts(new List<Message>(), true);
                    break;
                case UserCallbackAction.Updated:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void UpdateMessageCounts(List<Message> messages, bool forceUiSet = false)
        {
            // Check to see if there are any unread messages
            var unreadCount = messages.Count(searchMessage => searchMessage.IsNew);

            if (messages.Count == 0 && !forceUiSet) return;
            // Update the UI
            _mBaconMan.UserMan.UpdateUnReadMessageCount(unreadCount);

            // Update the notifications in the background
            Task.Run(() =>
            {
                _mBaconMan.BackgroundMan.MessageUpdaterMan.UpdateNotifications(messages);
            });
        }
    }
}
