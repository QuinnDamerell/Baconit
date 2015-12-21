using BaconBackend.DataObjects;
using Microsoft.Band;
using Microsoft.Band.Personalization;
using Microsoft.Band.Tiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;


namespace BaconBackend.Managers.Background
{
    public enum BandVersions
    {
        V1,
        V2
    }

    public class BackgroundBandManager
    {
        Guid c_bandTileGuid = new Guid("{D8665187-DC01-4573-8C3B-D5779FD168B2}");
        BaconManager m_baconMan;

        public BackgroundBandManager(BaconManager manager)
        {
            m_baconMan = manager;
        }

        /// <summary>
        /// Gets the band version 
        /// </summary>
        public async void GetBandVersion()
        {
            try
            {
                IBandInfo pairedBand = await GetPairedBand();
                if (pairedBand == null)
                {
                    // We don't have a band.
                    return;
                }

                // Try to connect to the band.
                using (IBandClient bandClient = await BandClientManager.Instance.ConnectAsync(pairedBand))
                {
                    string versionString = await bandClient.GetHardwareVersionAsync();
                    int version = int.Parse(versionString);
                    BandVersion = version >= 20 ? BandVersions.V2 : BandVersions.V1;   
                }
            }
            catch(Exception e)
            {
                m_baconMan.MessageMan.DebugDia("Failed to get band version.", e);
            }
        }

        /// <summary>
        /// Called when we should update the inbox and send any notifications.
        /// </summary>
        /// <param name="newNotifications"></param>
        /// <param name="currentMessages"></param>
        public async Task UpdateInboxMessages(List<Tuple<string, string, string>> newNotifications, List<Message> currentMessages)
        {
            // Make sure we are enabled and in a good state
            if(ShowInboxOnBand && await EnsureBandTileState())
            {
                try
                {
                    IBandInfo pairedBand = await GetPairedBand();
                    if (pairedBand == null)
                    {
                        // We don't have a band.
                        return;
                    }

                    // Try to connect to the band.
                    using (IBandClient bandClient = await BandClientManager.Instance.ConnectAsync(pairedBand))
                    {
                        foreach(Tuple<string, string, string> newNote in newNotifications)
                        {
                            string title = newNote.Item1;
                            string body = newNote.Item2;

                            // If the body is empty move the title to the body so it wraps
                            if (String.IsNullOrWhiteSpace(body))
                            {
                                body = title;
                                title = "";
                            }

                            // If we have a title clip it to only two words. The title can't be very long and
                            // looks odd if it is clipped on the band.
                            int firstSpace = String.IsNullOrWhiteSpace(title) ? -1 : title.IndexOf(' ');
                            if(firstSpace != -1)
                            {
                                int secondSpace = title.IndexOf(' ', firstSpace + 1);
                                if(secondSpace != -1)
                                {
                                    title = title.Substring(0,secondSpace);
                                }
                            }                    

                            // Send the message.
                            await bandClient.NotificationManager.SendMessageAsync(c_bandTileGuid, title, body, DateTimeOffset.Now, Microsoft.Band.Notifications.MessageFlags.ShowDialog);
                        }
                    }
                }
                catch(Exception e)
                {
                    m_baconMan.MessageMan.DebugDia("failed to update band message", e);
                    m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToUpdateBandMessages", e);
                }
            }
        }

