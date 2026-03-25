using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace ZohoHtmlToWordWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private delegate void UpdateProgressBarDelegate(System.Windows.DependencyProperty dp1, System.Windows.DependencyProperty dp2, Object value);
        
        private CancellationTokenSource cts;
        private volatile bool isPaused = false, isStopped=false;
        private readonly object _pauseLock = new object();
        private readonly object _lockObj = new object();
        static string exportPath = "../../../Тест";
        static string importPath = "../../../Импорт";
        static string defaultPath = "../../../";
        static bool windowOpened = false;
        static List<string> htmlContents = new List<string>();
        static bool exportDirChosen = false, importDirChosen = false;
        
        
        public MainWindow()
        {
            if (!windowOpened)
            {
                InitializeComponent();

                windowOpened = true;
                ClearRtbConsole();
                SetProgBars();
            }
        }
        public void WriteLineToRtb(string s)
        {
            rtbConsole.Dispatcher.Invoke(() => { rtbConsole.Document.Blocks.Add(new Paragraph(new Run(s))); }, DispatcherPriority.Background);
        }
        private void SetProgBars()
        {
            progBarForAllFiles.Minimum = 0;
            progBarForAllFiles.Maximum = 100;
            progBarForOneFile.Minimum = 0;
            progBarForOneFile.Maximum = 100;
        }
        private async Task ProcessFilesAsync(CancellationToken token)
        {
            WriteLineToRtb("Опускаются сумерки. Поют соловьи, падают тусклые звёзды.");
            ChangeProgBars(0, 0);

            #region Чтение HTML файла
            var fileNames = Directory.GetFiles(exportPath, "*.html");
            htmlContents.Clear();
            int count = 0;
            foreach (var fileName in fileNames)
            {
                htmlContents.Add(File.ReadAllText($"{fileName}"));
                WriteLineToRtb($"Читаем html номер {count}");
                count++;
                token.ThrowIfCancellationRequested();
                await Pause(cts.Token);
            }
            #endregion

            int currentFileIndex = 0;
            int totalFiles = htmlContents.Count;

            foreach (var htmlContent in htmlContents)
            {

                #region Паршение
                var parser = new ZohoNoteParser();
                var parsedNote = parser.ParseHtml(htmlContent);
                #endregion

                // Обновление прогресса для текущего файла
                
                ChangeProgBars(50, progBarForAllFiles.Value);
                WriteLineToRtb($"Распарсили html номер {currentFileIndex}");
                token.ThrowIfCancellationRequested();
                await Pause(cts.Token);
                #region Импорт
                var importer = new ZohoNotebookSpireImporter();
                Directory.CreateDirectory(importPath);
                importer.ImportParsedNoteToWord(parsedNote, importPath, exportPath);
                #endregion

                

                // Обновление прогресс-баров
                
                ChangeProgBars(100, (int)(((double)(currentFileIndex + 1) / totalFiles) * 100));
                WriteLineToRtb($"Импортировали заметку номер {currentFileIndex}");
                currentFileIndex++;
                token.ThrowIfCancellationRequested();
                await Pause(cts.Token);
                // Сброс прогресса для следующего файла
                ChangeProgBars(0, progBarForAllFiles.Value);


            }

            // Финальное обновление
            ChangeProgBars(100,100);

            WriteLineToRtb("Завершено!");
        }
        private async Task Pause(CancellationToken token)
        {
            bool enteredCycle = false;
            if (isPaused) WriteLineToRtb("Пауза.");
            while (isPaused)
            {
                await Task.Delay(50, token);
                token.ThrowIfCancellationRequested();
                if (!enteredCycle) enteredCycle = true;
            }
            if(enteredCycle)
                WriteLineToRtb("Продолжаем.");
        }
        
        private async void btnExport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog();
            dlg.Title = "Выберите папку с файлами html.";
            dlg.IsFolderPicker = true;
            dlg.InitialDirectory = exportPath;

            dlg.AddToMostRecentlyUsedList = false;
            dlg.AllowNonFileSystemItems = false;
            dlg.DefaultDirectory = defaultPath;
            dlg.EnsureFileExists = true;
            dlg.EnsurePathExists = true;
            dlg.EnsureReadOnly = false;
            dlg.EnsureValidNames = true;
            dlg.Multiselect = false;
            dlg.ShowPlacesList = true;

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                exportPath = dlg.FileName;
                SingletonForMainWindow.GetInstance().WriteLineToRtb("ПАПКА ЭКСПОРТА ВЫБРАНА: " + exportPath);
                exportDirChosen = true;
                if (exportDirChosen && importDirChosen)
                    ChangeButtonEnable(btnStart, true);
            }

        }

        private async void btnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CommonOpenFileDialog();
            dlg.Title = "Выберите папку для сохранения заметок.";
            dlg.IsFolderPicker = true;
            dlg.InitialDirectory = exportPath;

            dlg.AddToMostRecentlyUsedList = false;
            dlg.AllowNonFileSystemItems = false;
            dlg.DefaultDirectory = defaultPath;
            dlg.EnsureFileExists = true;
            dlg.EnsurePathExists = true;
            dlg.EnsureReadOnly = false;
            dlg.EnsureValidNames = true;
            dlg.Multiselect = false;
            dlg.ShowPlacesList = true;

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                importPath = dlg.FileName;
                SingletonForMainWindow.GetInstance().WriteLineToRtb("ПАПКА ИМПОРТА ВЫБРАНА: " + importPath);
                importDirChosen = true;
                if (exportDirChosen && importDirChosen)
                    ChangeButtonEnable(btnStart, true);
            }
        }

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {

            
            cts = new CancellationTokenSource();
            Task task;
            try
            {
                isPaused = false;
                ChangeButtonEnable(btnPauseResume, true);
                ChangeButtonContent(btnPauseResume, "Пауза");
                ChangeButtonEnable(btnStart, false);
                ChangeButtonEnable(btnExport, false);
                ChangeButtonEnable(btnImport, false);
                await ProcessFilesAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                WriteLineToRtb("Операция отменена!");
                ChangeProgBars(100,100);
            }
            catch (Exception ex)
            {
                WriteLineToRtb($"Ошибка: {ex.Message}");
            }
            finally
            {
                
                ChangeButtonEnable(btnPauseResume, false);
                ChangeButtonContent(btnPauseResume, "Пауза");
                ChangeButtonEnable(btnStart, true);
                ChangeButtonEnable(btnExport, true);
                ChangeButtonEnable(btnImport, true);
            }

        }

        private void btnPauseResume_Click(object sender, RoutedEventArgs e)
        {            
            isPaused = !isPaused;
            btnPauseResume.Content = isPaused ? "Продолжить" : "Пауза";

        }

        private void btnClearRtbConsole_Click(object sender, RoutedEventArgs e)
        {
            ClearRtbConsole();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (cts != null && !cts.IsCancellationRequested)
            {
                isPaused = false; // Снимаем с паузы, чтобы остановка сработала
                btnPauseResume.Content = "Пауза";
                cts.Cancel();
                SingletonForMainWindow.GetInstance().WriteLineToRtb("Остановка операции...");
            }
        }
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
        private void ClearRtbConsole()
        {
            rtbConsole.Dispatcher.Invoke(() => {
                    var instance = SingletonForMainWindow.GetInstance();

                    // Создаем абсолютно новый документ
                    var newDocument = new FlowDocument();
                    newDocument.Blocks.Add(new Paragraph());

                    // Заменяем документ
                    instance.rtbConsole.Document = newDocument;

                    // Сбрасываем историю
                    instance.rtbConsole.CaretPosition = instance.rtbConsole.Document.ContentStart;
                }, DispatcherPriority.Background);

        }
        private void ChangeButtonEnable( Button btn, bool enabled)
        {
            btn.Dispatcher.Invoke(() => { btn.IsEnabled = enabled; }, DispatcherPriority.Background);
        }
        private void ChangeButtonContent(Button btn, string content)
        {
            btn.Dispatcher.Invoke(() => { btn.Content = content; }, DispatcherPriority.Background);
                
        }
        private void ChangeProgBars(double oneFile, double allFiles)
        {
            progBarForOneFile.Dispatcher.Invoke(() => {
                progBarForOneFile.Value = oneFile;
            }, DispatcherPriority.Background);
            progBarForAllFiles.Dispatcher.Invoke(() => { 
                progBarForAllFiles.Value = allFiles;
            }, DispatcherPriority.Background);
        }
        
    }
}