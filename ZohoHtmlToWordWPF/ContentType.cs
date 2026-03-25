using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZohoHtmlToWordWPF
{
    
    public enum ContentType
    {
        None=0,
        BlockQuote = 1,
        BlockCode=2,
        Table=3,
        /// <summary>
        /// не текст, картинка; должна быть несовместима с ListContentType, IntersectableContentType
        /// </summary>
        Image = 4,



    }
    public enum ListContentType 
    { 
        NotList=0,
        NumberedList=1,
        PlainList=2,
        CheckList = 3

    }

    [Flags]
    public enum IntersectableContentType
    {
        None=0,
        Link = 1,
        Bold = 2,
        Italic = 4,
        Underlined = 8,
        Strike = 16,
        ColoredText = 32,
        ColoredMarker = 64,
        ResizedText=128
    }
}
