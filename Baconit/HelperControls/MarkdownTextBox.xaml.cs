using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    public sealed partial class MarkdownTextBox : UserControl
    {
        // Mark down to do!
        //
        // make an event for link clicks
        // parse subreddits like /r/baconit
        // Add list support (or something close)

        enum BlockTypes
        {
            Bold,
            Italic,
            Paragraph,
            Link,
            Quote,
            Code,
            RawHyperLink,
            RawSubreddit,
            Header,
            ListElement,
            HorizontalRule
        }

        /// <summary>
        /// Holds a list of hyperlinks we have an event on
        /// </summary>
        private List<Hyperlink> m_hyperLinks = new List<Hyperlink>();

        /// <summary>
        /// This is annoying, when a user clicks a link there is no way to associate the url to the
        /// event listener unless we set it as the URI. If we do then it will open the browser.
        /// So we use this map to associate link text with a url.
        /// </summary>
        private Dictionary<Hyperlink, string> m_hyperLinkToUrl = new Dictionary<Hyperlink, string>();

        /// <summary>
        /// Fired when the text is done parsing and formatting.
        /// </summary>
        public event EventHandler<EventArgs> OnMarkdownReady
        {
            add { m_onMarkdownReady.Add(value); }
            remove { m_onMarkdownReady.Remove(value); }
        }
        SmartWeakEvent<EventHandler<EventArgs>> m_onMarkdownReady = new SmartWeakEvent<EventHandler<EventArgs>>();

        //private string testSting = "Other videos in this thread:\n\n[Watch Playlist &amp;#9654;](http://sbtl.tv/_r3enevx?feature=playlist&amp;nline=1)\n\n\tVIDEO|VOTES - COMMENT\n\t-|-\n[Day of Failure (Infomercial compilation)](https://youtube.com/watch?v=a1ZbwqXQZPo)|[11](https://reddit.com/r/videos/comments/3enevx/_/ctgn8hl) - This video is the same idea, but way better IMO. \n[The Smiths - Please, Please, Please, Let Me Get What I Want](https://youtube.com/watch?v=GiqOsKngc-c)|[5](https://reddit.com/r/videos/comments/3enevx/_/ctgp5zu) - Great use of the Smiths. I can feel their pain. \n[Heat Two Meals in the Microwave at the Same Time!](https://youtube.com/watch?v=KEkd_7YdDro)|[4](https://reddit.com/r/videos/comments/3enevx/_/ctgq9pw) -  &amp;quot;There&amp;#39;s enough height in your microwave to fit two plates!&amp;quot; \n[John Hughes commentary - The Museum scene from Ferris Bueller's Day Off](https://youtube.com/watch?v=p89gBjHB2Gs)|[3](https://reddit.com/r/videos/comments/3enevx/_/ctgpazn) - Ferris Bueller&amp;#39;s Day Off museum scene \n[How Many Times Has This Happened To You?](https://youtube.com/watch?v=wb_GNzfEBKI)|[2](https://reddit.com/r/videos/comments/3enevx/_/ctgrk7j) - This is the best parody of these commercials I&amp;#39;ve ever seen.   Don&amp;#39;t let the fact that it&amp;#39;s collegehumor turn you away.    \n[Ferris Bueller's day off - Museum scene](https://youtube.com/watch?v=ubpRcZNJAnE)|[2](https://reddit.com/r/videos/comments/3enevx/_/ctgqr1i) - It is my favorite scene from the movie. Link \n[American Psycho - Do you like Huey Lewis and the News?](https://youtube.com/watch?v=vzN3qO-qc8U)|[1](https://reddit.com/r/videos/comments/3enevx/_/ctgrtqg) - It&amp;#39;s okay to like pop just because it&amp;#39;s good pop. It&amp;#39;s not supposed to change your way of seeing the world. \n[Morrissey - Lifeguard Sleeping Girl Drowning](https://youtube.com/watch?v=cK3PC2iYGIk)|[1](https://reddit.com/r/videos/comments/3enevx/_/ctgqwo5) - At least it wasn&amp;#39;t Lifeguard Sleeping, Girl Drowning \n[Spountin - Best Of As Seen On TV](https://youtube.com/watch?v=8-_hEOLWl_o)|[1](https://reddit.com/r/videos/comments/3enevx/_/ctgrt0e) - I&amp;#39;m guessing it&amp;#39;s &amp;quot;Spountin&amp;quot;, it turns your faucet into some sort of water fountain. \nI'm a bot working hard to help Redditors find related videos to watch.\n***\n[Info](https://np.reddit.com/r/SubtleTV/wiki/mentioned_videos) | [Chrome Extension](https://chrome.google.com/webstore/detail/mentioned-videos-for-redd/fiimkmdalmgffhibfdjnhljpnigcmohf)";
        //private string testString = "hello http://bing.com this **is a test** of [test](http://bing.com) *the bold* parser **hellow!!** test**\r\n\r\n > This is a > *quote* **test**!!!  >\r\n>>more indent *it* **bold**\r\n h*ell*o !*!*! \r t*est\r\n    this should be code like\n\r        more indent!https://quinndamerell.com/?test=55&hello=fers";

        public MarkdownTextBox()
        {
            this.InitializeComponent();
        }

        #region Markdown Logic

        /// <summary>
        /// This it how we get the post form the xmal binding.
        /// </summary>
        public string Markdown
        {
            get { return (string)GetValue(MarkdownProperty); }
            set { SetValue(MarkdownProperty, value); }
        }

        public static readonly DependencyProperty MarkdownProperty =
            DependencyProperty.Register(
                "Markdown",                     // The name of the DependencyProperty
                typeof(string),                   // The type of the DependencyProperty
                typeof(MarkdownTextBox), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    false,                      // The default value of the DependencyProperty
                    new PropertyChangedCallback(OnMarkdownChangedStatic)
                ));

        private static void OnMarkdownChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as MarkdownTextBox;
            if (instance != null)
            {
                // Send the post to the class.
                instance.OnMarkdownChanged(e.NewValue.GetType() == typeof(string) ? (string)e.NewValue : "");
            }
        }

        #endregion

        private void OnMarkdownChanged(string newMarkdown)
        {
            ConvertMarkdonwToRichTextBlock(newMarkdown);

            // When done if we set something fire the event.
            if(!String.IsNullOrWhiteSpace(newMarkdown))
            {
                m_onMarkdownReady.Raise(this, new EventArgs());
            }
        }

        private void CleanUpTextBlock()
        {
            // Clear any hyperlink events if we have any
            foreach (Hyperlink link in m_hyperLinks)
            {
                link.Click -= HyperLink_Click;
            }

            // Clear what exists
            ui_richTextBox.Blocks.Clear();
            m_hyperLinkToUrl.Clear();
            m_hyperLinks.Clear();
        }

        private void ConvertMarkdonwToRichTextBlock(string markdown)
        {
            try
            {
                CleanUpTextBlock();

                // Get the list of blocks
                List<Block> blockList = GenerateBlocksFromMarkdown(markdown);

                // Add the new
                foreach(Block block in blockList)
                {
                    ui_richTextBox.Blocks.Add(block);
                }
            }
            catch(Exception e)
            {
                App.BaconMan.MessageMan.DebugDia("Failed to parse markdown to rich", e);
            }
        }

        private List<Block> GenerateBlocksFromMarkdown(string markdown)
        {
            // Create the master list
            List<Block> blockList = new List<Block>();

            int curPos = 0;
            Paragraph currentPara = new Paragraph();
            while(curPos < markdown.Length)
            {
                // Get the next type
                BlockTypes nextBlockType = FindNextBlockType(markdown, curPos);

                switch(nextBlockType)
                {
                    case BlockTypes.Bold:
                        {
                            ParseBoldText(currentPara, markdown, ref curPos);
                            break;
                        }
                    case BlockTypes.Italic:
                        {
                            ParseItalicText(currentPara, markdown, ref curPos);
                            break;
                        }
                    case BlockTypes.Link:
                        {
                            ParseLinkText(currentPara, markdown, ref curPos);
                            break;
                        }
                    case BlockTypes.RawHyperLink:
                        {
                            ParseRawHyperLinkText(currentPara, markdown, ref curPos);
                            break;
                        }
                    case BlockTypes.RawSubreddit:
                        {
                            ParseRawSubredditLink(currentPara, markdown, ref curPos);
                            break;
                        }
                    case BlockTypes.Quote:
                        {
                            Paragraph quotePara = ParseQuoteText(currentPara, markdown, ref curPos);

                            // Add the paragraph quote completed
                            if (currentPara.Inlines.Count > 0)
                            {
                                blockList.Add(currentPara);
                            }

                            // Make the quote para current
                            currentPara = quotePara;
                            break;
                        }
                    case BlockTypes.Code:
                        {
                            Paragraph codePara = ParseCodeText(currentPara, markdown, ref curPos);

                            // Add the paragraph code completed
                            if (currentPara.Inlines.Count > 0)
                            {
                                blockList.Add(currentPara);
                            }

                            // Make the code para current
                            currentPara = codePara;
                            break;
                        }
                    case BlockTypes.Header:
                        {
                            Paragraph headerPara = ParseHeaderText(currentPara, markdown, ref curPos);

                            // Add the paragraph header completed
                            if (currentPara.Inlines.Count > 0)
                            {
                                blockList.Add(currentPara);
                            }

                            // Make the header para current
                            currentPara = headerPara;
                            break;
                        }
                    case BlockTypes.ListElement:
                        {
                            Paragraph listPara = ParseListElementText(currentPara, markdown, ref curPos);

                            // Add the paragraph list completed
                            if (currentPara.Inlines.Count > 0)
                            {
                                blockList.Add(currentPara);
                            }

                            // Make the list para current
                            currentPara = listPara;
                            break;
                        }
                    case BlockTypes.HorizontalRule:
                        {
                            Paragraph horzPara = ParseHorizontalRuleText(currentPara, markdown, ref curPos);

                            // Add the paragraph completed
                            if (currentPara.Inlines.Count > 0)
                            {
                                blockList.Add(currentPara);
                            }

                            // Make the para current
                            currentPara = horzPara;
                            break;
                        }
                    case BlockTypes.Paragraph:
                    default:
                        {
                            // Finish off the paragraph
                            ParseParagraphText(currentPara, markdown, ref curPos);

                            // Add it
                            if (currentPara.Inlines.Count > 0)
                            {
                                blockList.Add(currentPara);
                            }

                            // Make a new para
                            currentPara = new Paragraph();

                            // Give the new paragraph some spacing so you can see the boundary.
                            currentPara.Margin = new Thickness(0, 12, 0, 0);
                            break;
                        }
                }
            }

            // Add the current paragraph if it has inlines
            if (currentPara.Inlines.Count > 0)
            {
                blockList.Add(currentPara);
            }

            return blockList;
        }

        private BlockTypes FindNextBlockType(string markdown, int pos)
        {
            int currentClosesPos = 9999999;
            BlockTypes nextBlockType = BlockTypes.Paragraph;


            // Test for where the next bold is. First try to find one, if we find one see if it is the
            // next closest
            int tempPos = markdown.IndexOf("**", pos);
            if (tempPos != -1 && tempPos < currentClosesPos)
            {
                // Make sure there is an ending and it is in this paragraph.
                if(IsClosingInThisPara(markdown, tempPos + 2, "**"))
                {
                    currentClosesPos = tempPos;
                    nextBlockType = BlockTypes.Bold;
                }
            }

            // Test for italic
            // Note this will find a bold tag also, but due to this check (tempPos < currentClosesPos) it
            // will not enter.
            tempPos = markdown.IndexOf('*', pos);
            if (tempPos != -1 && tempPos < currentClosesPos)
            {
                // Make sure there is an ending and it is in this paragraph.
                // Note this needs to be +2 or we will pick up '**' for bold!
                if (markdown.Length > tempPos + 2 && IsClosingInThisPara(markdown, tempPos + 2, "*"))
                {
                    currentClosesPos = tempPos;
                    nextBlockType = BlockTypes.Italic;
                }
            }

            // Test for raw hyper links
            tempPos = markdown.IndexOf("http", pos);
            if (tempPos != -1 && tempPos < currentClosesPos)
            {
                int httpStart = markdown.IndexOf("http://", pos);
                int httpsStart = markdown.IndexOf("https://", pos);
                if (httpsStart != -1 || httpStart != -1)
                {
                    currentClosesPos = tempPos;
                    nextBlockType = BlockTypes.RawHyperLink;
                }
            }

            // Test for raw subreddit links. We need to loop here so if we find a false positive
            // we can keep checking before the current closest. Note this logic must match the logic
            // in the subreddit link parser below.
            int currentRawSubConsider = pos;
            currentRawSubConsider = markdown.IndexOf("r/", currentRawSubConsider);
            while (currentRawSubConsider != -1 && currentRawSubConsider < currentClosesPos)
            {
                // Make sure the char before the r/ is not a letter
                if (currentRawSubConsider == 0 || !Char.IsLetterOrDigit(markdown[currentRawSubConsider - 1]))
                {
                    // Make sure there is something after the r/
                    if (currentRawSubConsider + 2 < markdown.Length && Char.IsLetterOrDigit(markdown[currentRawSubConsider + 2]))
                    {
                        currentClosesPos = currentRawSubConsider;
                        nextBlockType = BlockTypes.RawSubreddit;
                        break;
                    }
                }
                currentRawSubConsider += 2;
                currentRawSubConsider = markdown.IndexOf("r/", currentRawSubConsider);
            }

            // Test for links
            // #todo we really need to make it so links can be in the middle of italics or bold
            tempPos = markdown.IndexOf('[', pos);
            if (tempPos != -1 && tempPos < currentClosesPos)
            {
                // Ensure we have a link
                int linkTextClose = markdown.IndexOf(']', tempPos);
                if(linkTextClose != -1)
                {
                    int linkOpen = markdown.IndexOf('(', linkTextClose);
                    if(linkOpen != -1)
                    {
                        int linkClose = markdown.IndexOf(')', linkOpen);
                        if (linkTextClose != -1)
                        {
                            // Make -1 be huge
                            linkTextClose = linkTextClose == -1 ? 9999999 : linkTextClose;
                            linkOpen = linkOpen == -1 ? 9999999 : linkOpen;
                            linkClose = linkClose == -1 ? 9999999 : linkClose;
                            int paraOrTextEnding = FindNextCloestNewLineOrReturn(markdown, pos, true);

                            // Make sure the order is correct
                            if (tempPos < linkTextClose && linkTextClose < linkOpen && linkOpen < linkClose && linkClose < paraOrTextEnding)
                            {
                                currentClosesPos = tempPos;
                                nextBlockType = BlockTypes.Link;
                            }
                        }
                    }
                }
            }

            // Special case! It is common for a > to start a comment off
            if(pos == 0 && markdown.Length > 0 && markdown[pos] == '>' && tempPos < currentClosesPos)
            {
                currentClosesPos = tempPos;
                nextBlockType = BlockTypes.Quote;
            }
            // The same for the header
            if (pos == 0 && markdown.Length > 0 && markdown[pos] == '#' && tempPos < currentClosesPos)
            {
                currentClosesPos = tempPos;
                nextBlockType = BlockTypes.Header;
            }

            // Test for a paragraph ending, look for /r or /n
            tempPos = FindNextCloestNewLineOrReturn(markdown, pos);
            if (tempPos != -1 && tempPos < currentClosesPos)
            {
                // Find the next char that isn't a \n, \r, or ' '
                int nextCharPos = tempPos;
                int contSpaceCount = 0;
                while (markdown.Length > nextCharPos &&
                      (markdown[nextCharPos] == '\r' || markdown[nextCharPos] == '\n' || markdown[nextCharPos] == ' '))
                {
                    if (markdown[nextCharPos] == ' ')
                    {
                        contSpaceCount++;
                    }
                    else
                    {
                        contSpaceCount = 0;
                    }
                    nextCharPos++;
                }

                // If we found 4 spaces in a row we have 'code'
                if(contSpaceCount > 3)
                {
                    currentClosesPos = tempPos;
                    nextBlockType = BlockTypes.Code;
                }
                // We have a quote; remember to check for the end of the text
                else if(markdown.Length > nextCharPos && markdown[nextCharPos] == '>')
                {
                    currentClosesPos = tempPos;
                    nextBlockType = BlockTypes.Quote;
                }
                // We have a header;
                else if (markdown.Length > nextCharPos && markdown[nextCharPos] == '#')
                {
                    currentClosesPos = tempPos;
                    nextBlockType = BlockTypes.Header;
                }
                // We have a list element;
                else if (markdown.Length > nextCharPos + 1 && markdown[nextCharPos] == '*' && markdown[nextCharPos + 1] == ' ')
                {
                    currentClosesPos = tempPos;
                    nextBlockType = BlockTypes.ListElement;
                }
                else if (markdown.IndexOf("*****", nextCharPos) == nextCharPos)
                {
                    currentClosesPos = tempPos;
                    nextBlockType = BlockTypes.HorizontalRule;
                }
                else
                {
                    currentClosesPos = tempPos;
                    nextBlockType = BlockTypes.Paragraph;
                }
            }

            return nextBlockType;
        }

        private void ParseBoldText(Paragraph currentPar, string markdown, ref int markdownPos)
        {
            int startingPos = markdown.IndexOf("**", markdownPos);
            int endingPos = markdown.IndexOf("**", (startingPos + 2));

            // Take any text that is before the bold and put in the paragraph.
            if (startingPos > markdownPos)
            {
                Run preText = new Run();
                preText.Text = markdown.Substring(markdownPos, startingPos - markdownPos);
                currentPar.Inlines.Add(preText);
            }

            // Now include the bold text
            Run boldRun = new Run();
            boldRun.FontWeight = FontWeights.Bold;

            // Set the new pos
            markdownPos = endingPos + 2;

            // Ge the pos in the correct places
            startingPos += 2;
            boldRun.Text = markdown.Substring(startingPos, endingPos - startingPos);
            currentPar.Inlines.Add(boldRun);
        }

        private void ParseItalicText(Paragraph currentPar, string markdown, ref int markdownPos)
        {
            int startingPos = markdown.IndexOf("*", markdownPos);
            int endingPos = markdown.IndexOf("*", (startingPos + 1));

            // Take any text that is before the italic and put in the paragraph.
            if (startingPos > markdownPos)
            {
                Run preText = new Run();
                preText.Text = markdown.Substring(markdownPos, startingPos - markdownPos);
                currentPar.Inlines.Add(preText);
            }

            // Now include the italic text
            Run italicRun = new Run();
            italicRun.FontStyle = FontStyle.Italic;

            // Set the new pos
            markdownPos = endingPos + 1;

            // Ge the pos in the correct places
            startingPos += 1;
            italicRun.Text = markdown.Substring(startingPos, endingPos - startingPos);
            currentPar.Inlines.Add(italicRun);
        }

        private void ParseLinkText(Paragraph currentPar, string markdown, ref int markdownPos)
        {
            // Find all of the link parts
            int linkTextOpen = markdown.IndexOf('[', markdownPos);
            int linkTextClose = markdown.IndexOf(']', linkTextOpen);
            int linkOpen = markdown.IndexOf('(', linkTextClose);
            int linkClose = markdown.IndexOf(')', linkOpen);


            // Take any text that is before the link and put in the paragraph.
            if (linkTextOpen > markdownPos)
            {
                Run preText = new Run();
                preText.Text = markdown.Substring(markdownPos, linkTextOpen - markdownPos);
                currentPar.Inlines.Add(preText);
            }

            // Grab the link text
            linkTextOpen++;
            string linkText = markdown.Substring(linkTextOpen, linkTextClose - linkTextOpen);

            // Grab the link
            linkOpen++;
            string link = markdown.Substring(linkOpen, linkClose - linkOpen);

            // Make the link.
            Run linkTextRun = new Run();
            linkTextRun.Text = linkText;
            Hyperlink hyperLink = new Hyperlink();
            hyperLink.Inlines.Add(linkTextRun);
            hyperLink.Click += HyperLink_Click;
            currentPar.Inlines.Add(hyperLink);
            m_hyperLinks.Add(hyperLink);
            m_hyperLinkToUrl.Add(hyperLink, link);

            // Set the new pos
            markdownPos = linkClose + 1;
        }

        private void ParseRawHyperLinkText(Paragraph currentPar, string markdown, ref int markdownPos)
        {
            // Find all of the link parts
            int httpStart = markdown.IndexOf("http://", markdownPos);
            int httpsStart = markdown.IndexOf("https://", markdownPos);

            httpStart = httpStart == -1 ? 9999999 : httpStart;
            httpsStart = httpsStart == -1 ? 9999999 : httpsStart;

            int linkStart = Math.Min(httpStart, httpsStart);

            // Find the end of the link
            int endOfLink = linkStart;
            int endOfParaOrText = FindNextCloestNewLineOrReturn(markdown, linkStart, true);
            while(markdown.Length > endOfLink && markdown[endOfLink] != ' ' && endOfLink < endOfParaOrText)
            {
                endOfLink++;
            }

            // Take any text that is before the link and put in the paragraph.
            if (linkStart > markdownPos)
            {
                Run preText = new Run();
                preText.Text = markdown.Substring(markdownPos, linkStart - markdownPos);
                currentPar.Inlines.Add(preText);
            }

            // Grab the link text
            string link = markdown.Substring(linkStart, endOfLink - linkStart);

            // Make the link.
            Run linkTextRun = new Run();
            linkTextRun.Text = link;
            Hyperlink hyperLink = new Hyperlink();
            hyperLink.Inlines.Add(linkTextRun);
            hyperLink.Click += HyperLink_Click;
            currentPar.Inlines.Add(hyperLink);
            m_hyperLinks.Add(hyperLink);
            m_hyperLinkToUrl.Add(hyperLink, link);

            // Set the new pos
            markdownPos = endOfLink;
        }


        private void ParseRawSubredditLink(Paragraph currentPar, string markdown, ref int markdownPos)
        {
            // Find the start of the link
            int subStart = markdown.IndexOf("r/", markdownPos);

            // Since we have the looping false positive logic above we need it here also.
            while (subStart != -1)
            {
                // Make sure the char before the r/ is not a letter
                if (subStart == 0 || !Char.IsLetterOrDigit(markdown[subStart - 1]))
                {
                    // Make sure there is something after the r/
                    if (subStart + 2 < markdown.Length && Char.IsLetterOrDigit(markdown[subStart + 2]))
                    {
                        break;
                    }
                }
                subStart += 2;
                subStart = markdown.IndexOf("r/", subStart);
            }

            // Grab where to begin looking for the end.
            int subEnd = subStart + 2;

            // Check if there is a / before it, if so include it
            if (subStart != 0 && markdown[subStart - 1] == '/')
            {
                subStart--;
            }

            // Find the end of the link, start after the r/
            int endOfParaOrText = FindNextCloestNewLineOrReturn(markdown, subStart, true);

            // While we didn't hit the end && (it is a char or digit or _ )&& and we are not past the end of the paragraph
            while (markdown.Length > subEnd && (Char.IsLetterOrDigit(markdown[subEnd]) || markdown[subEnd] == '_') && subEnd < endOfParaOrText)
            {
                subEnd++;
            }

            // Take any text that is before the sub and put in the paragraph.
            if (subStart > markdownPos)
            {
                Run preText = new Run();
                preText.Text = markdown.Substring(markdownPos, subStart - markdownPos);
                currentPar.Inlines.Add(preText);
            }

            // Grab the sub text
            string subreddit = markdown.Substring(subStart, subEnd - subStart);

            // Make the link.
            Run linkTextRun = new Run();
            linkTextRun.Text = subreddit;
            Hyperlink hyperLink = new Hyperlink();
            hyperLink.Inlines.Add(linkTextRun);
            hyperLink.Click += HyperLink_Click;
            currentPar.Inlines.Add(hyperLink);
            m_hyperLinks.Add(hyperLink);
            m_hyperLinkToUrl.Add(hyperLink, subreddit);

            // Set the new pos
            markdownPos = subEnd;
        }

        private Paragraph ParseQuoteText(Paragraph currentPar, string markdown, ref int markdownPos)
        {
            // Find the next ending
            int currentParaEnding = FindNextCloestNewLineOrReturn(markdown, markdownPos);

            // Special case! If this quote is in the very beginning we don't want to
            // consider the current ending or our text will be wrong!
            if(markdownPos == 0 && markdown[markdownPos] == '>')
            {
                currentParaEnding = 0;
            }

            // Find the > mark
            int quoteMarkPos = markdown.IndexOf('>', markdownPos);

            // Find how many are in a row
            int totalMarks = 0;
            while(markdown.Length > quoteMarkPos && markdown[quoteMarkPos] == '>')
            {
                totalMarks++;
                quoteMarkPos++;
            }

            // Take any text that is before the quote and put in the paragraph.
            if (currentParaEnding > markdownPos)
            {
                Run preText = new Run();
                preText.Text = markdown.Substring(markdownPos, currentParaEnding - markdownPos);
                currentPar.Inlines.Add(preText);
            }

            // Make the new quote paragraph
            Paragraph quotePara = new Paragraph();
            quotePara.Margin = new Thickness(totalMarks * 12,12,12,12);
            quotePara.Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));

            // Set the markdownPos
            markdownPos = quoteMarkPos;

            return quotePara;
        }

        private Paragraph ParseCodeText(Paragraph currentPar, string markdown, ref int markdownPos)
        {
            // Find the next ending
            int currentParaEnding = FindNextCloestNewLineOrReturn(markdown, markdownPos);

            // Find where the code begins
            int codeBegin = currentParaEnding;
            int spaceCount = 0;
            while(markdown.Length > codeBegin)
            {
                if(markdown[codeBegin] == ' ')
                {
                    spaceCount++;
                }
                else
                {
                    if(spaceCount > 3)
                    {
                        // We found the next char after the code begin
                        break;
                    }
                    else
                    {
                        // We found a char that broke the space count
                        spaceCount = 0;
                    }
                }
                codeBegin++;
            }

            // For every 4 spaces we want to add padding
            int paddingCount = (int)Math.Floor(spaceCount / 4.0);

            // Take any text that is before the code and put in the paragraph.
            if (currentParaEnding > markdownPos)
            {
                Run preText = new Run();
                preText.Text = markdown.Substring(markdownPos, currentParaEnding - markdownPos);
                currentPar.Inlines.Add(preText);
            }

            // Make the new code paragraph
            Paragraph codePara = new Paragraph();
            codePara.Margin = new Thickness(12 * paddingCount, 0,0,0);
            codePara.Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
            codePara.FontFamily = new FontFamily("Courier New");

            // Set the markdownPos
            markdownPos = codeBegin;

            return codePara;
        }

        private Paragraph ParseHeaderText(Paragraph currentPar, string markdown, ref int markdownPos)
        {
            // Find the next ending
            int currentParaEnding = FindNextCloestNewLineOrReturn(markdown, markdownPos);

            // Special case! If this header is in the very beginning we don't want to
            // consider the current ending or our text will be wrong!
            if (markdownPos == 0 && markdown[markdownPos] == '#')
            {
                currentParaEnding = 0;
            }

            // Find the #
            int hashPos = markdown.IndexOf('#', markdownPos);

            // Find how many are in a row
            int totalMarks = 0;
            while (markdown.Length > hashPos && markdown[hashPos] == '#')
            {
                totalMarks++;
                hashPos++;

                // To match reddit's formatting if there are more than 6 we should start showing them.
                if(totalMarks > 5)
                {
                    break;
                }
            }

            // Take any text that is before the quote and put in the paragraph.
            if (currentParaEnding > markdownPos)
            {
                Run preText = new Run();
                preText.Text = markdown.Substring(markdownPos, currentParaEnding - markdownPos);
                currentPar.Inlines.Add(preText);
            }

            // Make the new header paragraph
            Paragraph headerPara = new Paragraph();
            headerPara.Margin = new Thickness(0, 18, 0, 12);

            switch(totalMarks)
            {
                case 1:
                    headerPara.FontSize = 20;
                    headerPara.FontWeight = FontWeights.Bold;
                    break;
                case 2:
                    headerPara.FontSize = 20;
                    break;
                case 3:
                    headerPara.FontSize = 17;
                    headerPara.FontWeight = FontWeights.Bold;
                    break;
                case 4:
                    headerPara.FontSize = 17;
                    break;
                case 5:
                    headerPara.FontWeight = FontWeights.Bold;
                    break;
            }

            // Set the markdownPos
            markdownPos = hashPos;

            return headerPara;
        }

        private Paragraph ParseListElementText(Paragraph currentPar, string markdown, ref int markdownPos)
        {
            // Find the next ending
            int currentParaEnding = FindNextCloestNewLineOrReturn(markdown, markdownPos);

            // Find where the list begins
            int listStart = currentParaEnding;
            int listIndent = 1;
            while (markdown.Length > listStart)
            {
                if (markdown[listStart] == ' ')
                {
                    listIndent++;
                }
                else if(markdown[listStart] == '*')
                {
                    break;
                }
                listStart++;
            }

            // Remove one indent from the list. This doesn't work exactly like reddit's
            // but it is close enough
            listIndent = Math.Max(1, listIndent - 1);

            // Move one past it
            listStart++;

            // Take any text that is before the quote and put in the paragraph.
            if (currentParaEnding > markdownPos)
            {
                Run preText = new Run();
                preText.Text = markdown.Substring(markdownPos, currentParaEnding - markdownPos);
                currentPar.Inlines.Add(preText);
            }

            // Make the new header paragraph
            Paragraph listPara = new Paragraph();
            listPara.Margin = new Thickness(12 * listIndent, 0, 0, 0);
            Run run = new Run();
            run.Text = "•";
            listPara.Inlines.Add(run);

            // Set the markdownPos
            markdownPos = listStart;

            return listPara;
        }

        private Paragraph ParseHorizontalRuleText(Paragraph currentPar, string markdown, ref int markdownPos)
        {
            // Find the next ending
            int currentParaEnding = FindNextCloestNewLineOrReturn(markdown, markdownPos);

            // Find where the list begins
            int horzStart = markdown.IndexOf('*', currentParaEnding);

            // Find the end
            int horzEnd = horzStart;
            while (markdown.Length > horzEnd)
            {
                if (markdown[horzEnd] != '*')
                {
                    break;
                }
                horzEnd++;
            }

            // Take any text that is before the quote and put in the paragraph.
            if (currentParaEnding > markdownPos)
            {
                Run preText = new Run();
                preText.Text = markdown.Substring(markdownPos, currentParaEnding - markdownPos);
                currentPar.Inlines.Add(preText);
            }

            // This is going to be weird. To make this work we need to make a UI element
            // and fill it with text to make it stretch. If we don't fill it with text I can't
            // make it stretch the width of the box, so for now this is an "ok" hack.
            InlineUIContainer contianer = new InlineUIContainer();
            Grid grid = new Grid();
            grid.Height = 2;
            grid.Background = new SolidColorBrush(Color.FromArgb(255, 153, 153, 153));

            // Add the expanding text block.
            TextBlock magicExpandingTextBlock = new TextBlock();
            magicExpandingTextBlock.Foreground = new SolidColorBrush(Color.FromArgb(255, 153, 153, 153));
            magicExpandingTextBlock.Text = "This is Quinn writing magic text. You will never see this. Like a ghost! I love Marilyn Welniak! This needs to be really long! RRRRREEEEEAAAAALLLLYYYYY LLLOOOONNNGGGG. This is Quinn writing magic text. You will never see this. Like a ghost! I love Marilyn Welniak! This needs to be really long! RRRRREEEEEAAAAALLLLYYYYY LLLOOOONNNGGGG";
            grid.Children.Add(magicExpandingTextBlock);

            // Add the grid.
            contianer.Child = grid;

            // Make the new horizontal rule paragraph
            Paragraph horzPara = new Paragraph();
            horzPara.Margin = new Thickness(0, 12, 0, 12);
            horzPara.Inlines.Add(contianer);

            // Set the markdownPos
            markdownPos = horzEnd;

            return horzPara;
        }

        private void ParseParagraphText(Paragraph currentPar, string markdown, ref int markdownPos)
        {
            // Find the end of paragraph or the end of the text
            int endingPos = FindNextCloestNewLineOrReturn(markdown, markdownPos, true);

            // Make sure there is something to run, and not just dead space
            if(endingPos > markdownPos)
            {
                Run paraEnding = new Run();
                paraEnding.Text = markdown.Substring(markdownPos, endingPos - markdownPos);
                currentPar.Inlines.Add(paraEnding);
            }

            // Trim off any extra line endings
            while(markdown.Length > endingPos &&
                (markdown[endingPos] == '\n' || markdown[endingPos] == '\r' || markdown[endingPos] == ' '))
            {
                endingPos++;
            }

            // Update the markdown pos
            markdownPos = endingPos;
        }

        private bool IsClosingInThisPara(string markdown, int startingPos, string ending)
        {
            // Find the paragraph end
            int parahEnding = FindNextCloestNewLineOrReturn(markdown, startingPos, true);

            int posOfClosing = markdown.IndexOf(ending, startingPos);
            if(posOfClosing == -1 || posOfClosing > parahEnding)
            {
                return false;
            }
            return true;
        }

        private int FindNextCloestNewLineOrReturn(string markdown, int startingPos, bool ifNeitherExistReturnLenght = false)
        {
            // Find them both
            int newLinePos = markdown.IndexOf('\n', startingPos);
            int returnPos = markdown.IndexOf('\r', startingPos);

            if(newLinePos == -1 && returnPos == -1)
            {
                return ifNeitherExistReturnLenght ? markdown.Length : -1;
            }

            // If either are -1 make them huge
            newLinePos = newLinePos == -1 ? 99999999 : newLinePos;
            returnPos = returnPos == -1 ? 99999999 : returnPos;

            return Math.Min(newLinePos, returnPos);
        }

        private async void HyperLink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            if (m_hyperLinkToUrl.ContainsKey(sender))
            {
                string link = m_hyperLinkToUrl[sender];

                // This will take care of launching the subreddit if the link is a /r/whatever or http://www.reddit.com/r/whatever
                if (!App.BaconMan.ShowGlobalContent(link))
                {
                    // If we failed to show it try to launch the browser
                    App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "MarkdownFailedToGetGlobalContentPresenter");

                    try
                    {
                        await Windows.System.Launcher.LaunchUriAsync(new Uri(link, UriKind.Absolute));
                    }
                    catch (Exception e)
                    {
                        App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToLaunchMarkDownLink", e);
                    }
                }
            }
        }
    }
}
