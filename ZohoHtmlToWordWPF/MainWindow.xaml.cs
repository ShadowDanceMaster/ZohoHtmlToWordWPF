using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.ComponentModel;
using System.Windows.Threading;
using MahApps.Metro.Controls;

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
        private readonly object _pauseLock = new object();
        private readonly object _lockObj = new object();
        static string exportPath = "../../../Тест";
        static string importPath = "../../../Импорт";
        static string defaultPath = "../../../";
        static bool windowOpened = false;
        static List<string> htmlContents = new List<string>();
        static bool exportDirChosen = false, importDirChosen = false;
        
        private delegate void UpdateProgressBarDelegate(System.Windows.DependencyProperty dp1, System.Windows.DependencyProperty dp2, Object value);
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
        private async Task ProcessFilesAsync(CancellationToken token)
        {
            WriteLineToRtb("Опускаются сумерки. Поют соловьи, падают тусклые звёзды.");

            await Dispatcher.InvokeAsync(() =>
            {
                progBarForAllFiles.Value = 0;
                progBarForOneFile.Value = 0;
            });

            #region Чтение HTML файла
            var fileNames = Directory.GetFiles(exportPath, "*.html");
            htmlContents.Clear();

            foreach (var fileName in fileNames)
            {
                token.ThrowIfCancellationRequested();
                await WaitForResumeAsync(cts.Token);
                htmlContents.Add(File.ReadAllText($"{fileName}"));
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
                await Dispatcher.InvokeAsync(() =>
                {
                    progBarForOneFile.Value = 50;
                });

                token.ThrowIfCancellationRequested();
                await WaitForResumeAsync(cts.Token);
                #region Импорт
                var importer = new ZohoNotebookSpireImporter();
                Directory.CreateDirectory(importPath);
                importer.ImportParsedNoteToWord(parsedNote, importPath, exportPath);
                #endregion

                currentFileIndex++;

                // Обновление прогресс-баров
                await Dispatcher.InvokeAsync(() =>
                {
                    progBarForOneFile.Value = 100;
                    progBarForAllFiles.Value = (int)((double)currentFileIndex / totalFiles * 100);
                });

                // Сброс прогресса для следующего файла
                await Dispatcher.InvokeAsync(() =>
                {
                    progBarForOneFile.Value = 0;
                });

                token.ThrowIfCancellationRequested();
                await WaitForResumeAsync(cts.Token);
            }

            // Финальное обновление
            await Dispatcher.InvokeAsync(() =>
            {
                progBarForOneFile.Value = 100;
                progBarForAllFiles.Value = 100;
            });

            WriteLineToRtb("Завершено!");
        }
        private async Task WaitForResumeAsync(CancellationToken token)
        {
            if (!isPaused) return;

            await Task.Run(() =>
            {
                lock (_pauseLock)
                {
                    while (isPaused && !token.IsCancellationRequested)
                    {
                        // Ожидаем сигнала о снятии с паузы или отмены
                        Monitor.Wait(_pauseLock, TimeSpan.FromMilliseconds(100));
                    }
                }
            }, token);

            token.ThrowIfCancellationRequested();
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

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            isPaused = false;
            btnPauseResume.Content = "Пауза";
            btnPauseResume.IsEnabled = true;
            btnStart.IsEnabled = false;

            cts = new CancellationTokenSource();

            try
            {
                await ProcessFilesAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                WriteLineToRtb("Операция отменена!");
                ProgBarsFinal();
            }
            catch (Exception ex)
            {
                WriteLineToRtb($"Ошибка: {ex.Message}");
            }
            finally
            {
                btnStart.IsEnabled = true;
                btnPauseResume.IsEnabled = false;
                btnPauseResume.Content = "Пауза";
            }

        }

        private void btnPauseResume_Click(object sender, RoutedEventArgs e)
        {
            lock (_pauseLock)
            {
                isPaused = !isPaused;
                btnPauseResume.Content = isPaused ? "Продолжить" : "Пауза";

                // UI оповещение о смене состояния
                WriteLineToRtb(isPaused ? "Пауза." : "Продолжаем.");

                // Если снимаем с паузы, отправляем сигнал для продолжения
                if (!isPaused)
                {
                    // Этот монитор используется в CheckPauseAsync для ожидания
                    Monitor.PulseAll(_pauseLock);
                }

            }
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
            Dispatcher.InvokeAsync(() =>
            {
                progBarForOneFile.Value = 100;
                progBarForAllFiles.Value = 100;
            }, DispatcherPriority.Background);
        }
        
    }
}