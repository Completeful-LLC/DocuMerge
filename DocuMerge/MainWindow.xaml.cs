using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using iText.Kernel.Utils;
using Microsoft.Win32;
using PdfiumViewer;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using PrintDialog = System.Windows.Controls.PrintDialog;

namespace PDFCompiler
{
    public partial class MainWindow : Window
    {
        private List<string> pdfFiles;
        private bool showPdfImages;
        private PrintDocument printDocument;
        private double zoomLevel;
        //New Dictionary
        private Dictionary<string, List<int>> highlightedPages;
        public MainWindow()
        {
            InitializeComponent();
            pdfFiles = new List<string>();
            showPdfImages = false;
            zoomLevel = 1.0;
            highlightedPages = new Dictionary<string, List<int>>();
            UpdateImageCount();
        }

        private async void BtnAddPdf_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "PDF files (*.pdf)|*.pdf"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                ShowLoadingScreen(true);
                await AddPdfsAsync(openFileDialog.FileNames);
                ShowLoadingScreen(false);
                UpdateImageCount();
            }
        }

        private async Task AddPdfsAsync(string[] fileNames)
        {
            await Task.Run(() =>
            {
                foreach (string filename in fileNames)
                {
                    Debug.WriteLine("Adding PDF: " + filename);
                    pdfFiles.Add(filename);
                    Dispatcher.Invoke(() => AddPdfToPanel(filename));
                }
            });
        }

        private void ShowLoadingScreen(bool show)
        {
            loadingGrid.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            btnAddPdf.IsEnabled = !show;
            btnClearPdf.IsEnabled = !show;
            btnCombinePdf.IsEnabled = !show;
            btnPrint.IsEnabled = !show;
            btnClose.IsEnabled = !show;
            btnToggleView.IsEnabled = !show;
        }

        private void BtnClearPdf_Click(object sender, RoutedEventArgs e)
        {
            pdfFiles.Clear();
            pdfPanel.Children.Clear();
            highlightedPages.Clear();
            UpdateImageCount();
        }

        private void BtnCombinePdf_Click(object sender, RoutedEventArgs e)
        {
            if (pdfFiles.Count < 2)
            {
                System.Windows.MessageBox.Show("Please add at least two PDFs to combine.");
                return;
            }
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                CombinePdfs(pdfFiles, saveFileDialog.FileName);
                System.Windows.MessageBox.Show("PDFs combined successfully.");
            }
        }

        private void CombinePdfs(List<string> pdfFiles, string outputPath)
        {
            Debug.WriteLine("Output Path: " + outputPath);
            using (var pdf = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfWriter(outputPath)))
            {
                PdfMerger merger = new PdfMerger(pdf);
                int pageOffset = 0;

                foreach (string file in pdfFiles)
                {
                    Debug.WriteLine("Combining PDF: " + file);
                    using (var sourcePdf = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(file)))
                    {
                        int numberOfPages = sourcePdf.GetNumberOfPages();
                        merger.Merge(sourcePdf, 1, numberOfPages);

                        if (highlightedPages.ContainsKey(file))
                        {
                            if (!highlightedPages.ContainsKey(outputPath))
                            {
                                highlightedPages[outputPath] = new List<int>();
                            }

                            foreach (int page in highlightedPages[file])
                            {
                                highlightedPages[outputPath].Add(pageOffset + page);
                            }
                        }

                        pageOffset += numberOfPages;
                    }
                }
            }
        }

        private List<string> GetHighlightedFiles()
        {
            List<string> highlightedFiles = new List<string>();

            foreach (var entry in highlightedPages)
            {
                if (entry.Value.Count > 0) // Ensure there are highlighted pages  
                {
                    highlightedFiles.Add(entry.Key);
                }
            }

            return highlightedFiles;
        }
        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            var filesToPrint = GetHighlightedFiles();

            if (filesToPrint.Count == 0)
            {
                filesToPrint = new List<string>(pdfFiles);
                Debug.WriteLine("No highlighted files, using all files.");
            }
            else
            {
                Debug.WriteLine("Using highlighted files.");
            }

            if (filesToPrint.Count < 1)
            {
                System.Windows.MessageBox.Show("Please add a PDF to print.");
                return;
            }

            string tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf");
            bool is2x2Label = filesToPrint.Any(file =>
            {
                string fileName = Path.GetFileName(file).ToUpper();
                return fileName.StartsWith("GIL") || fileName.StartsWith("BEL") || fileName.StartsWith("HAN") || fileName.StartsWith("LAT");
            });

            CombinePdfs(filesToPrint, tempFilePath);
            PrintPdf(tempFilePath, is2x2Label);
        }

        private int currentPageIndex;
        private List<int> pagesToPrint;

        private void PrintPdf(string filePath, bool is2x2Label)
        {
            try
            {
                Debug.WriteLine("PrintPdf: Starting print process for filePath: " + filePath);

                var document = PdfiumViewer.PdfDocument.Load(filePath);
                if (highlightedPages.ContainsKey(filePath))
                {
                    pagesToPrint = highlightedPages[filePath];
                    Debug.WriteLine("PrintPdf: Highlighted pages found:");
                    foreach (var page in pagesToPrint)
                    {
                        Debug.WriteLine($"Page {page}");
                    }
                }
                else
                {
                    pagesToPrint = Enumerable.Range(1, document.PageCount).ToList();
                    Debug.WriteLine("PrintPdf: No highlighted pages, printing all pages:");
                    foreach (var page in pagesToPrint)
                    {
                        Debug.WriteLine($"Page {page}");
                    }
                }

                currentPageIndex = 0;

                printDocument = new PrintDocument();
                printDocument.PrintPage += (sender, e) =>
                {
                    if (currentPageIndex < pagesToPrint.Count)
                    {
                        int page = pagesToPrint[currentPageIndex];
                        Debug.WriteLine($"Printing page {page}");

                        var image = document.Render(page - 1, e.PageBounds.Width, e.PageBounds.Height, true);
                        e.Graphics.DrawImage(image, 0, 0, e.PageBounds.Width, e.PageBounds.Height);
                        currentPageIndex++;
                        e.HasMorePages = currentPageIndex < pagesToPrint.Count;
                    }
                    else
                    {
                        e.HasMorePages = false;
                    }
                };

                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    Debug.WriteLine("PrintPdf: Print dialog confirmed.");
                    printDocument.PrinterSettings.PrinterName = printDialog.PrintQueue.FullName;
                    printDocument.PrintController = new StandardPrintController();

                    if (is2x2Label)
                    {
                        Debug.WriteLine("PrintPdf: Matched 2x2 label condition for filePath: " + filePath);
                        printDocument.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
                        printDocument.DefaultPageSettings.PaperSize = new PaperSize("Custom", 200, 200);
                    }

                    printDocument.Print();
                }
                else
                {
                    Debug.WriteLine("PrintPdf: Print dialog canceled.");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("An error occurred while trying to print the PDF: " + ex.Message);
                Debug.WriteLine("PrintPdf: Exception occurred - " + ex.Message);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void PdfPanel_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    if (Path.GetExtension(file).ToLower() == ".pdf")
                    {
                        pdfFiles.Add(file);
                        AddPdfToPanel(file);
                    }
                }
                UpdateImageCount();
            }
        }

        private void PdfPanel_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void BtnToggleView_Click(object sender, RoutedEventArgs e)
        {
            showPdfImages = !showPdfImages;
            UpdatePanel();
            placeholderTextBlock.Visibility = showPdfImages ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Debug.WriteLine("Mouse wheel event triggered");
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                Debug.WriteLine("Ctrl key is down");
                if (e.Delta > 0)
                {
                    Debug.WriteLine("Zooming in");
                    zoomLevel += 0.2;
                }
                else if (e.Delta < 0)
                {
                    Debug.WriteLine("Zooming out");
                    zoomLevel -= 0.2;
                }

                zoomLevel = Math.Max(0.1, Math.Min(zoomLevel, 3.0)); // Clamp zoom level between 0.1 and 3.0  
                Debug.WriteLine($"Zoom level set to {zoomLevel}");
                UpdatePanel();
                e.Handled = true; // Mark the event as handled  
            }
        }
