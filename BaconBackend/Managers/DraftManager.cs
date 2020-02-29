using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace BaconBackend.Managers
{
    /// <summary>
    /// Used to contain data for post draft
    /// </summary>
    public class PostSubmissionDraftData
    {
        public string Title;
        public string UrlOrText;
        public string Subreddit;
        public bool IsSelfText;
    }

    public class DraftManager
    {
        private const string PostDraftFile = "PostSubmissionDraft.json";

        private readonly BaconManager _baconMan;

        public DraftManager(BaconManager baconMan)
        {
            _baconMan = baconMan;
        }

        /// <summary>
        /// Returns if we have a draft file or not.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> HasPostSubmitDraft()
        {
            var folder = ApplicationData.Current.LocalFolder;
            var file = await folder.TryGetItemAsync(PostDraftFile);
            return file != null;
        }

        /// <summary>
        /// Saves a current post to the draft file.
        /// </summary>
        /// <param name="data"></param>
        public async Task<bool> SavePostSubmissionDraft(PostSubmissionDraftData data)
        {
            try
            {
                // Get the folder and file.
                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync(PostDraftFile, CreationCollisionOption.ReplaceExisting);

                // Serialize the data
                var fileData = JsonConvert.SerializeObject(data);

                // Write to the file
                await FileIO.WriteTextAsync(file, fileData);
            }
            catch(Exception e)
            {
                _baconMan.MessageMan.DebugDia("failed to write draft", e);
                TelemetryManager.ReportUnexpectedEvent(this, "FailedToWriteDraftFile",e);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Returns the current submission post draft
        /// </summary>
        /// <returns></returns>
        public async Task<PostSubmissionDraftData> GetPostSubmissionDraft()
        {
            try
            {
                // Get the folder and file.
                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync(PostDraftFile, CreationCollisionOption.OpenIfExists);

                // Check that is exists.
                if(file == null)
                {
                    return null;
                }

                // Get the data
                var fileData = await FileIO.ReadTextAsync(file);

                // Deseralize the data
                return JsonConvert.DeserializeObject<PostSubmissionDraftData>(fileData);
            }
            catch (Exception e)
            {
                _baconMan.MessageMan.DebugDia("failed to read draft", e);
                TelemetryManager.ReportUnexpectedEvent(this, "FailedToReadDraftFile", e);
                return null;
            }
        }

        /// <summary>
        /// Deletes an existing draft
        /// </summary>
        public static async void DiscardPostSubmissionDraft()
        {
            if (!await HasPostSubmitDraft()) return;
            var folder = ApplicationData.Current.LocalFolder;
            var file = await folder.GetFileAsync(PostDraftFile);
            if (file != null)
            {
                await file.DeleteAsync();
            }
        }
    }
}
