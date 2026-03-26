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
                    }
                    
                }
                else if (o.Item1 is ListContentType lct)
                {
                    ListTypeOfContent = lct;
                }
                else if (o.Item1 is IntersectableContentType ict)
                {
                    IntersectableTypeOfContent |= ict;
                    if (IntersectableTypeOfContent.HasFlag(IntersectableContentType.Link) && o.Item2 != null && o.Item2.Select(a => a.Name).Contains("href"))
                    {
                        LinkHref = o.Item2.First(at => at.Name == "href").Value;

                    }
                    if (o.Item2 != null && o.Item2.Select(a => a.Name).Contains("style"))
                    {
                        try
                        {
                            string styleText = o.Item2.First(at => at.Name == "style").Value;
                            if (IntersectableTypeOfContent.HasFlag(IntersectableContentType.ColoredText))
                            {
                                ColourCode = ParseColour(styleText);
                            }
                            if (IntersectableTypeOfContent.HasFlag(IntersectableContentType.ColoredMarker))
                            {
                                HighlightCode = ParseColour(styleText);
                            }
                            if (IntersectableTypeOfContent.HasFlag(IntersectableContentType.ResizedText))
                            {
                                Size = ParseResizedText(styleText);
                            }
                        }
                        catch (Exception ex)
                        {

                            SingletonForMainWindow.GetInstance().WriteLineToRtb(ex.Message);
                        }
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
        public string? ColourCode { get; private set; }
        public string? HighlightCode { get; private set; }
        public int? Size { get; private set; }
        public List<List<string>> Table { get; private set; }
        public ContentType BasicTypeOfContent { get; private set; }
        public IntersectableContentType IntersectableTypeOfContent { get; private set; }
        public ListContentType ListTypeOfContent { get; private set; }
        private string? ParseColour(string styleText)
        {
            var styleTextRgb = string.Concat(styleText.SkipWhile(x => !char.IsDigit(x))).Replace(")", string.Empty);
            var RgbCode = styleTextRgb.Replace(" ", string.Empty).Split(',').ToList().Where(i => int.TryParse(i,out _)).Select(j => Convert.ToInt32(j)).ToList();
            return RgbToHexCode(RgbCode);
        }
        private string? RgbToHexCode(List<int> rgb)
        {
            if (rgb.Count != 3) throw new Exception("Неправильный RGB");
            string? hexCode = "#" + rgb[0].ToString("X2") + rgb[1].ToString("X2") + rgb[2].ToString("X2");
            return hexCode;
        }
        private int? ParseResizedText(string styleText)
        {
            var styleTextSize = string.Concat(styleText.SkipWhile(x => !char.IsDigit(x)).TakeWhile(x => char.IsDigit(x)));
            if (!int.TryParse(styleTextSize,out int result)) throw new Exception("Не удаётся извлечь размер шрифта");
            return result;
        }

    }
}
