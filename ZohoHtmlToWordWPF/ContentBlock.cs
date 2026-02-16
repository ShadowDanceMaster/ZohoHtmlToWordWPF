using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ZohoHtmlToWordWPF
{
    public class ContentBlock
    {
        public ContentBlock(Stack<(object, HtmlAttributeCollection?)> tagStack, string? text = null, List<List<string>> tableContents=null)
        {
            Text = text;
            Table = tableContents;
            BasicTypeOfContent = 0;
            ListTypeOfContent = 0;
            IntersectableTypeOfContent = 0;
            foreach (var o in tagStack)
            {
                if (o.Item1 is ContentType ct)
                {
                    BasicTypeOfContent = ct;
                    if (BasicTypeOfContent == ContentType.Image && o.Item2!=null && o.Item2.Select(a=>a.Name).Contains("src"))
                    {
                        ImageName = o.Item2.First(at => at.Name == "src").Value;
                        SingletonForMainWindow.GetInstance().WriteLineToRtb("Image src=" + ImageName);
                    }
                    
                }
                else if (o.Item1 is ListContentType lct)
                {
                    ListTypeOfContent = lct;
                }
                else if (o.Item1 is IntersectableContentType ict)
                {
                    IntersectableTypeOfContent |= ict;
                    if (IntersectableTypeOfContent==IntersectableContentType.Link && o.Item2 != null && o.Item2.Select(a => a.Name).Contains("href"))
                    {
                        LinkHref = o.Item2.First(at => at.Name == "href").Value;

                        SingletonForMainWindow.GetInstance().WriteLineToRtb("Link href=" + LinkHref);
                    }
                }
                else
                {
                    
                }
            }
        }
        public string? Text { get; private set; }
        public string? ImageName { get; private set; }
        public string? LinkHref { get; private set; }
        public List<List<string>> Table { get; private set; }
        public ContentType BasicTypeOfContent { get; private set; }
        public IntersectableContentType IntersectableTypeOfContent { get; private set; }
        public ListContentType ListTypeOfContent { get; private set; }
    }
}
