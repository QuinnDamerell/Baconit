using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    public sealed partial class SlidingImages : UserControl
    {
        private const int CAnimationAmmount = 60;
        private bool _mIsStarted;
        private bool _mIsAnimating;
        private bool _mIsImageLoaded;
        private List<Uri> _mImageUris;

        public SlidingImages()
        {
            InitializeComponent();
            SizeChanged += SlidingImages_SizeChanged;
        }

        /// <summary>
        /// Sets the image we will use
        /// </summary>
        /// <param name="imageUris"></param>
        public void SetImages(List<Uri> imageUris)
        {
            // Make sure we aren't animating already.
            lock (this)
            {
                if (_mIsStarted)
                {
                    return;
                }
            }

            // Set the list
            _mImageUris = imageUris;
            if (imageUris.Count == 0)
            {
                throw new Exception("No Images Passed!");
            }

            // Set the first image
            var image = new BitmapImage(imageUris[0]) { CreateOptions = BitmapCreateOptions.None };
            image.ImageOpened += Image_ImageOpened;
            ui_mainImage.Source = image;
        }

        /// <summary>
        /// Begins the image animating
        /// </summary>
        public void BeginAnimation()
        {
            // Make sure we have an image
            if(_mImageUris == null)
            {
                throw new Exception("No image was set!");
            }

            // Make sure we aren't animating already.
            lock (this)
            {
                if (_mIsStarted)
                {
                    return;
                }
                _mIsStarted = true;
            }

            UpdateImageSize();
        }

        public void StopAnimation()
        {
            // If we are already animating just jump out.
            lock (this)
            {
                _mIsAnimating = false;
                _mIsStarted = false;
                story_mainImageStory.Stop();
            }
        }

        private void SlidingImages_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // If the control size changes fix the image
            UpdateImageSize();
        }

        private void Image_ImageOpened(object sender, RoutedEventArgs e)
        {
            // When the image is loaded set the flag and set the size
            _mIsImageLoaded = true;

            UpdateImageSize();
        }

        private void UpdateImageSize()
        {
            // If the image isn't loaded do nothing.
            if(!_mIsImageLoaded)
            {
                return;
            }

            // Get the image
            var image = ui_mainImage.Source as BitmapImage;

            // Figure out the scales
            var heightScale = image.PixelHeight / ActualHeight;
            var widthScale = (image.PixelWidth - CAnimationAmmount) / ActualWidth;

            // If the control width is larger than the image with with the animation amount
            // we need to set the height larger than the control or we will show black on the sides
            if (heightScale > widthScale)
            {
                var neededHeight = ActualHeight + (ActualHeight * (heightScale - widthScale));
                ui_mainImage.Height = neededHeight;
                ui_mainImageCompositeTrans.TranslateY = -(neededHeight - ActualHeight) / 2;
                ui_mainImageClipTransform.Y = (neededHeight - ActualHeight) / 2;
            }
            else
            {
                // If not just use the height.
                ui_mainImage.Height = ActualHeight;
                ui_mainImageCompositeTrans.TranslateY = 0;
                ui_mainImageClipTransform.Y = 0;
            }

            // Set the clipping rect
            ui_mainImage.Clip.Rect = new Rect(0, 0, ActualWidth, ActualHeight);

            // If we shouldn't be animating don't
            lock (this)
            {
                if (!_mIsStarted)
                {
                    return;
                }
            }

            // If we are already animating just jump out.
            lock (this)
            {
                if(_mIsAnimating)
                {
                    return;
                }
                _mIsAnimating = true;
            }

            // Setup the image transform
            ui_mainImageCompositeTrans.TranslateX = -CAnimationAmmount;
            anim_mainImageTranslateAnim.To = 0;
            anim_mainImageTranslateAnim.From = -CAnimationAmmount;

            // Setup the clip transform
            anim_mainImageClipTranslateAnim.From = CAnimationAmmount;
            anim_mainImageClipTranslateAnim.To = 0;
            Storyboard.SetTarget(anim_mainImageClipTranslateAnim, ui_mainImage.Clip.Transform);

            // Play the animation!
            story_mainImageStory.Begin();
        }

        private void MainImageStory_Completed(object sender, object e)
        {
            // Make sure we should still be going
            lock (this)
            {
                if (!_mIsAnimating)
                {
                    return;
                }
            }

            // Flip the direction and start again!
            var temp = anim_mainImageTranslateAnim.From.Value;            
            anim_mainImageTranslateAnim.From = anim_mainImageTranslateAnim.To;
            anim_mainImageTranslateAnim.To = temp;

            temp = anim_mainImageClipTranslateAnim.From.Value;
            anim_mainImageClipTranslateAnim.From = anim_mainImageClipTranslateAnim.To;
            anim_mainImageClipTranslateAnim.To = temp;

            story_mainImageStory.Begin();
        }
    }
}
