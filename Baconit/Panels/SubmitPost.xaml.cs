using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using BaconBackend.Managers;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels
{
    public sealed partial class SubmitPost : UserControl, IPanel
    {
        /// <summary>
        /// Holds a reference to the panel host.
        /// </summary>
        IPanelHost m_host;

        /// <summary>
        /// Holds a list to the current subreddits.
        /// </summary>
        List<Subreddit> m_currentSubreddits = null;

        /// <summary>
        /// Used to save a draft every now and then.
        /// </summary>
        DispatcherTimer m_draftTimer;

        /// <summary>
        /// Indicates if we are open or not.
        /// </summary>
        bool m_isVisible = false;

        public SubmitPost()
        {
            this.InitializeComponent();

            // Setup the draft timer
            m_draftTimer = new DispatcherTimer();
            m_draftTimer.Interval = new TimeSpan(0, 0, 10);
            m_draftTimer.Tick += DraftTimer_Tick;

            // Switch to the correct header
            VisualStateManager.GoToState(this, "ShowUrl", false);
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            m_host = host;

            // Set the text is we were passed it
            if (arguments.ContainsKey(PanelManager.NAV_ARGS_SUBMIT_POST_SUBREDDIT))
            {
                ui_subredditSuggestBox.Text = (string)arguments[PanelManager.NAV_ARGS_SUBMIT_POST_SUBREDDIT];
            }

            // Resigner for back presses
            App.BaconMan.OnBackButton += BaconMan_OnBackButton;
        }

        public async void OnNavigatingFrom()
        {
            m_isVisible = false;

            // Stop the timer
            DisableAutoSave();

            // Save a draft now
            await SaveDraft();
        }

        public async void OnNavigatingTo()
        {
            m_isVisible = true;

            // Start the draft timer
            m_draftTimer.Start();

            // If we have a draft and we don't have data in the UI ask the user if they
            // want to restore.
            CheckForAndPromptForDraft();

            // Set the status bar color and get the size returned. If it is not 0 use that to move the
            // color of the page into the status bar.
            double statusBarHeight = await m_host.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
            ui_contentRoot.Padding = new Thickness(0, statusBarHeight, 0, 0);
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            OnNavigatingTo();
        }

        #region Selftext

        /// <summary>
        /// Fired when the selftext box is checked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IsSelfPostCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = ui_isSelfPostCheckBox.IsChecked.HasValue ? ui_isSelfPostCheckBox.IsChecked.Value : false;

            // Switch to the correct header
            VisualStateManager.GoToState(this, isChecked ? "ShowSelfText" : "ShowUrl", true);

            // Setup the self text box
            ui_postUrlTextBox.AcceptsReturn = isChecked;

            // Fire the text changed to update the text accordingly
            PostUrlTextBox_TextChanged(null, null);

            ui_postUrlTextBox.MinHeight = 30;

            ToggleFormattedPreviewState(isChecked);
            TogglePostUrlMinSize(isChecked);
        }

        /// <summary>
        /// Fired when the url box is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PostUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // If this is a self post update the markdown.
            if(ui_isSelfPostCheckBox.IsChecked.Value)
            {
                ui_formattedTextBlock.Markdown = ui_postUrlTextBox.Text;
            }
        }

        /// <summary>
        /// We should animate the formated preview box.
        /// </summary>
        /// <param name="shouldOpen"></param>
        private void ToggleFormattedPreviewState(bool shouldOpen)
        {
            // Set the element visible
            ui_formattedPreviewHolder.Visibility = Visibility.Visible;

            // Get the panel height, if the panel hasn't been shown yet
            // the height will be 0, so we will just need to make up a number.
            // The current height without text is about 162, so 170 is good.
            double panelHeight = ui_formattedPreviewHolder.ActualHeight;
            if(panelHeight == 0)
            {
                panelHeight = 170;
            }

            // Setup the animation, set the max height we should open to
            ui_animFormattedPreview.From = shouldOpen ? 0 : panelHeight;
            ui_animFormattedPreview.To = shouldOpen ? panelHeight : 0;
            ui_storyFormattedPreview.Begin();
        }

        /// <summary>
        /// Used to animate the min height of the post url box
        /// </summary>
        /// <param name="shouldOpen"></param>
        private void TogglePostUrlMinSize(bool shouldOpen)
        {
            // Setup the animation, set the min height
            // 32 is about the size of one line, 72 is about the size of 3
            ui_animPostUrlTextBox.From = shouldOpen ? 32 : 72;
            ui_animPostUrlTextBox.To = shouldOpen ? 72 : 32;
            ui_storyPostUrlTextBox.Begin();
        }

        /// <summary>
        /// Fired when the formatted preview animation is complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StoryFormattedPreview_Completed(object sender, object e)
        {
            if(ui_formattedPreviewHolder.MaxHeight == 0)
            {
                // If it is hidden collapse it.
                ui_formattedPreviewHolder.Visibility = Visibility.Collapsed;
            }
            else
            {
                // If it is show set the max height to be huge so when the user adds
                // text the box expands normally.
                ui_formattedPreviewHolder.MaxHeight = double.MaxValue;
            }
        }

        /// <summary>
        /// Fired when a user taps the markdown helper
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RedditMarkdownVisualHelper_OnHelperTapped(object sender, HelperControls.OnHelperTappedArgs e)
        {
            int selStart = ui_postUrlTextBox.SelectionStart;
            int selLength = ui_postUrlTextBox.SelectionLength;
            string curText = ui_postUrlTextBox.Text;
            string insertNewLine = null;

            // Depending on the type see what we can do.
            switch (e.Type)
            {
                case HelperControls.VisualHelperTypes.Bold:
                    if(selLength != 0)
                    {
                        // Wrap the selected text
                        ui_postUrlTextBox.Text = curText.Insert(selStart + selLength, "**").Insert(selStart, "**");
                    }
                    else
                    {
                        // Or add to the end
                        ui_postUrlTextBox.Text = curText + " **example**";
                    }
                    break;
                case HelperControls.VisualHelperTypes.Italic:
                    if (selLength != 0)
                    {
                        // Wrap the selected text
                        ui_postUrlTextBox.Text = curText.Insert(selStart + selLength, "*").Insert(selStart, "*");
                    }
                    else
                    {
                        // Or add to the end
                        ui_postUrlTextBox.Text = curText + " *example*";
                    }
                    break;
                case HelperControls.VisualHelperTypes.Link:
                    if (selLength != 0)
                    {
                        // Wrap the selected text
                        ui_postUrlTextBox.Text = curText.Insert(selStart + selLength, ")[http://www.example.com]").Insert(selStart, "{");
                    }
                    else
                    {
                        // Or add to the end
                        ui_postUrlTextBox.Text = curText + " [example](http://www.example.com)";
                    }
                    break;
                case HelperControls.VisualHelperTypes.NewLine:
                    int injectPos = selStart;
                    // Inject the new line at the current pos
                    ui_postUrlTextBox.Text = curText.Insert(injectPos, "  \r\n");
                    // Move the selection to the end of insert
                    ui_postUrlTextBox.SelectionStart = injectPos + 3;
                    break;
                case HelperControls.VisualHelperTypes.Quote:
                    insertNewLine = "> ";
                    break;
                case HelperControls.VisualHelperTypes.List:
                    insertNewLine = "* ";
                    break;
                case HelperControls.VisualHelperTypes.NumberedList:
                    insertNewLine = "1. ";
                    break;
                case HelperControls.VisualHelperTypes.Code:
                    insertNewLine = "    ";
                    break;
               
            }

            // If the insert on new line is not null we need to find the last new line and
            // insert the text.
            if(insertNewLine != null)
            {
                int searchStart = selStart == curText.Length ? selStart - 1 : selStart;
                int indexLastNewLine = curText.LastIndexOf('\n', searchStart);
                ui_postUrlTextBox.Text = curText.Insert(indexLastNewLine + 1, insertNewLine);
            }

            // When we leave focus the text box again so we don't press enter on the key again
            ui_postUrlTextBox.Focus(FocusState.Programmatic);
        }

        /// <summary>
        /// Fired when a user taps a link in the formatted markdown box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormattedTextBlock_OnMarkdownLinkTapped(object sender, UniversalMarkdown.OnMarkdownLinkTappedArgs e)
        {
            // Show it.
            App.BaconMan.ShowGlobalContent(e.Link);
        }

        #endregion

        #region Subreddit

        /// <summary>
        /// Fired when the text is changed in the suggestion text box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void SubredditSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Only get results when it was a user typing,
            // otherwise assume the value got filled in by TextMemberPath
            // or the handler for SuggestionChosen.
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                // First get the list if we don't have it
                if(m_currentSubreddits == null)
                {
                    m_currentSubreddits = App.BaconMan.SubredditMan.SubredditList;
                }

                // Do a simple starts with search for things that match the user's query.
                List<string> filteredSubreddits = new List<string>();
                string userStringLower = ui_subredditSuggestBox.Text.ToLower();
                foreach (Subreddit sub in m_currentSubreddits)
                {
                    if(!sub.IsArtifical && sub.DisplayName.ToLower().StartsWith(userStringLower))
                    {
                        filteredSubreddits.Add(sub.DisplayName);
                    }
                    if(filteredSubreddits.Count > 4)
                    {
                        break;
                    }
                }

                // Set the new suggestions
                ui_subredditSuggestBox.ItemsSource = filteredSubreddits;
            }
        }

        /// <summary>
        /// Fired when the user selects a suggestion.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void SubredditSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            ui_subredditSuggestBox.Text = (string)args.SelectedItem;
            ui_subredditSuggestBox.ItemsSource = null;
        }

        #endregion

        #region Submit Post

        /// <summary>
        /// Fired when the user taps submit
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            bool isSelfText = ui_isSelfPostCheckBox.IsChecked.HasValue ? ui_isSelfPostCheckBox.IsChecked.Value : false;

            // Grab local copies of the vars
            string titleText = ui_postTitleTextBox.Text;
            string urlOrMarkdownText = ui_postUrlTextBox.Text.Trim();
            string subreddit = ui_subredditSuggestBox.Text;

            // Remove focus from the boxes, yes, this is the only and best way to do this. :(
            ui_postTitleTextBox.IsEnabled = false;
            ui_postTitleTextBox.IsEnabled = true;
            ui_postUrlTextBox.IsEnabled = false;
            ui_postUrlTextBox.IsEnabled = true;
            ui_subredditSuggestBox.IsEnabled = false;
            ui_subredditSuggestBox.IsEnabled = true;

            // Do some basic validation.
            if (String.IsNullOrWhiteSpace(titleText))
            {
                App.BaconMan.MessageMan.ShowMessageSimple("Say Something...", "You can't submit a post with out a title.");
                return;
            }
            if (String.IsNullOrWhiteSpace(urlOrMarkdownText) && !isSelfText)
            {
                App.BaconMan.MessageMan.ShowMessageSimple("Say Something...", $"You can't submit a post with no link.");
                return;
            }
            if(!isSelfText && urlOrMarkdownText.IndexOf(' ') != -1)
            {
                App.BaconMan.MessageMan.ShowMessageSimple("Hmmmmm", "That URL doesn't look quite right, take a second look.");
                return;
            }
            if (String.IsNullOrWhiteSpace(subreddit))
            {
                App.BaconMan.MessageMan.ShowMessageSimple("Where's It Going?", $"You need to pick a subreddit to submit to.");
                return;
            }

            // Show the overlay
            ui_loadingOverlay.Show(true, "Submitting Post...");

            // Make the request
            SubmitNewPostResponse response = await MiscellaneousHelper.SubmitNewPost(App.BaconMan, titleText, urlOrMarkdownText, subreddit, isSelfText, ui_sendRepliesToInbox.IsChecked.Value);

            // Hide the overlay
            ui_loadingOverlay.Hide();

            if(response.Success && !String.IsNullOrWhiteSpace(response.NewPostLink))
            {
                // Navigate to the new post.
                App.BaconMan.ShowGlobalContent(response.NewPostLink);

                // Clear out all of the text so when we come back we are clean
                ui_postTitleTextBox.Text = "";
                ui_postUrlTextBox.Text = "";
                ui_isSelfPostCheckBox.IsChecked = false;
                IsSelfPostCheckBox_Click(null, null);

                // Delete the current draft data
                ClearDraft();
            }
            else
            {
                // Something went wrong, show an error.
                string title = "We Can't Post That";
                string message;

                switch(response.RedditError)
                {
                    case SubmitNewPostErrors.ALREADY_SUB:
                        message = "That link has already been submitted";
                        break;
                    case SubmitNewPostErrors.BAD_CAPTCHA:
                        message = "You need to provide a CAPTCHA to post. Baconit currently doesn't support CAPTCHA so you will have to post this from your desktop. After a few post try from Baconit again. Sorry about that.";
                        break;
                    case SubmitNewPostErrors.DOMAIN_BANNED:
                        message = "The domain of this link has been banned for spam.";
                        break;
                    case SubmitNewPostErrors.IN_TIMEOUT:
                    case SubmitNewPostErrors.RATELIMIT:
                        message = "You have posted too much recently, please wait a while before posting again.";
                        break;
                    case SubmitNewPostErrors.NO_LINKS:
                        message = "This subreddit only allows self text, you can't post links to it.";
                        break;
                    case SubmitNewPostErrors.NO_SELFS:
                        message = "This subreddit only allows links, you can't post self text to it.";
                        break;
                    case SubmitNewPostErrors.SUBREDDIT_NOEXIST:
                        message = "The subreddit your trying to post to doesn't exist.";
                        break;
                    case SubmitNewPostErrors.SUBREDDIT_NOTALLOWED:
                        message = "Your not allowed to post to the subreddit you selected.";
                        break;
                    case SubmitNewPostErrors.BAD_URL:
                        message = "Your URL is invalid, please make sure it is correct.";
                        break;
                    default:
                    case SubmitNewPostErrors.INVALID_OPTION:
                    case SubmitNewPostErrors.SUBREDDIT_REQUIRED:
                        title = "Something Went Wrong";
                        message = "We can't post for you right now, check your Internet connection.";
                        break;
                }
                App.BaconMan.MessageMan.ShowMessageSimple(title, message);
            }
        }

        #endregion

        #region Draft

        /// <summary>
        /// Fired when the back button is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BaconMan_OnBackButton(object sender, BaconBackend.OnBackButtonArgs e)
        {
            if(e.IsHandled)
            {
                return;
            }

            // Don't do anything while we are in the background.
            if(!m_isVisible)
            {
                return;
            }

            // We will always handle everything. We will ask the user if they want to leave an if so
            // call back ourselves.
            e.IsHandled = HasInfoToDraft();

            if(e.IsHandled)
            {
                PrmoptUserForBackout();
            }
        }

        /// <summary>
        /// This will ask the user if they want to leave the page now, save a draft, or discard.
        /// </summary>
        private async void PrmoptUserForBackout()
        {
            // Make a message to show the user
            bool? saveChanges = null;            
            MessageDialog message = new MessageDialog("It looks like you have a post in progress, what would you like to do with it? If you save a draft you can come back an pick up at anytime.", "Leaving So Fast?");
            message.Commands.Add(new UICommand(
                "Save a Draft",
                (IUICommand command) => { saveChanges = true; }));
            message.Commands.Add(new UICommand(
                "Discard Post",
                (IUICommand command) => { saveChanges = false; }));
            message.DefaultCommandIndex = 0;
            message.CancelCommandIndex = 0;

            // Show the dialog
            await message.ShowAsync();

            // They hit cancel
            if(!saveChanges.HasValue)
            {
                return;
            }
            else if(saveChanges.Value)
            {
                // They want to save and exit
                await SaveDraft();
            }
            else
            {
                // They want to discard changes and exit.
                DisableAutoSave();
                ClearDraft();
            }

            // Now go back
            m_host.GoBack();
        }

        /// <summary>
        /// Indicates if we have info so save or not.
        /// </summary>
        /// <returns></returns>
        private bool HasInfoToDraft()
        {
            return !String.IsNullOrWhiteSpace(ui_postTitleTextBox.Text) || !String.IsNullOrWhiteSpace(ui_postUrlTextBox.Text);
        }

        /// <summary>
        /// Fired when the draft timer ticks.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DraftTimer_Tick(object sender, object e)
        {
            // Save the draft.
            await SaveDraft();
        }

        /// <summary>
        /// Will save a draft of what the user currently has.
        /// </summary>
        private async Task SaveDraft()
        {
            // If we have nothing to save don't
            if(!HasInfoToDraft())
            {
                return;
            }

            // Make the data
            PostSubmissionDraftData data = new PostSubmissionDraftData()
            {
                Title = ui_postTitleTextBox.Text,
                UrlOrText = ui_postUrlTextBox.Text,
                isSelfText = ui_isSelfPostCheckBox.IsChecked.Value,
                Subreddit = ui_subredditSuggestBox.Text
            };

            // Save the data
            bool success = await App.BaconMan.DraftMan.SavePostSubmissionDraft(data);

            // Print the last save time
            if(success)
            {
                ui_lastDraftSaveTime.Text = "Saved at " + DateTime.Now.ToString("hh:mm:ss");
            }           
        }

        /// <summary>
        /// Will check for an existing draft and will restore if the users wants.
        /// </summary>
        private async void CheckForAndPromptForDraft()
        {
            // If we already have info on the screen don't replace it.
            if(HasInfoToDraft())
            {
                return;
            }

            // See if we have something to restore
            if(await App.BaconMan.DraftMan.HasPostSubmitDraft())
            {
                // Make a message to show the user
                bool restoreDraft = true;
                MessageDialog message = new MessageDialog("It looks like you have draft of a submission, would you like to restore it?", "Draft Restore");
                message.Commands.Add(new UICommand(
                    "Restore Draft",
                    (IUICommand command) => { restoreDraft = true; }));
                message.Commands.Add(new UICommand(
                    "Discard Draft",
                    (IUICommand command) => { restoreDraft = false; }));
                message.DefaultCommandIndex = 0;
                message.CancelCommandIndex = 1;

                // Show the dialog
                await message.ShowAsync();

                if(restoreDraft)
                {
                    // Get the data
                    PostSubmissionDraftData data = await App.BaconMan.DraftMan.GetPostSubmissionDraft();

                    if(data != null)
                    {
                        // Restore it
                        ui_postTitleTextBox.Text = data.Title;
                        ui_postUrlTextBox.Text = data.UrlOrText;
                        ui_isSelfPostCheckBox.IsChecked = data.isSelfText;
                        ui_subredditSuggestBox.Text = data.Subreddit;

                        // If we are self text open the box
                        if (ui_isSelfPostCheckBox.IsChecked.Value)
                        {
                            IsSelfPostCheckBox_Click(null, null);
                        }
                    }              
                }
                else
                {
                    // Delete the data.
                    ClearDraft();
                }
            }
        }

        /// <summary>
        /// Clears the draft data
        /// </summary>
        private void ClearDraft()
        {
            // Delete the data.
            App.BaconMan.DraftMan.DiscardPostSubmissionDraft();

            // Clear the text
            ui_lastDraftSaveTime.Text = "";
        }

        /// <summary>
        /// Fired when the user taps the discard button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Discard_Click(object sender, RoutedEventArgs e)
        {
            DisableAutoSave();

            // Delete everything.
            ClearDraft();

            // Go Back
            m_host.GoBack();
        }

        /// <summary>
        /// Stops the auto save timer
        /// </summary>
        private void DisableAutoSave()
        {
            m_draftTimer.Stop();
        }

        #endregion
    }
}
