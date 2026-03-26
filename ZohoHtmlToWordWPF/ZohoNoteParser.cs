using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace ZohoHtmlToWordWPF
{


    public class ZohoNoteParser
    {
        Stack<string> htmlTagStackString = new Stack<string>(100);
        /// <summary>
        /// стек html-тегов, не тегов заметок
        /// </summary>
        Stack<(object, HtmlAttributeCollection?)> htmlTagStack = new Stack<(object, HtmlAttributeCollection)> (100);
        List<ContentBlock> blocks = new List<ContentBlock>();
        int blockCount = 0;

        public class NotebookInfo
        {
            public CoverInfo Cover { get; set; }
            public string Name { get; set; }
            [JsonProperty("created_date")]
            public DateTime CreatedDate { get; set; }
            [JsonProperty("modified_date")]
            public DateTime ModifiedDate { get; set; }
        }

        public class CoverInfo
        {
            [JsonProperty("is_private")]
            public bool IsPrivate { get; set; }
            [JsonProperty("cover_id")]
            public string CoverId { get; set; }
        }

        public class NotecardInfo
        {
            public string Color { get; set; }
            public string Name { get; set; }
            [JsonProperty("created_date")]
            public DateTime CreatedDate { get; set; }
            [JsonProperty("modified_date")]
            public DateTime ModifiedDate { get; set; }
        }

        public class ParsedNote
        {
            public NotebookInfo Notebook { get; set; }
            public NotecardInfo Notecard { get; set; }
            public List<string> Tags { get; set; }
            public List<ContentBlock> ContentBlocks { get; set; } = new List<ContentBlock>();
            public string Title { get; set; }
        }



        public ParsedNote ParseHtml(string htmlContent)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            var body = doc.DocumentNode.SelectSingleNode("//body");

            var result = new ParsedNote
            {
                
                
                ContentBlocks = ParseBlocks(body),
                Title = ParseTitle(doc),
                Tags = ParseTags(body),
                Notebook = ParseNotebookInfo(body),
                Notecard = ParseNotecardInfo(body),
            };

            return result;
        }

        private NotebookInfo ParseNotebookInfo(HtmlNode body)
        {
            var notebookJson = body.GetAttributeValue("data-notebook", "").Replace("&quot;","\"").Replace("'",string.Empty);
            if (!string.IsNullOrEmpty(notebookJson))
            {
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    DateFormatString = "yyyy-MM-ddTHH:mm:sszzz"  // или "yyyy-MM-ddTHH:mm:ssK"
                };
                return JsonConvert.DeserializeObject<NotebookInfo>(notebookJson,settings);
            }
            return null;
        }

        private NotecardInfo ParseNotecardInfo(HtmlNode body)
        {
            var notecardJson = body.GetAttributeValue("data-notecard", "").Replace("&quot;", "\"").Replace("'", string.Empty);
            if (!string.IsNullOrEmpty(notecardJson))
            {
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    DateFormatString = "yyyy-MM-ddTHH:mm:sszzz"  // или "yyyy-MM-ddTHH:mm:ssK"
                };
                return JsonConvert.DeserializeObject<NotecardInfo>(notecardJson,settings);
            }
            return null;
        }

        private List<string> ParseTags(HtmlNode body)
        {
            var tagsJson = body.GetAttributeValue("data-tag", string.Empty);
            if (!string.IsNullOrEmpty(tagsJson))
            {
                var reg = new Regex(@"[\[\]'""]|&quot;");
                return reg.Replace(tagsJson, string.Empty).Trim().Split(",").ToList();
                //return tagsJson.Replace("'","").Replace("[", "").Replace("]", "").Replace("\"", "").Replace("&quot;", "").Trim().Split(",").ToList();
            }
            return new List<string>();
        }

        private string ParseTitle(HtmlDocument doc)
        {
            var titleNode = doc.DocumentNode.SelectSingleNode("//title");
            return titleNode?.InnerText?.Trim() ?? string.Empty;
        }
        private List<ContentBlock> ParseBlocks(HtmlNode body)
        {
            try
            {
                Recursive(body);
            }
            catch(Exception ex)
            {
                SingletonForMainWindow.GetInstance().WriteLineToRtb(ex.Message);
            }
            blockCount = 0;
            return blocks;
        }

        private void Recursive(HtmlNode mainNode)
        {
            var nodes = mainNode.ChildNodes.ToList();
            List<HtmlNode>? nodesFinal = nodes;
            
            for (var i=0;i< nodes.Count;i++)
            {
                
                var nodeNameObj = NodeNameToNodeEnumsElements(nodes[i].Name);
                if (nodes[i].NodeType == HtmlNodeType.Element)
                {

                    HtmlAttributeCollection? nodeAttributes = null;
                    if (nodeNameObj is null)
                    {
                        
                    }
                    if (nodes[i].HasAttributes)
                    {
                        nodeAttributes = nodes[i].Attributes;
                        if (nodeAttributes.Contains("class"))
                        {
                            if (nodeAttributes["class"].Value == "checklist")
                            {
                                nodeNameObj = NodeNameToNodeEnumsElements("checklist");
                                htmlTagStack.Push((nodeNameObj, nodeAttributes));
                                Recursive(nodes[i]);
                                InitiateHtmlTagStackPop();
                                continue;
                            }
                            if (nodeAttributes["class"].Value == "colour")
                            {
                                nodeNameObj = NodeNameToNodeEnumsElements("colour");
                                htmlTagStack.Push((nodeNameObj, nodeAttributes));
                                Recursive(nodes[i]);
                                InitiateHtmlTagStackPop();
                                continue;
                            }
                            if (nodeAttributes["class"].Value == "highlight")
                            {
                                nodeNameObj = NodeNameToNodeEnumsElements("highlight");
                                htmlTagStack.Push((nodeNameObj, nodeAttributes));
                                Recursive(nodes[i]);
                                InitiateHtmlTagStackPop();
                                continue;
                            }
                            if (nodeAttributes["class"].Value == "size")
                            {
                                nodeNameObj = NodeNameToNodeEnumsElements("size");
                                htmlTagStack.Push((nodeNameObj, nodeAttributes));
                                Recursive(nodes[i]);
                                InitiateHtmlTagStackPop();
                                continue;
                            }
                        }
                    }
                    if (nodeNameObj is not null)
                    {
                        htmlTagStack.Push((nodeNameObj, nodeAttributes));
                    }
                    
                    
                    if (nodeNameObj is ContentType && (ContentType)nodeNameObj == ContentType.Image
                        && nodeAttributes.Any()
                        && nodeAttributes.Select(at => at.Name).Any()
                        && nodeAttributes.Select(at => at.Name).Contains("src"))
                    {
                        AddToBlocks(htmlTagStack);
                    }
                    if (nodeNameObj is ContentType && (ContentType)nodeNameObj == ContentType.Table)
                    {
                        var theadTbodyTfoot = nodes[i].ChildNodes.Any() ? nodes[i].ChildNodes : null;
                        if (theadTbodyTfoot != null)
                        {
                            List<List<string>> tableList = new List<List<string>>();
                            foreach (var node in theadTbodyTfoot)
                            {
                                List<HtmlNode>? rows = node.ChildNodes.Where(n => n.Name == "tr").ToList();
                                
                                foreach (var row in rows)
                                {
                                    tableList.Add(row.ChildNodes.Where(r=>r.Name=="th"|| r.Name == "td").Select(node => HttpUtility.HtmlDecode(node.InnerText)).ToList());
                                    
                                }
                            }
                            AddToBlocks(htmlTagStack, tableContents: tableList);
                        }
                        InitiateHtmlTagStackPop(nodes[i]);
                        continue;
                    }
                    Recursive(nodes[i]);

                    InitiateHtmlTagStackPop(nodes[i]);

                }
                else
                {
                    if (nodes[i].NodeType == HtmlNodeType.Text)
                    {
                        if (!string.IsNullOrWhiteSpace(nodes[i].InnerText))
                        {
                            AddToBlocks(htmlTagStack, HttpUtility.HtmlDecode(nodes[i].InnerText));
                            
                        }
                        InitiateHtmlTagStackPop(mainNode);//избыточно?
                    }
                    
                    
                }

            }
        }
        private void InitiateHtmlTagStackPop( HtmlNode? node)
        {
            var nodeNameEnumEl = NodeNameToNodeEnumsElements(node.Name);
            if (nodeNameEnumEl is not null && htmlTagStack.Select(z=>z.Item1).Contains(nodeNameEnumEl) && !htmlTagStack.IsEmpty)
            {
                htmlTagStack.Pop();
            }
        }
        private void InitiateHtmlTagStackPop()
        {
            htmlTagStack.Pop();
        }
        private object NodeNameToNodeEnumsElements(string strNode)
        {
            switch (strNode)
            {
                case "a":
                    return IntersectableContentType.Link;
                case "b":
                    return IntersectableContentType.Bold;
                case "i":
                    return IntersectableContentType.Italic;
                case "u":
                    return IntersectableContentType.Underlined;
                case "strike":
                    return IntersectableContentType.Strike;
                case "colour":
                    return IntersectableContentType.ColoredText;
                case "highlight":
                    return IntersectableContentType.ColoredMarker;
                case "size":
                    return IntersectableContentType.ResizedText;
                case "ol":
                    return ListContentType.NumberedList;
                case "ul":
                    return ListContentType.PlainList;
                case "checklist":
                    return ListContentType.CheckList;
                case "blockquote":
                    return ContentType.BlockQuote;
                case "pre":
                    return ContentType.BlockCode;
                case "table":
                    return ContentType.Table;
                case "img":
                    return ContentType.Image;
                default:
                    return null;
            }
        }
        
        private void AddToBlocks(Stack<(object, HtmlAttributeCollection?)> tagStack, string? text = null, List<List<string>> tableContents=null)
        {
            blocks.Add(new ContentBlock(tagStack, text, tableContents));
        }

        #region потом
        private string GetTextAlignFromStyle(string style)
        {
            if (style.Contains("text-align:center")) return "center";
            if (style.Contains("text-align:right")) return "right";
            if (style.Contains("text-align:justify")) return "justify";
            return "left";
        }
        #endregion

    }

}
