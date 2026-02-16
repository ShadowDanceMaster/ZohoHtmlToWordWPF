using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Shapes;
using Spire.Doc.Formatting;
using System.ComponentModel;
using static System.Net.Mime.MediaTypeNames;
using Application = System.Windows.Application;
using Newtonsoft.Json.Linq;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using System.Runtime.InteropServices;

namespace ZohoHtmlToWordWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private BackgroundWorker worker;
        private ManualResetEventSlim pauseEvent = new ManualResetEventSlim(true);
        private CancellationTokenSource cts;
        private volatile bool isPaused = false;
        static string exportPath = "../../../Тест";
        static string importPath = "../../../Импорт";
        static string defaultPath = "../../../";
        static bool windowOpened = false;
        static List<string> htmlContents = new List<string>();
        static bool exportDirChosen = false, importDirChosen = false;
        private readonly object _lockObj = new object();
        private delegate void UpdateProgressBarDelegate(System.Windows.DependencyProperty dp1, System.Windows.DependencyProperty dp2, Object value);
        public MainWindow()
        {
            if (!windowOpened)
            {
                InitializeComponent();
                
                windowOpened = true;
                ClearRtbConsole();
                SetProgBars();
                SetBgWorker();
            }
        }
        public void WriteLineToRtb(string s)
        {
            lock (_lockObj)
            {
                Dispatcher.Invoke(() =>
            {
                var instance = SingletonForMainWindow.GetInstance();

                // Важно: проверяем, что документ существует
                if (instance.rtbConsole.Document == null)
                {
                    instance.rtbConsole.Document = new FlowDocument();
                }

                // Добавляем новый параграф
                instance.rtbConsole.Document.Blocks.Add(new Paragraph(new Run(s)));

                // Прокручиваем к последней строке
                instance.rtbConsole.ScrollToEnd();
            });
            }
        }
        private void SetProgBars()
        {
            progBarForAllFiles.Minimum = 0;
            progBarForAllFiles.Maximum = 100;
            progBarForOneFile.Minimum = 0;
            progBarForOneFile.Maximum = 100;
        }
        private void SetBgWorker()
        {
            // Создание BackgroundWorker
            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += WorkerDoWork;
            worker.ProgressChanged += BarsProgressChanged;
            worker.RunWorkerCompleted += WorkerRunWorkerCompleted;
        }
        private void BarsProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.UserState is ProgressData data)
                {
                    progBarForOneFile.Value = data.ProgressOneFile;
                    progBarForAllFiles.Value = data.ProgressAllFiles;


                }
            });


        }

        private void WorkerRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                SingletonForMainWindow.GetInstance().WriteLineToRtb("Отменено!");
                ProgBarsFinal();
            }
            else if (e.Error != null)
            {
                SingletonForMainWindow.GetInstance().WriteLineToRtb("Ошибка!");
            }
            else
            {
                SingletonForMainWindow.GetInstance().WriteLineToRtb("Завершено!");
                ProgBarsFinal();
            }
        }
        private void WorkerDoWork(object sender, DoWorkEventArgs e)
        {
            cts = new CancellationTokenSource();
            try
            {
                SingletonForMainWindow.GetInstance().WriteLineToRtb("Опускаются сумерки. Поют соловьи, падают тусклые звёзды.");

                Dispatcher.Invoke(() => btnPauseResume.IsEnabled = true);

                var progressData = new ProgressData()
                {
                    ProgressOneFile = 0,
                    ProgressAllFiles = 0
                };
                worker.ReportProgress(progressData.ProgressAllFiles, progressData);

                #region Чтение HTML файла
                var fileNames = Directory.GetFiles(exportPath, "*.html");
                foreach (var fileName in fileNames)
                {

                    if (worker.CancellationPending || cts.Token.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }
                    htmlContents.Add(File.ReadAllText($"{fileName}"));
                }
                #endregion
                foreach (var htmlContent in htmlContents)
                {

                    #region Паршение
                    var parser = new ZohoNoteParser();
                    var parsedNote = parser.ParseHtml(htmlContent);
                    #endregion
                    progressData.ProgressOneFile = 50;
                    progressData.ProgressAllFiles += 50 / htmlContents.Count;
                    worker.ReportProgress(progressData.ProgressAllFiles, progressData);
                    if (worker.CancellationPending || cts.Token.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }

                    #region Импорт
                    var importer = new ZohoNotebookSpireImporter();
                    Directory.CreateDirectory(importPath);
                    importer.ImportParsedNoteToWord(parsedNote, importPath, exportPath);
                    #endregion

                    Dispatcher.Invoke(() => progBarForOneFile.Value = 0);
                    progressData.ProgressOneFile = 99;
                    progressData.ProgressAllFiles += 50 / htmlContents.Count;
                    worker.ReportProgress(progressData.ProgressAllFiles, progressData);
                    // Проверка отмены
                    if (worker.CancellationPending || cts.Token.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }

                    // Проверка паузы
                    pauseEvent.Wait(cts.Token);
                }
                progressData.ProgressOneFile = 100;
                progressData.ProgressAllFiles = 100;
                worker.ReportProgress(progressData.ProgressAllFiles, progressData);
            }
            catch (OperationCanceledException ex)
            {
                e.Cancel = true;

                SingletonForMainWindow.GetInstance().WriteLineToRtb(ex.Message);
            }
            catch (Exception exep)
            {
                SingletonForMainWindow.GetInstance().WriteLineToRtb(exep.Message);
            }
            e.Result = "Завершено";
            Dispatcher.Invoke(() => btnPauseResume.IsEnabled = false);
            Dispatcher.Invoke(() => btnStart.IsEnabled = true);

        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
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
                    btnStart.IsEnabled = true;
            }

        }

        private void btnImport_Click(object sender, RoutedEventArgs e)
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
                    btnStart.IsEnabled = true;
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            pauseEvent.Set();
            isPaused = false;
            btnPauseResume.Content = isPaused ? "Продолжить" : "Пауза";
            if (!worker.IsBusy)
                worker.RunWorkerAsync();
            else
            {

            }
        }

        private void btnPauseResume_Click(object sender, RoutedEventArgs e)
        {
            if (!worker.IsBusy) return;
            btnStart.IsEnabled = false;
            if (!isPaused)
            {
                // Пауза

                pauseEvent.Reset();
                isPaused = true;
                Dispatcher.Invoke(() =>
                {

                    SingletonForMainWindow.GetInstance().WriteLineToRtb("Пауза.");
                });
            }
            else
            {
                // Продолжение

                pauseEvent.Set();
                isPaused = false;
                Dispatcher.Invoke(() =>
                {
                    SingletonForMainWindow.GetInstance().WriteLineToRtb("Продолжаем.");
                });
            }
            btnPauseResume.Content = isPaused ? "Продолжить" : "Пауза";
        }

        private void btnClearRtbConsole_Click(object sender, RoutedEventArgs e)
        {
            ClearRtbConsole();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (worker.IsBusy && worker.WorkerSupportsCancellation)
            {
                pauseEvent.Reset();
                isPaused = true;
                btnPauseResume.Content = isPaused ? "Продолжить" : "Пауза";

                btnStart.IsEnabled = true;
                worker.CancelAsync(); // Устанавливаем флаг отмены
                // RunWorkerCompleted вызовется после проверки CancellationPending в DoWork
                cts?.Cancel();
                ProgBarsFinal();


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
            Dispatcher.Invoke(new Action(() =>
            {
                var instance = SingletonForMainWindow.GetInstance();

                // Создаем абсолютно новый документ
                var newDocument = new FlowDocument();
                newDocument.Blocks.Add(new Paragraph());

                // Заменяем документ
                instance.rtbConsole.Document = newDocument;

                // Сбрасываем историю
                instance.rtbConsole.CaretPosition = instance.rtbConsole.Document.ContentStart;
            }), DispatcherPriority.Normal);

        }
        private void ProgBarsFinal()
        {
            var progressData = new ProgressData()
            {
                ProgressOneFile = 100,
                ProgressAllFiles = 100
            };
            progBarForOneFile.Dispatcher.Invoke(() => progBarForOneFile.Value = progressData.ProgressOneFile, DispatcherPriority.Background);
            progBarForAllFiles.Dispatcher.Invoke(() => progBarForAllFiles.Value = progressData.ProgressAllFiles, DispatcherPriority.Background);
        }
        
    }
}