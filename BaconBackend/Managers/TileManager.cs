using BaconBackend.DataObjects;
using NotificationsExtensions.Badges;
using NotificationsExtensions.Tiles;
using System;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.UI.StartScreen;
using NotificationsExtensions;

namespace BaconBackend.Managers
{
    public class TileManager
    {
        public const string SubredditOpenArgument = "goToSubreddit?displayName=";
        private const string SubredditTitleId = "subreddit.";

        private readonly BaconManager _baconMan;

        public TileManager(BaconManager baconMan)
        {
            _baconMan = baconMan;
        }

        /// <summary>
        /// Checks if a secondary tile for the subreddit exits
        /// </summary>
        /// <param name="subredditDisplayName"></param>
        /// <returns></returns>
        public static bool IsSubredditPinned(string subredditDisplayName)
        {
            return SecondaryTile.Exists(SubredditTitleId + subredditDisplayName);
        }

        /// <summary>
        /// Creates a secondary tile given a subreddit.
        /// </summary>
        /// <param name="subreddit"></param>
        /// <returns></returns>
        public static async Task<bool> CreateSubredditTile(Subreddit subreddit)
        {
            // If it already exists get out of here.
            if (IsSubredditPinned(subreddit.DisplayName))
            {
                return true;
            }

            // Try to make the tile
            var tile = new SecondaryTile();
            tile.DisplayName = subreddit.DisplayName;
            tile.TileId = SubredditTitleId + subreddit.DisplayName;
            tile.Arguments = SubredditOpenArgument + subreddit.DisplayName;

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
        public static async Task<bool> RemoveSubredditTile(Subreddit subreddit)
        {
            // Check that is exists
            if (!IsSubredditPinned(subreddit.DisplayName))
            {
                return true;
            }

            // Get all tiles
            var tiles = await SecondaryTile.FindAllAsync();

            // Find this one
            foreach (var tile in tiles)
            {
                if (tile.TileId.Equals(SubredditTitleId + subreddit.DisplayName))
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
            var content = new TileContent
            {
                Visual = new TileVisual
                {
                    TileSmall = new TileBinding
                    {
                        Content = new TileBindingContentIconic
                        {
                            Icon = new TileImageSource("ms-appx:///Assets/AppAssets/IconicTiles/Iconic144.png"),
                        }
                    },
                    TileMedium = new TileBinding
                    {
                        Content = new TileBindingContentIconic
                        {
                            Icon = new TileImageSource("ms-appx:///Assets/AppAssets/IconicTiles/Iconic200.png"),
                        }
                    },                    
                }
            };

            // If the user is signed in we will do more for large and wide.
            if(_baconMan.UserMan.IsUserSignedIn && _baconMan.UserMan.CurrentUser != null && !string.IsNullOrWhiteSpace(_baconMan.UserMan.CurrentUser.Name))
            {
                content.Visual.TileWide = new TileBinding
                {
                    Content = new TileBindingContentAdaptive
                    {
                        Children =
                        {
                            new AdaptiveText
                            {
                                Text = _baconMan.UserMan.CurrentUser.Name,
                                HintStyle = AdaptiveTextStyle.Caption
                            },
                            new AdaptiveText
                            {
                                Text = $"{_baconMan.UserMan.CurrentUser.CommentKarma:N0}" + " comment karma",
                                HintStyle = AdaptiveTextStyle.CaptionSubtle
                            },
                            new AdaptiveText
                            {
                                Text = $"{_baconMan.UserMan.CurrentUser.LinkKarma:N0}" + " link karma",
                                HintStyle = AdaptiveTextStyle.CaptionSubtle
                            },                           
                        }
                    },
                };

                // If we have messages replace the user name with the message string.
                if (unreadCount != 0)
                {
                    var unreadCountText = new AdaptiveText
                    {
                        Text = unreadCount + " Unread Inbox Message" + (unreadCount == 1 ? "" : "s"),
                        HintStyle = AdaptiveTextStyle.Body
                    };
                    ((TileBindingContentAdaptive)content.Visual.TileWide.Content).Children[0] = unreadCountText;
                }

                // Also set the cake day if it is today
                var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                var userCreationTime = origin.AddSeconds(_baconMan.UserMan.CurrentUser.CreatedUtc).ToLocalTime();
                var elapsed = DateTime.Now - userCreationTime;
                var fullYears = Math.Floor((elapsed.TotalDays / 365));
                var daysUntil = (int)(elapsed.TotalDays - (fullYears * 365));

                if(daysUntil == 0)
                {
                    // Make the text
                    var cakeDayText = new AdaptiveText
                    {
                        Text = "Today is your cake day!",
                        HintStyle = AdaptiveTextStyle.Body
                    };
                    ((TileBindingContentAdaptive)content.Visual.TileWide.Content).Children[0] = cakeDayText;
                }
            }  

            // Set large to the be same as wide.
            content.Visual.TileLarge = content.Visual.TileWide;

            // Update the tile
            TileUpdateManager.CreateTileUpdaterForApplication().Update(new TileNotification(content.GetXml()));

            // Update the badge
            var badgeContent = new BadgeNumericNotificationContent {Number = (uint) unreadCount};
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(new BadgeNotification(badgeContent.GetXml()));
        }
    }
}
