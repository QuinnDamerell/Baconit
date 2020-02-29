using BaconBackend.Helpers;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    public enum VisualHelperFullscreenStatus
    {
        DontShow,
        GotoFullscreen,
        RestoreFromFullscreen
    }

    public enum VisualHelperTypes
    {
        Bold,
        Italic,
        Link,
        Quote,
        Code,
        List,
        NumberedList,
        NewLine,
        Fullscreen
    }

    public class HelperTappedArgs : EventArgs
    {
        public VisualHelperTypes Type;
    }

    public sealed partial class RedditMarkdownVisualHelper : UserControl
    {
        /// <summary>
        /// Fired when an element is clicked
        /// </summary>
        public event EventHandler<HelperTappedArgs> OnHelperTapped
        {
            add => _mOnHelperTapped.Add(value);
            remove => _mOnHelperTapped.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<HelperTappedArgs>> _mOnHelperTapped = new SmartWeakEvent<EventHandler<HelperTappedArgs>>();

        public RedditMarkdownVisualHelper()
        {
            InitializeComponent();
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

        private void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            FireTapped(VisualHelperTypes.Fullscreen);
        }

        private void FireTapped(VisualHelperTypes type)
        {
            _mOnHelperTapped.Raise(this, new HelperTappedArgs { Type = type });
        }

        #region Fullscreen Status

        /// <summary>
        /// This it how we get the FullscreenStatus from the xmal binding.
        /// </summary>
        public VisualHelperFullscreenStatus FullscreenStatus
        {
            get => (VisualHelperFullscreenStatus)GetValue(FullscreenStatusProperty);
            set => SetValue(FullscreenStatusProperty, value);
        }

        public static readonly DependencyProperty FullscreenStatusProperty =
            DependencyProperty.Register(
                "FullscreenStatus",
                typeof(VisualHelperFullscreenStatus),
                typeof(CircleIconButton),
                new PropertyMetadata(VisualHelperFullscreenStatus.DontShow, OnFullscreenStatusChangedStatic));

        private static void OnFullscreenStatusChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as RedditMarkdownVisualHelper;
            instance?.OnFullscreenStatusChanged((VisualHelperFullscreenStatus)e.NewValue);
        }

        private void OnFullscreenStatusChanged(VisualHelperFullscreenStatus newValue)
        {
            if(newValue == VisualHelperFullscreenStatus.DontShow)
            {
                ui_fullscreen.Visibility = Visibility.Collapsed;
                ui_fullscreenColDef.Width = new GridLength(0);
            }
            else
            {
                ui_fullscreen.Visibility = Visibility.Visible;
                ui_fullscreenColDef.Width = new GridLength(1, GridUnitType.Star);
                if (newValue == VisualHelperFullscreenStatus.GotoFullscreen)
                {
                    ui_fullscreenSymbol.Symbol = Symbol.FullScreen;
                }
                else
                {
                    ui_fullscreenSymbol.Symbol = Symbol.BackToWindow;
                }
            }
        }

        #endregion


        #region Text Editing

        private const string CExampleUrl = "http://www.example.com";
        private const string CExampleText = "example";

        /// <summary>
        /// Preforms the selected edit type on the text box given.
        /// </summary>
        /// <param name="textBox"></param>
        /// <param name="editType"></param>
        public static void DoEdit(TextBox textBox, VisualHelperTypes editType)
        {
            // First refocus the text box since the user clicked a button.
            // We need to do this quickly so the virt keyboard doesn't move.
            textBox.Focus(FocusState.Programmatic);

            // Get some vars
            var selStart = textBox.SelectionStart;
            var selLength = textBox.SelectionLength;
            var newLineSelOffset = 0;
            var curText = textBox.Text;
            string insertNewLine = null;
            string insertAtEnd = null;
            var isLink = false;
            var hasExampleText = false;

            // For some reason the SelectionStart count /r/n as 1 instead of two. So add one for each /r/n we find.
            for(var count = 0; count < selStart + newLineSelOffset; count++)
            {
                if(curText[count] == '\r' && count + 1 < curText.Length && curText[count + 1] == '\n')
                {
                    newLineSelOffset++;
                }
            }

            // Depending on the type see what we can do.
            switch (editType)
            {
                case VisualHelperTypes.Bold:
                    if (selLength != 0)
                    {
                        // Wrap the selected text
                        textBox.Text = curText.Insert(selStart + newLineSelOffset + selLength, "**").Insert(selStart + newLineSelOffset, "**");
                    }
                    else
                    {
                        // Or add to the end
                        insertAtEnd = $"**{CExampleText}**";
                        hasExampleText = true;
                    }
                    break;
                case VisualHelperTypes.Italic:
                    if (selLength != 0)
                    {
                        // Wrap the selected text
                        textBox.Text = curText.Insert(selStart + newLineSelOffset + selLength, "*").Insert(selStart + newLineSelOffset, "*");
                    }
                    else
                    {
                        // Or add to the end
                        insertAtEnd = $"*{CExampleText}*";
                        hasExampleText = true;
                    }
                    break;
                case VisualHelperTypes.Link:
                    if (selLength != 0)
                    {
                        // Wrap the selected text
                        textBox.Text = curText.Insert(selStart + newLineSelOffset + selLength, $"]({CExampleUrl})").Insert(selStart + newLineSelOffset, "[");
                    }
                    else
                    {
                        // Or add to the end
                        insertAtEnd = $"[{CExampleText}]({CExampleUrl})";
                    }
                    isLink = true;
                    break;
                case VisualHelperTypes.NewLine:
                    var injectPos = selStart + newLineSelOffset;
                    // Inject the new line at the current pos
                    textBox.Text = curText.Insert(injectPos, "  \r\n");
                    // Move the selection to the end of insert
                    textBox.SelectionStart = injectPos + 3 - newLineSelOffset;
                    break;
                case VisualHelperTypes.Quote:
                    insertNewLine = "> ";
                    break;
                case VisualHelperTypes.List:
                    insertNewLine = "* ";
                    break;
                case VisualHelperTypes.NumberedList:
                    insertNewLine = "1. ";
                    break;
                case VisualHelperTypes.Code:
                    insertNewLine = "    ";
                    break;

            }

            // If the insert on new line is not null we need to find the last new line and
            // insert the text.
            if (insertNewLine != null)
            {
                // Search for a new line.
                var offsetSelStart = selStart + newLineSelOffset;
                var searchStart = offsetSelStart == curText.Length ? offsetSelStart - 1 : offsetSelStart;

                // Try to find it the new line before the cursor
                var indexLastNewLine = curText.LastIndexOf('\n', searchStart);

                // We need to make sure there are two new lines before this block
                var searchNewlineCount = indexLastNewLine - 1;
                while (searchNewlineCount > 0)
                {
                    // If we find an /r just move on.
                    if(curText[searchNewlineCount] == '\r')
                    {
                        searchNewlineCount--;
                        continue;
                    }
                    // If we find an /n we are good.

                    if (curText[searchNewlineCount] == '\n')
                    {
                        break;
                    }
                    // If it is anything else we need to add it.
                    insertNewLine = "\r\n" + insertNewLine;
                    break;
                }

                // Insert the text
                textBox.Text = curText.Insert(indexLastNewLine + 1, insertNewLine);

                // If where we ended up inserting was after where the selection was move it to the end.
                if(indexLastNewLine == -1 || (offsetSelStart + insertNewLine.Length) < indexLastNewLine)
                {
                    textBox.SelectionStart = offsetSelStart - newLineSelOffset + insertNewLine.Length;
                }
                else
                {
                    // If not move it back to where it was + our what we inserted.
                    textBox.SelectionStart = selStart + insertNewLine.Length;
                }
            }

            // If this isn't null we have something to add at the end of the current text.
            if (insertAtEnd != null)
            {
                // If the last char isn't a space add one.
                if (textBox.Text.Length > 0 && !char.IsWhiteSpace(textBox.Text[textBox.Text.Length - 1]))
                {
                    textBox.Text += ' ';
                }
                textBox.Text += insertAtEnd;
                textBox.SelectionStart = textBox.Text.Length;
            }

            // If we added a link try to select the example url
            if (isLink)
            {
                var urlStart = textBox.Text.LastIndexOf(CExampleUrl);
                if(urlStart != -1 && textBox.Text.Length > urlStart + CExampleUrl.Length)
                {
                    // +7 -7 so we don't selected the http://
                    textBox.SelectionStart = urlStart + 7 - newLineSelOffset;
                    textBox.SelectionLength = CExampleUrl.Length - 7;
                }
            }

            // If we have example text try to select it
            if (hasExampleText)
            {
                // Note we could accidentally select the word "example" anywhere in the text box...
                // but that's ok for now.
                var exampleStart = textBox.Text.LastIndexOf(CExampleText);
                if (exampleStart != -1 && textBox.Text.Length > exampleStart + CExampleText.Length)
                {
                    textBox.SelectionStart = exampleStart - newLineSelOffset;
                    textBox.SelectionLength = CExampleText.Length;
                }
            }
        }

        #endregion
    }
}
