using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public bool isSelfText;
    }

    public class DraftManager
    {
        const string c_postDraftFile = "PostSubmissionDraft.json";

        BaconManager m_baconMan;

        public DraftManager(BaconManager baconMan)
        {
            m_baconMan = baconMan;
        }

        /// <summary>
        /// Returns if we have a draft file or not.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> HasPostSubmitDraft()
        {
            StorageFolder folder = Windows.Storage.ApplicationData.Current.LocalFolder;
            IStorageItem file = await folder.TryGetItemAsync(c_postDraftFile);
            return file != null;
        }

        /// <summary>
        /// Saves a current post to the draft file.
        /// </summary>
        /// <param name="title"></param>
        /// <param name="urlOrText"></param>
        /// <param name="isSelfText"></param>
        /// <param name="subreddit"></param>
        public async Task<bool> SavePostSubmissionDraft(PostSubmissionDraftData data)
        {
            try
            {
                // Get the folder and file.
                StorageFolder folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                StorageFile file = await folder.CreateFileAsync(c_postDraftFile, CreationCollisionOption.ReplaceExisting);

                // Serialize the data
                string fileData = JsonConvert.SerializeObject(data);

                // Write to the file
                await Windows.Storage.FileIO.WriteTextAsync(file, fileData);
            }
            catch(Exception e)
            {
                m_baconMan.MessageMan.DebugDia("failed to write draft", e);
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToWriteDraftFile",e);
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
                StorageFolder folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                StorageFile file = await folder.CreateFileAsync(c_postDraftFile, CreationCollisionOption.OpenIfExists);

                // Check that is exists.
                if(file == null)
                {
                    return null;
                }

                // Get the data
                string fileData = await Windows.Storage.FileIO.ReadTextAsync(file);

                // Deseralize the data
                return JsonConvert.DeserializeObject<PostSubmissionDraftData>(fileData);
            }
            catch (Exception e)
            {
                m_baconMan.MessageMan.DebugDia("failed to read draft", e);
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToReadDraftFile", e);
                return null;
            }
        }

        /// <summary>
        /// Deletes an existing draft
        /// </summary>
        public async void DiscardPostSubmissionDraft()
        {
            if(await HasPostSubmitDraft())
            {
                StorageFolder folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                StorageFile file = await folder.GetFileAsync(c_postDraftFile);
                if (file != null)
                {
                    await file.DeleteAsync();
                }
            }          
        }
    }
}