        /// <summary>
        /// Ensure if the tile should exist it does, if it shouldn't it won't.
        /// </summary>
        public async Task<bool> EnsureBandTileState()
        {
            IBandInfo pairedBand = await GetPairedBand();
            if(pairedBand == null)
            {
                // We don't have a band.
                return false;
            }
          
            bool wasSuccess = true;
            try
            {
                // Try to connect to the band.
                using (IBandClient bandClient = await BandClientManager.Instance.ConnectAsync(pairedBand))
                {
                    // Get the current set of tiles
                    IEnumerable<BandTile> tiles = await bandClient.TileManager.GetTilesAsync();

                    // See if our tile exists
                    BandTile baconitTile = null;
                    foreach(BandTile tile in tiles)
                    {
                        if(tile.TileId.Equals(c_bandTileGuid))
                        {
                            baconitTile = tile;
                        }
                    }

                    if(baconitTile == null)
                    {
                        // The tile doesn't exist
                        if(ShowInboxOnBand)
                        {
                            // We need a tile, try to make one.
                            int tileCapacity = await bandClient.TileManager.GetRemainingTileCapacityAsync();
                            if (tileCapacity == 0)
                            {
                                m_baconMan.MessageMan.ShowMessageSimple("Can't add tile to Band", "Baconit can't add a tile to your Microsoft Band because you reached the maximum number of tile. To add the Baconit tile remove one of the other tiles on your band.");
                                wasSuccess = false;
                            }
                            else
                            {
                                // Create the icons
                                BandIcon smallIcon = null;
                                BandIcon tileIcon = null;

                                // This icon should be 24x24 pixels
                                WriteableBitmap smallIconBitmap = await BitmapFactory.New(1, 1).FromContent(new Uri("ms-appx:///Assets/AppAssets/BandIcons/BandImage24.png", UriKind.Absolute));
                                smallIcon = smallIconBitmap.ToBandIcon();
                                // This icon should be 46x46 pixels
                                WriteableBitmap tileIconBitmap = await BitmapFactory.New(1, 1).FromContent(new Uri("ms-appx:///Assets/AppAssets/BandIcons/BandImage46.png", UriKind.Absolute));
                                tileIcon = tileIconBitmap.ToBandIcon();
     
                                // Create a new tile
                                BandTile tile = new BandTile(c_bandTileGuid)
                                {
                                    IsBadgingEnabled = true,
                                    Name = "Baconit",
                                    SmallIcon = smallIcon,
                                    TileIcon = tileIcon
                                };

                                if (!await bandClient.TileManager.AddTileAsync(tile))
                                {
                                    m_baconMan.MessageMan.DebugDia("failed to create tile");
                                    m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToCreateTileOnBand");
                                    wasSuccess = false;
                                }
                            }
                        }   
                    }
                    else
                    {
                        // The tile exists
                        if (!ShowInboxOnBand)
                        {
                            // We need to remove it
                            if (!await bandClient.TileManager.RemoveTileAsync(baconitTile))
                            {
                                m_baconMan.MessageMan.DebugDia("failed to remove tile");
                                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToRemoveTileOnBand");
                                wasSuccess = false;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_baconMan.MessageMan.DebugDia("band connect failed", e);
                wasSuccess = false;
            }
            return wasSuccess;
        }

        /// <summary>
        /// Gets the currently paired band
        /// </summary>
        /// <returns></returns>
        public async Task<IBandInfo> GetPairedBand()
        {
            IBandInfo[] pairedBands = await BandClientManager.Instance.GetBandsAsync();
            if(pairedBands.Length != 0)
            {
                return pairedBands[0];
            }
            return null;
        }

        /// <summary>
        /// Updates the band wallpaper
        /// </summary>
        /// <param name="file"></param>
        public async Task<bool> UpdateBandWallpaper(StorageFile file)
        {
            try
            {
                IBandInfo pairedBand = await GetPairedBand();
                if (pairedBand == null)
                {
                    // We don't have a band.
                    return true;
                }

                // Try to connect to the band.
                using (IBandClient bandClient = await BandClientManager.Instance.ConnectAsync(pairedBand))
                {
                    WriteableBitmap bitmap = null;
                    BandImage newBandImage = null;
                    //byte[] pixelArray = null;
                    using (AutoResetEvent are = new AutoResetEvent(false))
                    {
                        // Get the bitmap on the UI thread.
                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                        {
                            try
                            {
                                // Create a bitmap for the Me Tile image. 
                                // The image must be 310x102 pixels for Microsoft Band 1 
                                // and 310x102 or 310x128 pixels for Microsoft Band 2.  
                                ImageProperties properties = await file.Properties.GetImagePropertiesAsync();
                                bitmap = new WriteableBitmap((int)properties.Width, (int)properties.Height);
                                bitmap.SetSource((await file.OpenReadAsync()));
                                newBandImage = bitmap.ToBandImage();
                                //pixelArray = bitmap.ToByteArray();
                            }
                            catch (Exception) { }
                            are.Set();
                        });
                        are.WaitOne(10000);
                    }

                    // Check if we failed
                    if(bitmap == null || newBandImage == null)
                    {
                        throw new Exception("Failed to make new bitmap image.");
                    }

                    // Try to set the image.
                    await bandClient.PersonalizationManager.SetMeTileImageAsync(newBandImage);

                    //// Compute the average color
                    //Color averageColor = new Color();
                    //uint bAvg = 0;
                    //uint rAvg = 0;
                    //uint gAvg = 0;
                    //uint count = 0;
                    //for (int i = 0; i < pixelArray.Length; i += 4)
                    //{
                    //    rAvg += pixelArray[i + 1];
                    //    gAvg += pixelArray[i + 2];
                    //    bAvg += pixelArray[i + 3];
                    //    count++;
                    //}
                    //averageColor.R = (byte)(rAvg / count);
                    //averageColor.B = (byte)(bAvg / count);
                    //averageColor.G = (byte)(gAvg / count);

                    //BandTheme theme = new BandTheme() { Base = averageColor.ToBandColor() };
                    //await bandClient.PersonalizationManager.SetThemeAsync(theme);
                }
            }
            catch(Exception e)
            {
                m_baconMan.MessageMan.DebugDia("failed to set band wallpaper", e);
                return false;
            }
            return true;
        }

        #region Vars

        /// <summary>
        /// Indicates if we should send notifications to the band.
        /// </summary>
        public bool ShowInboxOnBand
        {
            get
            {
                if (!m_showInboxOnBand.HasValue)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundBandManager.ShowInboxOnBand"))
                    {
                        m_showInboxOnBand = m_baconMan.SettingsMan.ReadFromLocalSettings<bool>("BackgroundBandManager.ShowInboxOnBand");
                    }
                    else
                    {
                        m_showInboxOnBand = false;
                    }
                }
                return m_showInboxOnBand.Value;
            }
            set
            {
                m_showInboxOnBand = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<bool>("BackgroundBandManager.ShowInboxOnBand", m_showInboxOnBand.Value);
            }
        }
        private bool? m_showInboxOnBand = null;


        /// <summary>
        /// Indicates what band version we have.
        /// </summary>
        public BandVersions BandVersion
        {
            get
            {
                if (!m_bandVersion.HasValue)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundBandManager.BandVersion"))
                    {
                        m_bandVersion = m_baconMan.SettingsMan.ReadFromLocalSettings<BandVersions>("BackgroundBandManager.BandVersion");
                    }
                    else
                    {
                        m_bandVersion = BandVersions.V1;
                    }
                }
                return m_bandVersion.Value;
            }
            set
            {
                m_bandVersion = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<BandVersions>("BackgroundBandManager.BandVersion", m_bandVersion.Value);
            }
        }
        private BandVersions? m_bandVersion = null;

        #endregion
    }
}
