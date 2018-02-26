using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using SimpleImageEditing;
using System.Collections.ObjectModel;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Kernel_Convolutions
{

    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private bool IsGreyScale(SoftwareBitmap bitmap)
        {
            using (SoftwareBitmapEditor editor = new SoftwareBitmapEditor(bitmap))
            {
                for (uint x = 0; x < editor.width; x++) {
                    for (uint y = 0; y < editor.height; y++) {
                        var p = editor.getPixel(x, y);
                        if (p.r != p.g || p.r != p.b || p.g != p.b) {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private async void LoadImage()
        {
            try
            {
                using (var stream = await ImageData.OriginalFile.OpenAsync(FileAccessMode.Read))
                {
                    // this is a PNG file so we need to decode it to raw pixel data.
                    var bitmapDecoder = await BitmapDecoder.CreateAsync(stream);

                    // grab the pixels in a byte[] array.
                    var pixelProvider = await bitmapDecoder.GetPixelDataAsync();
                    var bits = pixelProvider.DetachPixelData();

                    // make a software bitmap to decode it into
                    ImageData.OriginalBitmap = new SoftwareBitmap(
                      BitmapPixelFormat.Bgra8,
                      (int)bitmapDecoder.PixelWidth,
                      (int)bitmapDecoder.PixelHeight,
                      BitmapAlphaMode.Premultiplied);

                    // copy the pixels.
                    ImageData.OriginalBitmap.CopyFromBuffer(bits.AsBuffer());

                    // we now need something to glue this into a XAML Image object via
                    // something derived from ImageSource.
                    var originalBitmapSource = new SoftwareBitmapSource();
                    await originalBitmapSource.SetBitmapAsync(ImageData.OriginalBitmap);

                    OriginalImage.Source = originalBitmapSource;
                }

                ImageData.OriginalPixelIncrement = 1;
                ImageData.OriginalGreyscale = IsGreyScale(ImageData.OriginalBitmap);
                ImageData.ResultGreyscale = ImageData.OriginalGreyscale;
                ImageData.NewBitmap = null;

                NewImage.Source = null;

                UpdateButton.IsEnabled = false;
                SaveButton.IsEnabled = false;
                ResetButton.IsEnabled = false;
            }
            catch { }
        }

        private async void LoadImageButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker fileOpenPicker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };
            fileOpenPicker.FileTypeFilter.Add(".png");
            fileOpenPicker.FileTypeFilter.Add(".jpg");
            fileOpenPicker.ViewMode = PickerViewMode.Thumbnail;

            // Open a well-known image file created by Visual Studio template.
            ImageData.OriginalFile = await fileOpenPicker.PickSingleFileAsync();

            if (ImageData.OriginalFile == null)
                return;

            LoadImage();

            PixelateButton.IsEnabled = true;
            PixelationSlider.IsEnabled = true;
            GreyscaleButton.IsEnabled = true;
            GaussianButton.IsEnabled = true;
            MeanButton.IsEnabled = true;
            SobelButton.IsEnabled = true;
            AnimationToggleSwitch.IsEnabled = true;
            AngleIdentificationSwitch.IsEnabled = true;
            HueShiftButton.IsEnabled = true;
            HueShiftSlider.IsEnabled = true;
        }

        private async void SetImageOutput()
        {
            var originalBitmapSource = new SoftwareBitmapSource();
            await originalBitmapSource.SetBitmapAsync(ImageData.NewBitmap);

            NewImage.Source = originalBitmapSource;
            UpdateButton.IsEnabled = true;
            SaveButton.IsEnabled = true;
            ResetButton.IsEnabled = true;
        }

        public async Task SetPreviewImage()
        {
            var originalBitmapSource = new SoftwareBitmapSource();
            await originalBitmapSource.SetBitmapAsync(ImageData.PreviewBitmap);

            NewImage.Source = originalBitmapSource;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            LoadImage();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ImageData.OriginalFile == null)
                return;

            FileSavePicker fileSavePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                SuggestedFileName = ImageData.OriginalFile.DisplayName + "(edited)"
            };
            fileSavePicker.FileTypeChoices.Add("PNG", new List<string> { ".png" });
            fileSavePicker.FileTypeChoices.Add("JPEG", new List<string> { ".jpg" });

            var saveFile = await fileSavePicker.PickSaveFileAsync();

            if (saveFile == null)
                return;

            // Open the stream for read.
            using (var stream = await saveFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                // this is a PNG file so we need to decode it to raw pixel data.
                Guid encoderId = saveFile.FileType == ".jpg" ? BitmapEncoder.JpegEncoderId : BitmapEncoder.PngEncoderId;
                var bitmapEncoder = await BitmapEncoder.CreateAsync(encoderId, stream);

                bitmapEncoder.SetSoftwareBitmap(ImageData.NewBitmap);
                await bitmapEncoder.FlushAsync();
            }

        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            ImageData.OriginalPixelIncrement = ImageData.PixelIncrement;
            ImageData.OriginalGreyscale = ImageData.ResultGreyscale;
            ImageData.OriginalBitmap = ImageData.NewBitmap;

            var originalBitmapSource = new SoftwareBitmapSource();
            await originalBitmapSource.SetBitmapAsync(ImageData.OriginalBitmap);

            OriginalImage.Source = originalBitmapSource;
        }

        private void PixelateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ImageData.PixelIncrement = ImageData.OriginalPixelIncrement * (uint)PixelationSlider.Value;
                using (SoftwareBitmapEditor editor = new SoftwareBitmapEditor(ImageData.OriginalBitmap))
                {
                    ImageData.NewBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, editor.width - (int)(editor.width % ImageData.PixelIncrement), editor.height - (int)(editor.height % ImageData.PixelIncrement), BitmapAlphaMode.Ignore);
                    using (SoftwareBitmapEditor newEditor = new SoftwareBitmapEditor(ImageData.NewBitmap))
                    {
                        SoftwareBitmapPixel pixel;
                        for (uint row = 0; row < editor.height; row += ImageData.PixelIncrement)
                        {
                            for (uint column = 0; column < editor.width; column += ImageData.PixelIncrement)
                            {
                                int pixelCount = (int)Math.Pow(ImageData.PixelIncrement, 2);
                                int totalRed = 0;
                                int totalGreen = 0;
                                int totalBlue = 0;

                                // Fetch the color values for area in original image
                                for (uint x = 0; x < ImageData.PixelIncrement; x++)
                                {
                                    for (uint y = 0; y < ImageData.PixelIncrement; y++)
                                    {
                                        if (column + x < editor.width && row + y < editor.height)
                                        {
                                            pixel = editor.getPixel(column + x, row + y);
                                            totalRed += pixel.r;
                                            totalGreen += pixel.b;
                                            totalBlue += pixel.g;
                                            // G and B in wrong order due to API inaccuracy
                                        }
                                    }
                                }

                                // Calculate mean color values
                                byte r = (byte)(totalRed / pixelCount);
                                byte g = (byte)(totalGreen / pixelCount);
                                byte b = (byte)(totalBlue / pixelCount);

                                // Set color for same area in new image
                                for (uint newX = 0; newX < ImageData.PixelIncrement; newX++)
                                {
                                    for (uint newY = 0; newY < ImageData.PixelIncrement; newY++)
                                    {
                                        if (column + newX < newEditor.width && row + newY < newEditor.height)
                                            newEditor.setPixel(column + newX, row + newY, r, g, b);
                                    }
                                }
                            }
                        }
                    }
                }
                SetImageOutput();
            }
            catch { }
        }

        public static SoftwareBitmap GreyscaleImage(SoftwareBitmap sourceBitmap)
        {
            ImageData.PixelIncrement = ImageData.OriginalPixelIncrement;
            using (SoftwareBitmapEditor editor = new SoftwareBitmapEditor(sourceBitmap))
            {
                SoftwareBitmap resultBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, editor.width - (int)(editor.width % ImageData.PixelIncrement), editor.height - (int)(editor.height % ImageData.PixelIncrement), BitmapAlphaMode.Ignore);
                using (SoftwareBitmapEditor newEditor = new SoftwareBitmapEditor(resultBitmap))
                {
                    try
                    {
                        SoftwareBitmapPixel pixel;

                        for (uint row = 0; row < editor.height; row += ImageData.PixelIncrement)
                        {
                            for (uint column = 0; column < editor.width; column += ImageData.PixelIncrement)
                            {
                                int pixelCount = (int)Math.Pow(ImageData.PixelIncrement, 2);
                                int totalRed = 0;
                                int totalGreen = 0;
                                int totalBlue = 0;

                                // Fetch the color values for area in original image
                                for (uint x = 0; x < ImageData.PixelIncrement; x++)
                                {
                                    for (uint y = 0; y < ImageData.PixelIncrement; y++)
                                    {
                                        if (column + x < editor.width && row + y < editor.height)
                                        {
                                            pixel = editor.getPixel(column + x, row + y);
                                            totalRed += pixel.r;
                                            totalGreen += pixel.b;
                                            totalBlue += pixel.g;
                                            // G and B in wrong order due to API inaccuracy
                                        }
                                    }
                                }

                                // Calculate mean color values
                                byte r = (byte)(totalRed / pixelCount);
                                byte g = (byte)(totalGreen / pixelCount);
                                byte b = (byte)(totalBlue / pixelCount);

                                // Calculte mean overall color
                                byte grey = (byte)((r + g + b) / 3);

                                // Set color for same area in new image
                                for (uint newX = 0; newX < ImageData.PixelIncrement; newX++)
                                {
                                    for (uint newY = 0; newY < ImageData.PixelIncrement; newY++)
                                    {
                                        if (column + newX < newEditor.width && row + newY < newEditor.height)
                                            newEditor.setPixel(column + newX, row + newY, grey, grey, grey);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
                ImageData.ResultGreyscale = true;
                return resultBitmap;
            }
        }

        public static SoftwareBitmap ShiftHue(SoftwareBitmap sourceBitmap, double degrees)
        {
            ImageData.PixelIncrement = ImageData.OriginalPixelIncrement;
            using (SoftwareBitmapEditor editor = new SoftwareBitmapEditor(sourceBitmap))
            {
                SoftwareBitmap resultBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, editor.width - (int)(editor.width % ImageData.PixelIncrement), editor.height - (int)(editor.height % ImageData.PixelIncrement), BitmapAlphaMode.Ignore);
                using (SoftwareBitmapEditor resultEditor = new SoftwareBitmapEditor(resultBitmap))
                {
                    try
                    {
                        SoftwareBitmapPixel originalPixel;

                        for (uint x = 0; x < editor.width; x++)
                        {
                            for (uint y = 0; y < editor.height; y++)
                            {
                                originalPixel = editor.getPixel(x, y);
                                ConversionColor color = new ConversionColor();
                                color.SetRGB(originalPixel.r, originalPixel.b, originalPixel.g);
                                color.AddHue(degrees);
                                while (color.H > 360)
                                    color.AddHue(-360);

                                resultEditor.setPixel(x, y, color.R, color.G, color.B);
                            }
                        }
                    }
                    catch { }
                }
                return resultBitmap;
            }
        }

        private void GreyscaleButton_Click(object sender, RoutedEventArgs e)
        {
            ImageData.NewBitmap = GreyscaleImage(ImageData.OriginalBitmap);
            SetImageOutput();
        }
        
        public void SetKernelDisplay(int[,] kernel, int kernelSize)
        {
            MovingKernelGrid.RowDefinitions.Clear();
            MovingKernelGrid.ColumnDefinitions.Clear();
            MovingKernelGrid.Children.Clear();

            double imageSizeProportion = OriginalImage.ActualWidth / ImageData.OriginalBitmap.PixelWidth;

            for (int i = 0; i < kernelSize; i++)
            {
                MovingKernelGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(ImageData.OriginalPixelIncrement * imageSizeProportion) });
                for (int j = 0; j < kernelSize; j++)
                {
                    if (i == 0) MovingKernelGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(ImageData.OriginalPixelIncrement * imageSizeProportion) });

                    Rectangle rectangle = new Rectangle() { Stroke = new SolidColorBrush(Windows.UI.Colors.White), StrokeThickness = 1, Margin = new Thickness(0)};
                    Grid.SetRow(rectangle, i);
                    Grid.SetColumn(rectangle, j);
                    MovingKernelGrid.Children.Add(rectangle);

                    TextBlock textBlock = (TextBlock)FindName("Kernel" + i.ToString() + j.ToString());
                    textBlock.Text = kernel[i, j].ToString();
                }
            }

            double margin = imageSizeProportion * (0 - (ImageData.OriginalPixelIncrement + 3));
            MovingKernelHolder.Margin = new Thickness(margin, margin, 0, 0);
            MovingKernelHolder.Visibility = Visibility.Visible;

        }

        public void UpdateMovingKernel(uint x, uint y)
        {
            double imageSizeProportion = OriginalImage.ActualWidth / ImageData.OriginalBitmap.PixelWidth;

            if (ImageData.OriginalPixelIncrement * imageSizeProportion != MovingKernelGrid.RowDefinitions[0].ActualHeight)
            {
                foreach (RowDefinition r in MovingKernelGrid.RowDefinitions) r.Height = new GridLength(ImageData.OriginalPixelIncrement * imageSizeProportion);
                foreach (ColumnDefinition c in MovingKernelGrid.ColumnDefinitions) c.Width = new GridLength(ImageData.OriginalPixelIncrement * imageSizeProportion);
            }

            double marginX = imageSizeProportion * (x - (ImageData.OriginalPixelIncrement + 3));
            double marginY = imageSizeProportion * (y - (ImageData.OriginalPixelIncrement + 3));
            MovingKernelHolder.Margin = new Thickness(marginX, marginY, 0, 0);
        }

        public void ClearKernelDisplay()
        {
            MovingKernelHolder.Visibility = Visibility.Collapsed;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    TextBlock kernelTextBlock = (TextBlock)FindName("Kernel" + i.ToString() + j.ToString());
                    kernelTextBlock.Text = string.Empty;

                    TextBlock contextTextBlock = (TextBlock)FindName("Context" + i.ToString() + j.ToString());
                    contextTextBlock.Text = string.Empty;

                    TextBlock resultTextBlock = (TextBlock)FindName("Result" + i.ToString() + j.ToString());
                    resultTextBlock.Text = string.Empty;
                }
                ResultTotalTextBlock.Text = string.Empty;
                KernelTotalTextBlock.Text = string.Empty;
                NewPixelTextBlock.Text = string.Empty;
            }
        }

        public void SetResultDisplay(int[,] context, int[,] result, int resultTotal, int kernelTotal, int newPixelValue)
        {
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    TextBlock contextTextBlock = (TextBlock)FindName("Context" + i.ToString() + j.ToString());
                    contextTextBlock.Text = context[i, j].ToString();

                    TextBlock resultTextBlock = (TextBlock)FindName("Result" + i.ToString() + j.ToString());
                    resultTextBlock.Text = result[i, j].ToString();
                }
                ResultTotalTextBlock.Text = resultTotal.ToString();
                KernelTotalTextBlock.Text = kernelTotal.ToString();
                NewPixelTextBlock.Text = newPixelValue.ToString();
            }
        }

        private async void GaussianButton_Click(object sender, RoutedEventArgs e)
        {
            Convolution convolution = new Convolution(new int[3, 3] { { 1, 2, 1 }, { 2, 4, 2 }, { 1, 2, 1 } }, this, ImageData.OriginalBitmap);
            ImageData.NewBitmap = await convolution.Run();
            SetImageOutput();
        }

        private async void MeanButton_Click(object sender, RoutedEventArgs e)
        {
            Convolution convolution = new Convolution(new int[3, 3] { { 1, 1, 1 }, { 1, 1, 1 }, { 1, 1, 1 } }, this, ImageData.OriginalBitmap);
            ImageData.NewBitmap = await convolution.Run();
            SetImageOutput();
        }

        private byte ColorPythag(byte colorA, byte colorB)
        {
            return (byte)Math.Sqrt(Math.Pow(colorA, 2) + Math.Pow(colorB, 2));
        }

        private SoftwareBitmapPixel ColorAngle(byte colorA, byte colorB)
        {
            byte grey = ColorPythag(colorA, colorB);
            double angle = (colorA == 0) ? 180 : Math.Atan(colorB / colorA) * 360 / Math.PI;
            ConversionColor color = new ConversionColor();
            color.SetHSV(angle, 1, grey / 255d);
            return new SoftwareBitmapPixel() { r = color.R, g = color.G, b = color.B };
        }

        private SoftwareBitmap BitmapPythag(SoftwareBitmap bitmapA, SoftwareBitmap bitmapB)
        {
            int width = Math.Min(bitmapA.PixelWidth,bitmapB.PixelWidth);
            int height = Math.Min(bitmapA.PixelHeight,bitmapB.PixelHeight);

            SoftwareBitmap resultBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Ignore);
            using (SoftwareBitmapEditor resultEditor = new SoftwareBitmapEditor(resultBitmap), editorA = new SoftwareBitmapEditor(bitmapA), editorB = new SoftwareBitmapEditor(bitmapB))
            {
                for(uint x = 0; x < width; x++)
                {
                    for(uint y = 0; y < height; y++)
                    {
                        SoftwareBitmapPixel pixelA = editorA.getPixel(x, y);
                        SoftwareBitmapPixel pixelB = editorB.getPixel(x, y);

                        if (AngleIdentificationSwitch.IsOn)
                        {
                            SoftwareBitmapPixel newPixel = ColorAngle(pixelA.r, pixelB.r);
                            resultEditor.setPixel(x, y, newPixel.r, newPixel.g, newPixel.b);
                            ImageData.ResultGreyscale = false;
                        }
                        else
                        {
                            byte newRed = ColorPythag(pixelA.r, pixelB.r);
                            byte newGreen = ColorPythag(pixelA.b, pixelB.b);
                            byte newBlue = ColorPythag(pixelA.g, pixelB.g);

                            resultEditor.setPixel(x, y, newRed, newGreen, newBlue);
                        }
                    }
                }
            }
            return resultBitmap;
        }

        private async void SobelButton_Click(object sender, RoutedEventArgs e)
        {
            if (AngleIdentificationSwitch.IsOn && !ImageData.OriginalGreyscale)
            {
                ImageData.OriginalBitmap = GreyscaleImage(ImageData.OriginalBitmap);
                ImageData.OriginalGreyscale = true;
            }

            Convolution gX = new Convolution(new int[3, 3] { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } }, this, ImageData.OriginalBitmap, 1, true);
            SoftwareBitmap gXBitmap = await gX.Run();

            Convolution gY = new Convolution(new int[3, 3] { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } }, this, ImageData.OriginalBitmap, 1, true);
            SoftwareBitmap gYBitmap = await gY.Run();

            SoftwareBitmap result = BitmapPythag(gXBitmap, gYBitmap);
            ImageData.NewBitmap = result;

            SetImageOutput();
        }

        private void AnimationToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            AnimationSpeedSlider.IsEnabled = AnimationToggleSwitch.IsOn;
            ImageData.AnimationsOn = AnimationToggleSwitch.IsOn;
        }

        private void AnimationSpeedSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            ImageData.AnimationMode = (int)e.NewValue;
        }

        private void HueShiftButton_Click(object sender, RoutedEventArgs e)
        {
            ImageData.NewBitmap = ShiftHue(ImageData.OriginalBitmap, HueShiftSlider.Value);
            SetImageOutput();
        }
    }

    public enum Channel
    {
        red,
        green,
        blue,
        grey
    }

    public class Convolution
    {

        private MainPage Page;
        private SoftwareBitmap SourceBitmap;

        public Convolution(int[,] kernel, MainPage page, SoftwareBitmap source, int divisor = 0, bool ignoreEdgePixels = false)
        {
            Kernel = kernel;
            KernelSize = (int)Math.Sqrt(kernel.Length);

            Page = page;
            SourceBitmap = source;

            if(divisor != 0)
            {
                KernelTotal = divisor;
                CalculateDivisor = false;
            }

            if (ignoreEdgePixels)
                padding = ImageData.PixelIncrement;

        }

        public int[,] Kernel { get; private set; }
        public int KernelSize;

        private bool CalculateDivisor = true;
        public int KernelTotal { get; private set; }

        private uint padding = 0;

        private async Task RunKernelAnimation(SoftwareBitmapEditor editor, Channel channel, int[,] context, int[,] result, int resultTotal, int newPixelValue, uint column, uint row)
        {
            Page.UpdateMovingKernel(column,row);
            Page.SetResultDisplay(context, result, resultTotal, KernelTotal, newPixelValue);
            ImageData.PreviewBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, editor.width, editor.height, BitmapAlphaMode.Premultiplied);
            using (SoftwareBitmapEditor previewEditor = new SoftwareBitmapEditor(ImageData.PreviewBitmap))
            {
                for (uint x = 0; x < previewEditor.width; x++)
                {
                    for (uint y = 0; y < previewEditor.height; y++)
                    {
                        SoftwareBitmapPixel pixel = editor.getPixel(x, y);

                        if (channel == Channel.grey)
                            previewEditor.setPixel(x, y, pixel.r, pixel.g, pixel.b);
                        else if (channel == Channel.red)
                            previewEditor.setPixel(x, y, pixel.r, 0, 0);
                        else if (channel == Channel.blue)
                            previewEditor.setPixel(x, y, 0, 0, pixel.b);
                        else
                            previewEditor.setPixel(x, y, 0, pixel.g, 0);
                    }
                }
            }
            await Page.SetPreviewImage();
        }

        private async Task TryAnimateKernel(bool isFast, SoftwareBitmapEditor editor, Channel channel, int[,] context, int[,] result, int resultTotal, int newPixelValue, uint x, uint y)
        {
            if (ImageData.AnimationsOn)
            {
                switch (ImageData.AnimationMode)
                {
                    case 1:
                        if (!isFast)
                        {
                            await RunKernelAnimation(editor, channel, context, result, resultTotal, newPixelValue,x,y);
                            await Task.Delay(500);
                        }
                        break;
                    case 2:
                        if (!isFast)
                        {
                            await RunKernelAnimation(editor, channel, context, result, resultTotal, newPixelValue, x, y);
                            await Task.Delay(100);
                        }
                        break;
                    case 3:
                        if (!isFast)
                        {
                            await RunKernelAnimation(editor, channel, context, result, resultTotal, newPixelValue, x, y);
                            await Task.Delay(10);
                        }
                        break;
                    case 4:
                        if (!isFast)
                            await RunKernelAnimation(editor, channel, context, result, resultTotal, newPixelValue, x, y);
                        break;
                    case 5:
                        if (isFast)
                            await RunKernelAnimation(editor, channel, context, result, resultTotal, newPixelValue, x, y);
                        break;
                }
            }
        }

        public async Task<SoftwareBitmap> Run()
        {
            try
            {
                int[,] context = new int[KernelSize, KernelSize];
                int[,] result = new int[KernelSize, KernelSize];
                int resultTotal = 0;
                int newPixelValue = 0;

                Page.SetKernelDisplay(Kernel, KernelSize);
                ImageData.ResultGreyscale = ImageData.OriginalGreyscale;
                ImageData.PixelIncrement = ImageData.OriginalPixelIncrement;

                SoftwareBitmap resultBitmap;

                using (SoftwareBitmapEditor editor = new SoftwareBitmapEditor(SourceBitmap))
                {
                    resultBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, editor.width - (int)(editor.width % ImageData.PixelIncrement) - (int)(2 * padding), editor.height - (int)(editor.height % ImageData.PixelIncrement) - (int)(2 * padding), BitmapAlphaMode.Ignore);
                    using (SoftwareBitmapEditor newEditor = new SoftwareBitmapEditor(resultBitmap))
                    {
                        if (ImageData.OriginalGreyscale)
                        {
                            for (uint row = padding; row < editor.height - padding; row += ImageData.PixelIncrement)
                            {
                                for (uint column = padding; column < editor.width - padding; column += ImageData.PixelIncrement)
                                {
                                    context = new int[KernelSize, KernelSize];
                                    result = new int[KernelSize, KernelSize];
                                    resultTotal = 0;
                                    newPixelValue = 0;
                                    
                                    if(CalculateDivisor)
                                        KernelTotal = 0;

                                    for (int x = -1; x <= 1; x++)
                                    {
                                        for (int y = -1; y <= 1; y++)
                                        {
                                            var xPixel = column + (x * ImageData.PixelIncrement);
                                            var yPixel = row + (y * ImageData.PixelIncrement);
                                            if (xPixel >= 0 && xPixel < editor.width && yPixel >= 0 && yPixel < editor.height)
                                            {
                                                context[y + 1, x + 1] = editor.getPixel((uint)xPixel, (uint)yPixel).r;
                                                result[y + 1, x + 1] = context[y + 1, x + 1] * Kernel[y + 1, x + 1];
                                                resultTotal += result[y + 1, x + 1];

                                                if (CalculateDivisor)
                                                    KernelTotal += Kernel[y + 1, x + 1];
                                            }
                                        }
                                    }

                                    newPixelValue = Math.Abs(resultTotal / KernelTotal);

                                    // Set color for area in new image
                                    for (uint newX = 0; newX < ImageData.PixelIncrement; newX++)
                                    {
                                        for (uint newY = 0; newY < ImageData.PixelIncrement; newY++)
                                        {
                                            if (column + newX < newEditor.width && row + newY < newEditor.height)
                                                newEditor.setPixel(column + newX - padding, row + newY - padding, (byte)newPixelValue, (byte)newPixelValue, (byte)newPixelValue);
                                        }
                                    }

                                    await TryAnimateKernel(false, newEditor, Channel.grey, context, result, resultTotal, newPixelValue,column,row);
                                }
                                await TryAnimateKernel(true, newEditor, Channel.grey, context, result, resultTotal, newPixelValue, (uint)newEditor.width - ImageData.OriginalPixelIncrement, row);
                            }
                        }
                        else
                        {
                            foreach (Channel channel in new List<Channel> { Channel.red, Channel.green, Channel.blue })
                            {
                                for (uint row = padding; row < editor.height - padding; row += ImageData.PixelIncrement)
                                {
                                    for (uint column = padding; column < editor.width - padding; column += ImageData.PixelIncrement)
                                    {
                                        context = new int[KernelSize, KernelSize];
                                        result = new int[KernelSize, KernelSize];
                                        resultTotal = 0;
                                        newPixelValue = 0;

                                        if(CalculateDivisor)
                                            KernelTotal = 0;

                                        for (int x = -1; x <= 1; x++)
                                        {
                                            for (int y = -1; y <= 1; y++)
                                            {
                                                var xPixel = column + (x * ImageData.PixelIncrement);
                                                var yPixel = row + (y * ImageData.PixelIncrement);
                                                if (xPixel >= 0 && xPixel < editor.width && yPixel >= 0 && yPixel < editor.height)
                                                {
                                                    SoftwareBitmapPixel p = editor.getPixel((uint)xPixel, (uint)yPixel);
                                                    context[y + 1, x + 1] = channel == Channel.red ? p.r : channel == Channel.green ? p.g : p.b;

                                                    result[y + 1, x + 1] = context[y + 1, x + 1] * Kernel[y + 1, x + 1];
                                                    resultTotal += result[y + 1, x + 1];

                                                    if(CalculateDivisor)
                                                        KernelTotal += Kernel[y + 1, x + 1];
                                                }
                                            }
                                        }

                                        newPixelValue = Math.Abs(resultTotal / KernelTotal);

                                        // Set color for area in new image
                                        for (uint newX = 0; newX < ImageData.PixelIncrement; newX++)
                                        {
                                            for (uint newY = 0; newY < ImageData.PixelIncrement; newY++)
                                            {
                                                if (column + newX - padding < newEditor.width && row + newY - padding < newEditor.height)
                                                {
                                                    SoftwareBitmapPixel currentPixel = newEditor.getPixel(column + newX - padding, row + newY - padding);

                                                    if (channel == Channel.red)
                                                        newEditor.setPixel(column + newX - padding, row + newY - padding, (byte)newPixelValue, currentPixel.b, currentPixel.g);
                                                    else if (channel == Channel.blue)
                                                        newEditor.setPixel(column + newX - padding, row + newY - padding, currentPixel.r, (byte)newPixelValue, currentPixel.g);
                                                    else
                                                        newEditor.setPixel(column + newX - padding, row + newY - padding, currentPixel.r, currentPixel.b, (byte)newPixelValue);

                                                }
                                            }
                                        }

                                        await TryAnimateKernel(false, newEditor, channel, context, result, resultTotal, newPixelValue, column, row);
                                    }
                                    await TryAnimateKernel(true, newEditor, channel, context, result, resultTotal, newPixelValue, (uint)newEditor.width - ImageData.OriginalPixelIncrement, row);
                                }
                            }
                        }
                    }
                }
                Page.ClearKernelDisplay();
                return resultBitmap;
            }
            catch
            {
                Page.ClearKernelDisplay();
                return SourceBitmap;
            }
        }
    }

    public class ConversionColor
    {
        public double H { get; private set; }
        public double S { get; private set; }
        public double V { get; private set; }

        public byte R { get; private set; }
        public byte G { get; private set; }
        public byte B { get; private set; }

        public void SetRGB(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;

            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));

            var color = System.Drawing.Color.FromArgb(r,g,b);
            H = color.GetHue();
            S = (max == 0) ? 0 : 1d - (1d * min / max);
            V = max / 255d;
        }

        public void AddHue(double hue)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = (H + hue) / 60 - Math.Floor((H + hue) / 60);

            var v = Convert.ToInt32(V * 255);
            int p = Convert.ToInt32(v * (1 - S));
            int q = Convert.ToInt32(v * (1 - f * S));
            int t = Convert.ToInt32(v * (1 - (1 - f) * S));

            switch (hi)
            {
                case 0:
                    R = (byte)v;
                    G = (byte)t;
                    B = (byte)p;
                    break;
                case 1:
                    R = (byte)q;
                    G = (byte)v;
                    B = (byte)p;
                    break;
                case 2:
                    R = (byte)p;
                    G = (byte)v;
                    B = (byte)t;
                    break;
                case 3:
                    R = (byte)p;
                    G = (byte)q;
                    B = (byte)v;
                    break;
                case 4:
                    R = (byte)t;
                    G = (byte)p;
                    B = (byte)v;
                    break;
                case 5:
                    R = (byte)v;
                    G = (byte)p;
                    B = (byte)q;
                    break;
            }
        }

        public void SetHSV(double h, double s, double v)
        {
            H = 0;
            S = s;
            V = v;
            AddHue(h);
        }

    }

    public static class ImageData
    {
        public static StorageFile OriginalFile;

        public static SoftwareBitmap OriginalBitmap;
        public static SoftwareBitmap NewBitmap;
        public static SoftwareBitmap PreviewBitmap;

        public static uint OriginalPixelIncrement = 1;
        public static uint PixelIncrement = 1;

        public static bool OriginalGreyscale = false;
        public static bool ResultGreyscale = false;

        public static bool AnimationsOn;
        public static int AnimationMode;
    }

}