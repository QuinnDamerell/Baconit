using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    public enum VisualHelperTypes
    {
        Bold,
        Italic,
        Link,
        Quote,
        Code,
        List,
        NumberedList,
        NewLine
    }

    public class OnHelperTappedArgs : EventArgs
    {
        public VisualHelperTypes Type;
    }

    public sealed partial class RedditMarkdownVisualHelper : UserControl
    {
        /// <summary>
        /// Fired when an element is clicked
        /// </summary>
        public event EventHandler<OnHelperTappedArgs> OnHelperTapped
        {
            add { m_onHelperTapped.Add(value); }
            remove { m_onHelperTapped.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnHelperTappedArgs>> m_onHelperTapped = new SmartWeakEvent<EventHandler<OnHelperTappedArgs>>();

        public RedditMarkdownVisualHelper()
        {
            this.InitializeComponent();
        }

        private void Bold_Click(object sender, RoutedEventArgs e)
        {
            FireTapped(VisualHelperTypes.Bold);
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            FireTapped(VisualHelperTypes.Link);
        }

        private void Quote_Click(object sender, RoutedEventArgs e)
        {
            FireTapped(VisualHelperTypes.Quote);
        }

        private void Italic_Click(object sender, RoutedEventArgs e)
        {
            FireTapped(VisualHelperTypes.Italic);
        }

        private void NumberedList_Click(object sender, RoutedEventArgs e)
        {
            FireTapped(VisualHelperTypes.NumberedList);
        }

        private void List_Click(object sender, RoutedEventArgs e)
        {
            FireTapped(VisualHelperTypes.List);
        }
        private void Code_Click(object sender, RoutedEventArgs e)
        {
            FireTapped(VisualHelperTypes.Code);
        }
        private void NewLine_Click(object sender, RoutedEventArgs e)
        {
            FireTapped(VisualHelperTypes.NewLine);
        }

        private void FireTapped(VisualHelperTypes type)
        {
            m_onHelperTapped.Raise(this, new OnHelperTappedArgs() { Type = type });
        }
    }
}
