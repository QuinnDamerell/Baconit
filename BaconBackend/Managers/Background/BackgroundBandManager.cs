using BaconBackend.DataObjects;
using Microsoft.Band;
using Microsoft.Band.Personalization;
using Microsoft.Band.Tiles;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
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
        private readonly Guid _bandTileGuid = new Guid("{D8665187-DC01-4573-8C3B-D5779FD168B2}");
        private readonly BaconManager _baconMan;

        public BackgroundBandManager(BaconManager manager)
        {
            _baconMan = manager;
        }

        /// <summary>
        /// Gets the band version 
        /// </summary>
        public async void GetBandVersion()
        {
            try
            {
                var pairedBand = await GetPairedBand();
                if (pairedBand == null)
                {
                    // We don't have a band.
                    return;
                }

                // Try to connect to the band.
                using (var bandClient = await BandClientManager.Instance.ConnectAsync(pairedBand))
                {
                    var versionString = await bandClient.GetHardwareVersionAsync();
                    var version = int.Parse(versionString);
                    BandVersion = version >= 20 ? BandVersions.V2 : BandVersions.V1;   
                }
            }
            catch(Exception e)
            {
                _baconMan.MessageMan.DebugDia("Failed to get band version.", e);
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
                    var pairedBand = await GetPairedBand();
                    if (pairedBand == null)
                    {
                        // We don't have a band.
                        return;
                    }

                    // Try to connect to the band.
                    using (var bandClient = await BandClientManager.Instance.ConnectAsync(pairedBand))
                    {
                        foreach(var newNote in newNotifications)
                        {
                            var title = newNote.Item1;
                            var body = newNote.Item2;

                            // If the body is empty move the title to the body so it wraps
                            if (string.IsNullOrWhiteSpace(body))
                            {
                                body = title;
                                title = "";
                            }

                            // If we have a title clip it to only two words. The title can't be very long and
                            // looks odd if it is clipped on the band.
                            var firstSpace = string.IsNullOrWhiteSpace(title) ? -1 : title.IndexOf(' ');
                            if(firstSpace != -1)
                            {
                                if (title != null)
                                {
                                    var secondSpace = title.IndexOf(' ', firstSpace + 1);
                                    if(secondSpace != -1)
                                    {
                                        title = title.Substring(0,secondSpace);
                                    }
                                }
                            }                    

                            // Send the message.
                            await bandClient.NotificationManager.SendMessageAsync(_bandTileGuid, title, body, DateTimeOffset.Now, Microsoft.Band.Notifications.MessageFlags.ShowDialog);
                        }
                    }
                }
                catch(Exception e)
                {
                    _baconMan.MessageMan.DebugDia("failed to update band message", e);
                    TelemetryManager.ReportUnexpectedEvent(this, "FailedToUpdateBandMessages", e);
                }
            }
        }

        /// <summary>
        /// Ensure if the tile should exist it does, if it shouldn't it won't.
        /// </summary>
        public async Task<bool> EnsureBandTileState()
        {
            var pairedBand = await GetPairedBand();
            if(pairedBand == null)
            {
                // We don't have a band.
                return false;
            }
          
            var wasSuccess = true;
            try
            {
                // Try to connect to the band.
                using (var bandClient = await BandClientManager.Instance.ConnectAsync(pairedBand))
                {
                    // Get the current set of tiles
                    var tiles = await bandClient.TileManager.GetTilesAsync();

                    // See if our tile exists
                    BandTile baconitTile = null;
                    foreach(var tile in tiles)
                    {
                        if(tile.TileId.Equals(_bandTileGuid))
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
                            var tileCapacity = await bandClient.TileManager.GetRemainingTileCapacityAsync();
                            if (tileCapacity == 0)
                            {
                                _baconMan.MessageMan.ShowMessageSimple("Can't add tile to Band", "Baconit can't add a tile to your Microsoft Band because you reached the maximum number of tile. To add the Baconit tile remove one of the other tiles on your band.");
                                wasSuccess = false;
                            }
                            else
                            {
                                // Create the icons
                                BandIcon smallIcon = null;
                                BandIcon tileIcon = null;

                                // This icon should be 24x24 pixels
                                var smallIconBitmap = await BitmapFactory.New(1, 1).FromContent(new Uri("ms-appx:///Assets/AppAssets/BandIcons/BandImage24.png", UriKind.Absolute));
                                smallIcon = smallIconBitmap.ToBandIcon();
                                // This icon should be 46x46 pixels
                                var tileIconBitmap = await BitmapFactory.New(1, 1).FromContent(new Uri("ms-appx:///Assets/AppAssets/BandIcons/BandImage46.png", UriKind.Absolute));
                                tileIcon = tileIconBitmap.ToBandIcon();
     
                                // Create a new tile
                                var tile = new BandTile(_bandTileGuid)
                                {
                                    IsBadgingEnabled = true,
                                    Name = "Baconit",
                                    SmallIcon = smallIcon,
                                    TileIcon = tileIcon
                                };

                                if (!await bandClient.TileManager.AddTileAsync(tile))
                                {
                                    _baconMan.MessageMan.DebugDia("failed to create tile");
                                    TelemetryManager.ReportUnexpectedEvent(this, "FailedToCreateTileOnBand");
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
                                _baconMan.MessageMan.DebugDia("failed to remove tile");
                                TelemetryManager.ReportUnexpectedEvent(this, "FailedToRemoveTileOnBand");
                                wasSuccess = false;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _baconMan.MessageMan.DebugDia("band connect failed", e);
                wasSuccess = false;
            }
            return wasSuccess;
        }

        /// <summary>
        /// Gets the currently paired band
        /// </summary>
        /// <returns></returns>
        private static async Task<IBandInfo> GetPairedBand()
        {
            var pairedBands = await BandClientManager.Instance.GetBandsAsync();
            return pairedBands.Length != 0 ? pairedBands[0] : null;
        }

        /// <summary>
        /// Updates the band wallpaper
        /// </summary>
        /// <param name="file"></param>
        public async Task<bool> UpdateBandWallpaper(StorageFile file)
        {
            try
            {
                var pairedBand = await GetPairedBand();
                if (pairedBand == null)
                {
                    // We don't have a band.
                    return true;
                }

                // Try to connect to the band.
                using (var bandClient = await BandClientManager.Instance.ConnectAsync(pairedBand))
                {
                    WriteableBitmap bitmap = null;
                    BandImage newBandImage = null;
                    //byte[] pixelArray = null;
                    using (var are = new AutoResetEvent(false))
                    {
                        // Get the bitmap on the UI thread.
                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                        {
                            try
                            {
                                // Create a bitmap for the Me Tile image. 
                                // The image must be 310x102 pixels for Microsoft Band 1 
                                // and 310x102 or 310x128 pixels for Microsoft Band 2.  
                                var properties = await file.Properties.GetImagePropertiesAsync();
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
                _baconMan.MessageMan.DebugDia("failed to set band wallpaper", e);
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
                if (_showInboxOnBand.HasValue) return _showInboxOnBand.Value;
                _showInboxOnBand = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundBandManager.ShowInboxOnBand") && _baconMan.SettingsMan.ReadFromLocalSettings<bool>("BackgroundBandManager.ShowInboxOnBand");
                return _showInboxOnBand.Value;
            }
            set
            {
                _showInboxOnBand = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundBandManager.ShowInboxOnBand", _showInboxOnBand.Value);
            }
        }
        private bool? _showInboxOnBand;


        /// <summary>
        /// Indicates what band version we have.
        /// </summary>
        public BandVersions BandVersion
        {
            get
            {
                if (!_bandVersion.HasValue)
                {
                    _bandVersion = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundBandManager.BandVersion") ? _baconMan.SettingsMan.ReadFromLocalSettings<BandVersions>("BackgroundBandManager.BandVersion") : BandVersions.V1;
                }
                return _bandVersion.Value;
            }
            private set
            {
                _bandVersion = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundBandManager.BandVersion", _bandVersion.Value);
            }
        }
        private BandVersions? _bandVersion;

        #endregion
    }
}
