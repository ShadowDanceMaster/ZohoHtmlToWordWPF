using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO;

namespace ZohoHtmlToWordWPF
{
    

    public class DocumentMerger
    {
        /// <summary>
        /// Объединяет несколько DOCX-файлов в один
        /// </summary>
        /// <param name="inputFiles">Массив путей к исходным файлам</param>
        /// <param name="outputFile">Путь к результирующему файлу</param>
        public static void MergeDocuments(string[] inputFiles, string outputFile)
        {
            // Проверка: есть ли файлы для объединения
            if (inputFiles == null || inputFiles.Length == 0)
            {
                throw new ArgumentException("Не указаны файлы для объединения");
            }

            // Создаём выходной документ
            using (WordprocessingDocument outputDocument =
                   WordprocessingDocument.Create(outputFile, WordprocessingDocumentType.Document))
            {
                // Добавляем главную часть документа
                MainDocumentPart mainPart = outputDocument.AddMainDocumentPart();
                mainPart.Document = new Document();
                mainPart.Document.AppendChild(new Body());

                // Перебираем все входные файлы
                foreach (string inputFile in inputFiles)
                {
                    if (!File.Exists(inputFile))
                    {
                        continue; // Пропускаем отсутствующие файлы
                    }

                    // Генерируем уникальный идентификатор для AltChunk
                    string altChunkId = "AltChunkId" + Guid.NewGuid().ToString("N");

                    // Добавляем в главную часть контейнер для альтернативного формата
                    AlternativeFormatImportPart chunk = mainPart.AddAlternativeFormatImportPart(
                        AlternativeFormatImportPartType.WordprocessingML,
                        altChunkId);

                    // Загружаем содержимое исходного файла в контейнер
                    using (FileStream fileStream = File.Open(inputFile, FileMode.Open))
                    {
                        chunk.FeedData(fileStream);
                    }

                    // Создаём элемент AltChunk и добавляем его в конец документа
                    AltChunk altChunk = new AltChunk();
                    altChunk.Id = altChunkId;
                    mainPart.Document.Body.AppendChild(altChunk);
                }

                // Сохраняем документ
                mainPart.Document.Save();
            }
        }
    }
}
