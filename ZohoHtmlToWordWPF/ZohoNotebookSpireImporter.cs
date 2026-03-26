using System;
using System.Collections.Generic;
using System.IO;
using Spire.Doc;
using Spire.Doc.Documents;
using Spire.Doc.Fields;
using System.Drawing;
using static ZohoHtmlToWordWPF.ZohoNoteParser;
using Spire.Doc.Formatting;
using ControlzEx.Standard;

namespace ZohoHtmlToWordWPF
{
    public class ZohoNotebookSpireImporter
    {
        string importPath = string.Empty;
        string exportDir = string.Empty;
        string fullPath = string.Empty;
        int countSubFiles = 0;
        int countUnnamedNote = 0;
        /// <summary>
        /// Импортирует распаршенную заметку в ворд с использованием Spire.Doc.
        /// </summary>
        /// <param name="parsedNote">распаршенная заметка</param>
        /// <param name="importDir">директория (куда сохранять) без имени файла</param>
        public void ImportParsedNoteToWord(ParsedNote parsedNote, string importDir, string exportDir)
        {
            this.exportDir = exportDir;
            var parsedNoteNotebookName = parsedNote.Notebook != null ? new string(parsedNote.Notebook.Name.Trim().Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()) : "Безымянный";
            var filePath= $"{parsedNoteNotebookName}/{new string(parsedNote.Title.Trim().Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray()) ?? $"безымянная заметка {++countUnnamedNote}"}.docx";
            fullPath = $"{importDir}/{filePath}";
            importPath = Path.GetDirectoryName(fullPath);

            // Создаем директорию, если она не существует
            if (!Directory.Exists(importPath))
            {
                Directory.CreateDirectory(importPath);
            }
            if (File.Exists(fullPath))
            {
                SingletonForMainWindow.GetInstance().WriteLineToRtb($"файл {filePath} уже существует");
                return;
            }
            else
            {
                SingletonForMainWindow.GetInstance().WriteLineToRtb($"файл {filePath} будет добавлен");
            }
            // Создаем новый документ
            Document document = new Document();

            try
            {
                countSubFiles = 0;
                // Добавляем заголовок
                AddTitle(document, parsedNote.Title);

                // Добавляем информацию о блокноте
                AddNotebookInfo(document, parsedNote.Notebook);

                // Добавляем информацию о карточке
                AddNotecardInfo(document, parsedNote.Notecard);

                // Добавляем теги
                AddTags(document, parsedNote.Tags);

                // Добавляем контентные блоки
                AddContentBlocks(ref document, parsedNote.ContentBlocks);

                if (countSubFiles > 0)
                {
                    countSubFiles++;
                    document.SaveToFile(new string(fullPath.Take(fullPath.Length - 5).ToArray()) + $" ({countSubFiles}).docx", FileFormat.Docx);
                    SingletonForMainWindow.GetInstance().WriteLineToRtb($"файл {filePath} разбит на {countSubFiles} частей");
                    string[] inputFilesPaths = new string[countSubFiles];
                    for (int i=1; i <= inputFilesPaths.Length; i++)
                    {
                        inputFilesPaths[i-1] = new string(fullPath.Take(fullPath.Length - 5).ToArray()) + $" ({i}).docx";
                    }
                    DocumentMerger.MergeDocuments(inputFilesPaths, new string(fullPath.Take(fullPath.Length - 5).ToArray()) + $" (общий).docx");
                }
                else
                {
                    document.SaveToFile(fullPath, FileFormat.Docx);
                }
            }
            finally
            {
                document.Dispose();
            }
        }
        private void AddTitle(Document document, string title)
        {
            if (string.IsNullOrEmpty(title))
                return;

            Paragraph paragraph = document.AddSection().AddParagraph();
            paragraph.AppendText(title);
            paragraph.Format.HorizontalAlignment = HorizontalAlignment.Center;

            // Настраиваем стиль заголовка
            paragraph.ApplyStyle(BuiltinStyle.Heading1);
            CharacterFormat charFormat = paragraph.Items[0] is TextRange textRange ?
                textRange.CharacterFormat : new CharacterFormat(document);
            charFormat.FontSize = 16;
            charFormat.Bold = true;

            // Добавляем пустую строку после заголовка
            document.Sections[0].AddParagraph();
        }

        private void AddNotebookInfo(Document document, NotebookInfo notebook)
        {
            if (notebook == null)
                return;

            Section section = document.Sections[0];

            // Название блокнота
            Paragraph paragraph = section.AddParagraph();
            TextRange textRange = paragraph.AppendText($"Блокнот: {notebook.Name}");
            paragraph.Format.HorizontalAlignment = HorizontalAlignment.Left;
            paragraph.ApplyStyle(BuiltinStyle.Normal);
            textRange.CharacterFormat.FontSize = 12;

            // Дата создания
            paragraph = section.AddParagraph();
            textRange = paragraph.AppendText($"Создан: {notebook.CreatedDate:dd.MM.yyyy HH:mm}");
            paragraph.ApplyStyle(BuiltinStyle.Normal);
            textRange.CharacterFormat.FontSize = 10;

            // Дата изменения
            paragraph = section.AddParagraph();
            textRange = paragraph.AppendText($"Изменен: {notebook.ModifiedDate:dd.MM.yyyy HH:mm}");
            paragraph.ApplyStyle(BuiltinStyle.Normal);
            textRange.CharacterFormat.FontSize = 10;

            // Добавляем пустую строку
            section.AddParagraph();
        }

