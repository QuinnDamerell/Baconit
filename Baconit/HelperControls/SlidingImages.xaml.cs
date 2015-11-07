using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    public sealed partial class SlidingImages : UserControl
    {
        const int c_animationAmmount = 60;
        bool m_isStarted = false;
        bool m_isAnimating = false;
        bool m_isImageLoaded = false;
        List<Uri> m_imageUris = null;

        public SlidingImages()
        {
            this.InitializeComponent();
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
                if (m_isStarted)
                {
                    return;
                }
            }

            // Set the list
            m_imageUris = imageUris;
            if (imageUris.Count == 0)
            {
                throw new Exception("No Images Passed!");
            }

            // Set the first image
            BitmapImage image = new BitmapImage(imageUris[0]) { CreateOptions = BitmapCreateOptions.None };
            image.ImageOpened += Image_ImageOpened;
            ui_mainImage.Source = image;
        }

        /// <summary>
        /// Begins the image animating
        /// </summary>
        public void BeginAnimation()
        {
            // Make sure we have an image
            if(m_imageUris == null)
            {
                throw new Exception("No image was set!");
            }

            // Make sure we aren't animating already.
            lock (this)
            {
                if (m_isStarted)
                {
                    return;
                }
                m_isStarted = true;
            }

            UpdateImageSize();
        }

        public void StopAnimation()
        {
            // If we are already animating just jump out.
            lock (this)
            {
                m_isAnimating = false;
                m_isStarted = false;
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
            m_isImageLoaded = true;

            UpdateImageSize();
        }

        private void UpdateImageSize()
        {
            // If the image isn't loaded do nothing.
            if(!m_isImageLoaded)
            {
                return;
            }

            // Get the image
            BitmapImage image = ui_mainImage.Source as BitmapImage;

            // Figure out the scales
            double heightScale = image.PixelHeight / this.ActualHeight;
            double widthScale = (image.PixelWidth - c_animationAmmount) / this.ActualWidth;

            // If the control width is larger than the image with with the animation amount
            // we need to set the height larger than the control or we will show black on the sides
            if (heightScale > widthScale)
            {
                double neededHeight = this.ActualHeight + (this.ActualHeight * (heightScale - widthScale));
                ui_mainImage.Height = neededHeight;
                ui_mainImageCompositeTrans.TranslateY = -(neededHeight - this.ActualHeight) / 2;
                ui_mainImageClipTransform.Y = (neededHeight - this.ActualHeight) / 2;
            }
            else
            {
                // If not just use the height.
                ui_mainImage.Height = this.ActualHeight;
                ui_mainImageCompositeTrans.TranslateY = 0;
                ui_mainImageClipTransform.Y = 0;
            }

            // Set the clipping rect
            ui_mainImage.Clip.Rect = new Rect(0, 0, this.ActualWidth, this.ActualHeight);

            // If we shouldn't be animating don't
            lock (this)
            {
                if (!m_isStarted)
                {
                    return;
                }
            }

            // If we are already animating just jump out.
            lock (this)
            {
                if(m_isAnimating)
                {
                    return;
                }
                m_isAnimating = true;
            }

            // Setup the image transform
            ui_mainImageCompositeTrans.TranslateX = -c_animationAmmount;
            anim_mainImageTranslateAnim.To = 0;
            anim_mainImageTranslateAnim.From = -c_animationAmmount;

            // Setup the clip transform
            anim_mainImageClipTranslateAnim.From = c_animationAmmount;
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
                if (!m_isAnimating)
                {
                    return;
                }
            }

            // Flip the direction and start again!
            double temp = anim_mainImageTranslateAnim.From.Value;            
            anim_mainImageTranslateAnim.From = anim_mainImageTranslateAnim.To;
            anim_mainImageTranslateAnim.To = temp;

            temp = anim_mainImageClipTranslateAnim.From.Value;
            anim_mainImageClipTranslateAnim.From = anim_mainImageClipTranslateAnim.To;
            anim_mainImageClipTranslateAnim.To = temp;

            story_mainImageStory.Begin();
        }
    }
}
