using BaconBackend.DataObjects;
using NotificationsExtensions.Badges;
using NotificationsExtensions.Tiles;
using NotificationsExtensions.Toasts;
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
            return SecondaryTile.Exists(c_subredditTitleId + subredditDisplayName);
        }

        /// <summary>
        /// Creates a secondary tile given a subreddit.
        /// </summary>
        /// <param name="subreddit"></param>
        /// <returns></returns>
        public async Task<bool> CreateSubredditTile(Subreddit subreddit)
        {
            // If it already exists get out of here.
            if (IsSubredditPinned(subreddit.DisplayName))
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
            foreach (SecondaryTile tile in tiles)
            {
                if (tile.TileId.Equals(c_subredditTitleId + subreddit.DisplayName))
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
        public void UpdateMainTile(int unreadCount)
        {
            // Setup the main tile as iconic for small and medium
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
                    },                    
                }
            };

            // If the user is signed in we will do more for large and wide.
            if(m_baconMan.UserMan.IsUserSignedIn && m_baconMan.UserMan.CurrentUser != null && !String.IsNullOrWhiteSpace(m_baconMan.UserMan.CurrentUser.Name))
            {
                content.Visual.TileWide = new TileBinding()
                {
                    Content = new TileBindingContentAdaptive()
                    {
                        Children =
                        {
                            new TileText()
                            {
                                Text = m_baconMan.UserMan.CurrentUser.Name,
                                Style = TileTextStyle.Caption
                            },
                            new TileText()
                            {
                                Text = String.Format("{0:N0}", m_baconMan.UserMan.CurrentUser.CommentKarma) + " comment karma",
                                Style = TileTextStyle.CaptionSubtle
                            },
                            new TileText()
                            {
                                Text = String.Format("{0:N0}", m_baconMan.UserMan.CurrentUser.LinkKarma) + " link karma",
                                Style = TileTextStyle.CaptionSubtle
                            },                           
                        }
                    },
                };

                // If we have messages replace the user name with the message string.
                if (unreadCount != 0)
                {
                    TileText unreadCountText = new TileText()
                    {
                        Text = unreadCount + " Unread Inbox Message" + (unreadCount == 1 ? "" : "s"),
                        Style = TileTextStyle.Body
                    };
                    ((TileBindingContentAdaptive)content.Visual.TileWide.Content).Children[0] = unreadCountText;
                }

                // Also set the cake day if it is today
                DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                DateTime userCreationTime = origin.AddSeconds(m_baconMan.UserMan.CurrentUser.CreatedUtc).ToLocalTime();
                TimeSpan elapsed = DateTime.Now - userCreationTime;
                double fullYears = Math.Floor((elapsed.TotalDays / 365));
                int daysUntil = (int)(elapsed.TotalDays - (fullYears * 365));

                if(daysUntil == 0)
                {
                    // Make the text
                    TileText cakeDayText = new TileText()
                    {
                        Text = "Today is your cake day!",
                        Style = TileTextStyle.Body
                    };
                    ((TileBindingContentAdaptive)content.Visual.TileWide.Content).Children[0] = cakeDayText;
                }
            }  

            // Set large to the be same as wide.
            content.Visual.TileLarge = content.Visual.TileWide;

            // Update the tile
            TileUpdateManager.CreateTileUpdaterForApplication().Update(new TileNotification(content.GetXml()));

            // Update the badge
            BadgeNumericNotificationContent badgeContent = new BadgeNumericNotificationContent();
            badgeContent.Number = (uint)unreadCount;
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(new BadgeNotification(badgeContent.GetXml()));
        }
    }
}