        private void AddNotecardInfo(Document document, NotecardInfo notecard)
        {
            if (notecard == null)
                return;

            Paragraph paragraph = document.Sections[0].AddParagraph();
            TextRange textRange = paragraph.AppendText($"Карточка: {notecard.Name}");
            paragraph.Format.HorizontalAlignment = HorizontalAlignment.Left;

            paragraph.ApplyStyle(BuiltinStyle.Normal);
            textRange.CharacterFormat.FontSize = 12;
            textRange.CharacterFormat.Bold = true;

            // Добавляем пустую строку
            document.Sections[0].AddParagraph();
        }

        private void AddTags(Document document, List<string> tags)
        {
            if (tags == null || tags.Count == 0)
                return;

            Paragraph paragraph = document.Sections[0].AddParagraph();
            TextRange textRange = paragraph.AppendText("Теги: " + string.Join(", ", tags));

            paragraph.ApplyStyle(BuiltinStyle.Normal);
            textRange.CharacterFormat.FontSize = 10;
            textRange.CharacterFormat.Italic = true;

            // Добавляем пустую строку
            document.Sections[0].AddParagraph();
        }

        private void AddContentBlocks(ref Document document, List<ContentBlock> contentBlocks)
        {
            if (contentBlocks == null || contentBlocks.Count == 0)
                throw new Exception("Нет блока контента");

            foreach (var block in contentBlocks)
            {
                AddContentBlock(ref document, block);
            }
        }

        private void AddContentBlock(ref Document document, ContentBlock block)
        {
            if (block == null)
                return;

            // Обработка изображений
            if (block.BasicTypeOfContent == ContentType.Image)
            {
                AddImage(document, block);
                return;
            }
            Section section = document.Sections.Count > 0 ? document.Sections[0] : document.AddSection();
            // Создаем параграф для текстового блока

            int countParagraphs = section.Paragraphs.Count;
            
            if (countParagraphs > 400)
            {
                countSubFiles++;
                document.SaveToFile(new string(fullPath.Take(fullPath.Length-5).ToArray())+$" ({countSubFiles}).docx", FileFormat.Docx);
                document = new Document();
            }

            Paragraph paragraph = section.AddParagraph();
            Table table = null;
            // Добавляем текст
            TextRange textRange = null;
            if (!string.IsNullOrEmpty(block.Text))
            {
                if (!block.IntersectableTypeOfContent.HasFlag(IntersectableContentType.Link) && block.ListTypeOfContent != ListContentType.CheckList)
                    textRange = paragraph.AppendText(block.Text);

                paragraph.ApplyStyle(BuiltinStyle.Normal);
            }
            else
            {
                textRange = paragraph.AppendText(string.Empty);
                paragraph.ApplyStyle(BuiltinStyle.Normal);
            }
            if (block.BasicTypeOfContent == ContentType.Table)
            {
                table = section.AddTable(true);
                var rowCount = block.Table.Count;
                var maxFieldInRowCount = block.Table.Max(list => list.Count);
                table.ResetCells(rowCount, maxFieldInRowCount);
                // Заполняем таблицу данными
                for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    var rowData = block.Table[rowIndex];

                    for (int colIndex = 0; colIndex < rowData.Count; colIndex++)
                    {
                        if (colIndex < rowData.Count)
                        {
                            table.Rows[rowIndex].Cells[colIndex].AddParagraph().AppendText(rowData[colIndex]);
                            //Paragraph cellParagraph = table.Rows[rowIndex].Cells[colIndex].AddParagraph();
                            //cellParagraph.AppendText(rowData[colIndex]);

                            // Опционально: настройка выравнивания
                            paragraph.Format.HorizontalAlignment = HorizontalAlignment.Left;
                        }
                    }
                }

            }

            // Применяем стили в зависимости от типа контента
            ApplyContentTypeStyles(paragraph, textRange, block, table);
            ApplyListStyles(paragraph, ref textRange, block);
            ApplyIntersectableStyles(ref paragraph, textRange, block);

        }

        private void ApplyContentTypeStyles(Paragraph paragraph, TextRange textRange, ContentBlock block, Table table = null)
        {
            if (textRange == null)
                return;

            switch (block.BasicTypeOfContent)
            {
                case ContentType.BlockQuote:
                    paragraph.Format.LeftIndent = 20;
                    paragraph.Format.RightIndent = 20;
                    textRange.CharacterFormat.Italic = true;
                    textRange.CharacterFormat.TextColor = Color.Gray;
                    break;

                case ContentType.BlockCode:
                    textRange.CharacterFormat.FontName = "Consolas";
                    textRange.CharacterFormat.FontSize = 10;
                    paragraph.Format.BackColor = Color.LightGray;
                    paragraph.Format.LeftIndent = 20;
                    paragraph.Format.RightIndent = 20;
                    break;
                case ContentType.Table:
                    table.TableFormat.Paddings.All = 10;
                    table.TableFormat.Borders.BorderType = BorderStyle.Single;
                    table.TableFormat.Borders.Color = Color.Black;
                    break;
                default:
                    break;
            }
        }

