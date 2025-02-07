using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Windows;
using iText.Kernel.Utils;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PdfiumViewer;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace PDFCompiler
{
    public partial class MainWindow : Window
    {
        private List<string> pdfFiles;
        private bool showPdfImages;

        public MainWindow()
        {
            InitializeComponent();
            pdfFiles = new List<string>();
            showPdfImages = false;
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
                    // Debug statement to check the filenames being added  
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
            UpdateImageCount();
        }

        private void BtnCombinePdf_Click(object sender, RoutedEventArgs e)
        {
            if (pdfFiles.Count < 2)
            {
                MessageBox.Show("Please add at least two PDFs to combine.");
                return;
            }
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                CombinePdfs(pdfFiles, saveFileDialog.FileName);
                MessageBox.Show("PDFs combined successfully.");
            }
        }

        private void CombinePdfs(List<string> pdfFiles, string outputPath)
        {
            // Debug statement to check the output path  
            Debug.WriteLine("Output Path: " + outputPath);

            using (var pdf = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfWriter(outputPath)))
            {
                PdfMerger merger = new PdfMerger(pdf);
                foreach (string file in pdfFiles)
                {
                    // Debug statement to check each file being combined  
                    Debug.WriteLine("Combining PDF: " + file);

                    using (var sourcePdf = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(file)))
                    {
                        merger.Merge(sourcePdf, 1, sourcePdf.GetNumberOfPages());
                    }
                }
            }
        }


        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (pdfFiles.Count < 1)
            {
                MessageBox.Show("Please add a PDF to print.");
                return;
            }

            string tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf");

            // Determine if any of the original files need custom margins and paper size  
            bool is2x2Label = pdfFiles.Any(file =>
            {
                string fileName = Path.GetFileName(file).ToUpper();
                return fileName.StartsWith("GIL") || fileName.StartsWith("BEL") || fileName.StartsWith("HAN") || fileName.StartsWith("LAT");
            });

            CombinePdfs(pdfFiles, tempFilePath);

            // Pass the information about whether it's a 2x2 label to the PrintPdf method  
            PrintPdf(tempFilePath, is2x2Label);
        }


        private void PrintPdf(string filePath, bool is2x2Label)
        {
            try
            {
                Debug.WriteLine("PrintPdf filePath: " + filePath);

                using (var document = PdfiumViewer.PdfDocument.Load(filePath))
                {
                    PrintDialog printDialog = new PrintDialog();
                    if (printDialog.ShowDialog() == true)
                    {
                        PrintDocument printDocument = document.CreatePrintDocument();
                        printDocument.PrinterSettings.PrinterName = printDialog.PrintQueue.FullName;

                        if (is2x2Label)
                        {
                            Debug.WriteLine("Matched 2x2 label condition: " + filePath);

                            // Force custom margins and paper size for 2x2 labels  
                            printDocument.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0); // Adjust margins as needed  
                            printDocument.DefaultPageSettings.PaperSize = new PaperSize("Custom", 200, 200); // 2x2 inches in hundredths of an inch  

                            // Event handler to customize the print page  
                            printDocument.PrintPage += (sender, e) =>
                            {
                                e.PageSettings.Margins = new Margins(0, 0, 0, 0);
                                e.PageSettings.PaperSize = new PaperSize("Custom", 200, 200);
                            };
                        }
                        else
                        {
                            Debug.WriteLine("Did not match 2x2 label condition: " + filePath);
                        }

                        printDocument.Print();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred while trying to print the PDF: " + ex.Message);
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
        }

        private void UpdatePanel()
        {
            pdfPanel.Children.Clear();
            foreach (string file in pdfFiles)
            {
                AddPdfToPanel(file);
            }
        }

        private void AddPdfToPanel(string file)
        {
            if (showPdfImages)
            {
                var firstPageImage = GetFirstPageImage(file);
                if (firstPageImage != null)
                {
                    var imageControl = new System.Windows.Controls.Image { Source = firstPageImage, Width = 100, Height = 100, Margin = new Thickness(5) };
                    pdfPanel.Children.Add(imageControl);
                }
            }
            else
            {
                var textBlock = new TextBlock { Text = file, Margin = new Thickness(5) };
                pdfPanel.Children.Add(textBlock);
            }
        }

        private BitmapImage GetFirstPageImage(string filePath)
        {
            try
            {
                using (var document = PdfiumViewer.PdfDocument.Load(filePath))
                {
                    using (var image = document.Render(0, 100, 100, true))
                    {
                        var bitmap = new Bitmap(image);
                        return ConvertToBitmapImage(bitmap);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred while trying to render the PDF: " + ex.Message);
                return null;
            }
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
            txtImageCount.Text = $"TOTAL IMAGES: {pdfPanel.Children.Count}";
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
    }
}