private void UpdatePanel()  
{  
    if (pdfFiles == null)  
        return;  
  
    pdfPanel.Children.Clear();  
    foreach (string file in pdfFiles)  
    {  
        AddPdfToPanel(file);  
    }  
  
    // Update placeholder visibility based on whether there are PDF files and the view mode  
    placeholderTextBlock.Visibility = showPdfImages && pdfFiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;  
}  


        private void AddPdfToPanel(string file)
        {
            if (showPdfImages)
            {
                var images = GetPdfPageImages(file, zoomLevel);
                for (int pageIndex = 0; pageIndex < images.Count; pageIndex++)
                {
                    var image = images[pageIndex];
                    Border border = new Border
                    {
                        Margin = new Thickness(5),
                        BorderThickness = new Thickness(2),
                        BorderBrush = System.Windows.Media.Brushes.Transparent,
                        Tag = new Tuple<string, int>(file, pageIndex + 1)
                    };
                    var imageControl = new System.Windows.Controls.Image
                    {
                        Source = image,
                        Width = 100 * zoomLevel,
                        Height = 100 * zoomLevel
                    };
                    border.Child = imageControl;

                    // Add event handlers for highlighting and unhighlighting  
                    border.MouseLeftButtonDown += PdfPreview_MouseLeftButtonDown;
                    border.MouseRightButtonDown += PdfPreview_MouseRightButtonDown;

                    pdfPanel.Children.Add(border);
                }
            }
            else
            {
                Border border = new Border
                {
                    Margin = new Thickness(5),
                    BorderThickness = new Thickness(2),
                    BorderBrush = System.Windows.Media.Brushes.Transparent,
                    Tag = new Tuple<string, int>(file, 1) // Default to page 1 for non-image view  
                };
                var textBlock = new TextBlock { Text = file, Margin = new Thickness(5) };
                border.Child = textBlock;

                // Add event handlers for highlighting and unhighlighting  
                border.MouseLeftButtonDown += PdfPreview_MouseLeftButtonDown;
                border.MouseRightButtonDown += PdfPreview_MouseRightButtonDown;

                pdfPanel.Children.Add(border);
            }
        }

        private void PdfPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Border border = sender as Border;
            if (border != null && border.Tag is Tuple<string, int> tag)
            {
                string filePath = tag.Item1;
                int pageNumber = tag.Item2;

                if (!highlightedPages.ContainsKey(filePath))
                {
                    highlightedPages[filePath] = new List<int>();
                }

                if (!highlightedPages[filePath].Contains(pageNumber))
                {
                    highlightedPages[filePath].Add(pageNumber);
                    border.BorderBrush = System.Windows.Media.Brushes.Blue;
                    Debug.WriteLine($"Highlighted: {filePath}, Page: {pageNumber}");
                }
            }
        }


        private void PdfPreview_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Border border = sender as Border;
            if (border != null && border.Tag is Tuple<string, int> tag)
            {
                string filePath = tag.Item1;
                int pageNumber = tag.Item2;

                if (highlightedPages.ContainsKey(filePath) && highlightedPages[filePath].Contains(pageNumber))
                {
                    highlightedPages[filePath].Remove(pageNumber);
                    if (highlightedPages[filePath].Count == 0)
                    {
                        highlightedPages.Remove(filePath);
                    }
                    border.BorderBrush = System.Windows.Media.Brushes.Transparent;
                    Debug.WriteLine($"Unhighlighted: {filePath}, Page: {pageNumber}");
                }
            }
        }


        private List<BitmapImage> GetPdfPageImages(string filePath, double zoomLevel)
        {
            var images = new List<BitmapImage>();
            try
            {
                using (var document = PdfiumViewer.PdfDocument.Load(filePath))
                {
                    int pageCount = document.PageCount;
                    int renderWidth = (int)(100 * zoomLevel);
                    int renderHeight = (int)(100 * zoomLevel);

                    for (int i = 0; i < pageCount; i++)
                    {
                        using (var image = document.Render(i, renderWidth, renderHeight, true))
                        {
                            var bitmap = new Bitmap(image);
                            images.Add(ConvertToBitmapImage(bitmap));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("An error occurred while trying to render the PDF: " + ex.Message);
            }
            return images;
        }

        private BitmapImage ConvertToBitmapImage(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }

        private void UpdateImageCount()
        {
            txtImageCount.Text = $"TOTAL IMAGES: {pdfPanel?.Children.Count ?? 0}";
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow dragging the window if the click originated from the window itself  
            if (e.OriginalSource == this)
            {
                this.DragMove();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (e.Key == Key.Add || e.Key == Key.OemPlus)
                {
                    zoomLevel += 0.1;
                }
                else if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
                {
                    zoomLevel -= 0.1;
                }

                zoomLevel = Math.Max(0.1, Math.Min(zoomLevel, 3.0)); // Clamp zoom level between 0.1 and 3.0  
                UpdatePanel();
            }
        }
    }
}