        private void ApplyListStyles(Paragraph paragraph, ref TextRange textRange, ContentBlock block)
        {
            switch (block.ListTypeOfContent)
            {
                case ListContentType.NumberedList:
                    paragraph.ListFormat.ApplyNumberedStyle();
                    break;

                case ListContentType.PlainList:
                    paragraph.ListFormat.ApplyBulletStyle();
                    break;

                case ListContentType.CheckList:
                    // Получаем существующий текст и добавляем чекбокс
                    textRange=paragraph.AppendText("☐ " + block.Text);
                    break;
                default:
                    break;
            }
        }

        private void ApplyIntersectableStyles(ref Paragraph paragraph, TextRange textRange, ContentBlock block)
        {
            if (string.IsNullOrEmpty(block.Text))
                return;
            if (block.IntersectableTypeOfContent.HasFlag(IntersectableContentType.Link))
            {
                if (!string.IsNullOrEmpty(block.LinkHref))
                {
                    AddHyperlink(ref paragraph, ref textRange, block.LinkHref, block.Text!);
                }
            }

            if (block.IntersectableTypeOfContent.HasFlag(IntersectableContentType.Bold))
            {
                textRange.CharacterFormat.Bold = true;
            }

            if (block.IntersectableTypeOfContent.HasFlag(IntersectableContentType.Italic))
            {
                textRange.CharacterFormat.Italic = true;
            }

            if (block.IntersectableTypeOfContent.HasFlag(IntersectableContentType.Underlined))
            {
                textRange.CharacterFormat.UnderlineStyle = UnderlineStyle.Single;
            }

            if (block.IntersectableTypeOfContent.HasFlag(IntersectableContentType.Strike))
            {
                textRange.CharacterFormat.IsStrikeout = true;
            }
            if (block.IntersectableTypeOfContent.HasFlag(IntersectableContentType.ColoredText))
            {
                if (block.ColourCode is not null)
                    textRange.CharacterFormat.TextColor = ColorTranslator.FromHtml(block.ColourCode);
                else textRange.CharacterFormat.TextColor = Color.Black;
            }
            if (block.IntersectableTypeOfContent.HasFlag(IntersectableContentType.ColoredMarker))
            {
                if (block.HighlightCode is not null)
                    textRange.CharacterFormat.HighlightColor = ColorTranslator.FromHtml(block.HighlightCode);
                else textRange.CharacterFormat.HighlightColor = Color.LightGreen;
            }
            if (block.IntersectableTypeOfContent.HasFlag(IntersectableContentType.ResizedText))
            {
                if (block.Size is not null)
                    textRange.CharacterFormat.FontSize = (float)block.Size;
            }




        }

        private void AddImage(Document document, ContentBlock block)
        {
            if (string.IsNullOrEmpty(block.ImageName))
                return;

            try
            {
                // Проверяем, существует ли файл изображения
                if (File.Exists(exportDir + "\\" + block.ImageName))
                {
                    Paragraph paragraph = document.Sections[0].AddParagraph();

                    // Вставляем изображение
                    DocPicture picture = paragraph.AppendPicture(Image.FromFile(exportDir + "\\" + block.ImageName));

                    // Настраиваем размеры изображения
                    picture.Width = 200;
                    picture.Height = 150;

                    // Центрируем изображение
                    paragraph.Format.HorizontalAlignment = HorizontalAlignment.Center;
                }
                else
                {
                    Paragraph paragraph = document.Sections[0].AddParagraph();
                    TextRange textRange = paragraph.AppendText($"[Изображение: {block.ImageName} не загружено (не найдено)]");
                    paragraph.ApplyStyle(BuiltinStyle.Normal);
                    textRange.CharacterFormat.Italic = true;
                    textRange.CharacterFormat.TextColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                Paragraph paragraph = document.Sections[0].AddParagraph();
                TextRange textRange = paragraph.AppendText($"Ошибка загрузки изображения: {ex.Message}");
                paragraph.ApplyStyle(BuiltinStyle.Normal);
            }
        }

        private void AddHyperlink(ref Paragraph paragraph, ref TextRange textRange, string href, string text)
        {
            try
            {
                // Добавляем гиперссылку
                textRange=paragraph.AppendHyperlink(
                    href,
                    !string.IsNullOrEmpty(text) ? text : href,
                    HyperlinkType.WebLink
                );
            }
            catch (Exception ex)
            {
                // В случае ошибки просто добавляем текст ссылки
                textRange=paragraph.AppendText($"(ошибка) {text} [{href}]");
            }
        }
    }
}