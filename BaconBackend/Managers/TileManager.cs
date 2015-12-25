using BaconBackend.DataObjects;
using NotificationsExtensions.Tiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.UI.StartScreen;

namespace BaconBackend.Managers
{
    public class TileManager
    {
        public const string c_subredditOpenArgument = "goToSubreddit?displayName=";
        const string c_subredditTitleId = "subreddit.";

        BaconManager m_baconMan;

        public TileManager(BaconManager baconMan)
        {
            m_baconMan = baconMan;
        }

        /// <summary>
        /// Checks if a secondary tile for the subreddit exits
        /// </summary>
        /// <param name="subredditDisplayName"></param>
        /// <returns></returns>
        public bool IsSubredditPinned(string subredditDisplayName)
        {
            return SecondaryTile.Exists(c_subredditTitleId+subredditDisplayName);
        }

        /// <summary>
        /// Creates a secondary tile given a subreddit.
        /// </summary>
        /// <param name="subreddit"></param>
        /// <returns></returns>
        public async Task<bool> CreateSubredditTile(Subreddit subreddit)
        {
            // If it already exists get out of here.
            if(IsSubredditPinned(subreddit.DisplayName))
            {
                return true;
            }

            // Try to make the tile
            SecondaryTile tile = new SecondaryTile();
            tile.DisplayName = subreddit.DisplayName;
            tile.TileId = c_subredditTitleId + subreddit.DisplayName;
            tile.Arguments = c_subredditOpenArgument + subreddit.DisplayName;

            // Set the visuals
            tile.VisualElements.Square150x150Logo = new Uri("ms-appx:///Assets/AppAssets/Square150x150/Square150.png", UriKind.Absolute);
            tile.VisualElements.Square310x310Logo = new Uri("ms-appx:///Assets/AppAssets/Square310x310/Square210.png", UriKind.Absolute);
            tile.VisualElements.Square44x44Logo = new Uri("ms-appx:///Assets/AppAssets/Square44x44/Square44.png", UriKind.Absolute);
            tile.VisualElements.Square71x71Logo = new Uri("ms-appx:///Assets/AppAssets/Square71x71/Square71.png", UriKind.Absolute);
            tile.VisualElements.Wide310x150Logo = new Uri("ms-appx:///Assets/AppAssets/Wide310x310/Wide310.png", UriKind.Absolute);
            tile.VisualElements.ShowNameOnSquare150x150Logo = true;
            tile.VisualElements.ShowNameOnSquare310x310Logo = true;
            tile.VisualElements.ShowNameOnWide310x150Logo = true;
            tile.RoamingEnabled = true;

            // Request the create.
            return await tile.RequestCreateAsync();
        }

        /// <summary>
        /// Removes a subreddit tile that is pinned.
        /// </summary>
        /// <param name="subreddit"></param>
        /// <returns></returns>
        public async Task<bool> RemoveSubredditTile(Subreddit subreddit)
        {
            // Check that is exists
            if (!IsSubredditPinned(subreddit.DisplayName))
            {
                return true;
            }

            // Get all tiles
            IReadOnlyList<SecondaryTile> tiles = await SecondaryTile.FindAllAsync();

            // Find this one
            foreach(SecondaryTile tile in tiles)
            {
                if(tile.TileId.Equals(c_subredditTitleId+subreddit.DisplayName))
                {
                    return await tile.RequestDeleteAsync();
                }
            }

            // We failed
            return false;
        }

        /// <summary>
        /// Makes sure the main app tile is an icon tile type
        /// </summary>
        public void EnsureMainTileIsIconic()
        {
            TileContent content = new TileContent()
            {
                Visual = new TileVisual()
                {
                    TileSmall = new TileBinding()
                    {
                        Content = new TileBindingContentIconic()
                        {
                            Icon = new TileImageSource("ms-appx:///Assets/AppAssets/IconicTiles/Iconic144.png"),
                        }
                    },
                    TileMedium = new TileBinding()
                    {
                        Content = new TileBindingContentIconic()
                        {
                            Icon = new TileImageSource("ms-appx:///Assets/AppAssets/IconicTiles/Iconic200.png"),
                        }
                    }
                }
            };

            // Update the tile
            TileUpdateManager.CreateTileUpdaterForApplication().Update(new TileNotification(content.GetXml()));
        }
    }
}
