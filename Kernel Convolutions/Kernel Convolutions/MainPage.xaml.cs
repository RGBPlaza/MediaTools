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

                                // Fetch the colour values for area in original image
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

                                // Calculate mean colour values
                                byte r = (byte)(totalRed / pixelCount);
                                byte g = (byte)(totalGreen / pixelCount);
                                byte b = (byte)(totalBlue / pixelCount);

                                // Set colour for same area in new image
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

                                // Fetch the colour values for area in original image
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

                                // Calculate mean colour values
                                byte r = (byte)(totalRed / pixelCount);
                                byte g = (byte)(totalGreen / pixelCount);
                                byte b = (byte)(totalBlue / pixelCount);

                                // Calculte mean overall colour
                                byte grey = (byte)((r + g + b) / 3);

                                // Set colour for same area in new image
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

        private void GreyscaleButton_Click(object sender, RoutedEventArgs e)
        {
            ImageData.NewBitmap = GreyscaleImage(ImageData.OriginalBitmap);
            SetImageOutput();
        }
        
        public void SetKernelDisplay(int[,] kernel)
        {
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    TextBlock textBlock = (TextBlock)FindName("Kernel" + i.ToString() + j.ToString());
                    textBlock.Text = kernel[i, j].ToString();
                }
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

        private byte ColourPythag(byte colourA, byte colourB)
        {
            return (byte)Math.Sqrt(Math.Pow(colourA, 2) + Math.Pow(colourB, 2));
        }

        private SoftwareBitmapPixel ColourAngle(byte colourA, byte colourB)
        {
            byte grey = ColourPythag(colourA, colourB);
            double angle = (colourA == 0) ? 180 : Math.Atan(colourB / colourA) * 360 / Math.PI;
            var newRGB = new ColorMine.ColorSpaces.Hsv(angle, 1, grey / 255d).ToRgb();
            return new SoftwareBitmapPixel() { r = (byte)newRGB.R, g = (byte)newRGB.G, b = (byte)newRGB.B };
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
                            SoftwareBitmapPixel newPixel = ColourAngle(pixelA.r, pixelB.r);
                            resultEditor.setPixel(x, y, newPixel.r, newPixel.g, newPixel.b);
                            ImageData.ResultGreyscale = false;
                        }
                        else
                        {
                            byte newRed = ColourPythag(pixelA.r, pixelB.r);
                            byte newGreen = ColourPythag(pixelA.b, pixelB.b);
                            byte newBlue = ColourPythag(pixelA.g, pixelB.g);
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
        private int KernelSize;

        private bool CalculateDivisor = true;
        public int KernelTotal { get; private set; }

        private uint padding = 0;

        private async Task RunKernelAnimation(SoftwareBitmapEditor editor, Channel channel, int[,] context, int[,] result, int resultTotal, int newPixelValue)
        {
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

        private async Task TryAnimateKernel(bool isFast, SoftwareBitmapEditor editor, Channel channel, int[,] context, int[,] result, int resultTotal, int newPixelValue)
        {
            if (ImageData.AnimationsOn)
            {
                switch (ImageData.AnimationMode)
                {
                    case 1:
                        if (!isFast)
                        {
                            await RunKernelAnimation(editor, channel, context, result, resultTotal, newPixelValue);
                            await Task.Delay(500);
                        }
                        break;
                    case 2:
                        if (!isFast)
                        {
                            await RunKernelAnimation(editor, channel, context, result, resultTotal, newPixelValue);
                            await Task.Delay(100);
                        }
                        break;
                    case 3:
                        if (!isFast)
                        {
                            await RunKernelAnimation(editor, channel, context, result, resultTotal, newPixelValue);
                            await Task.Delay(10);
                        }
                        break;
                    case 4:
                        if (!isFast)
                            await RunKernelAnimation(editor, channel, context, result, resultTotal, newPixelValue);
                        break;
                    case 5:
                        if (isFast)
                            await RunKernelAnimation(editor, channel, context, result, resultTotal, newPixelValue);
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

                Page.SetKernelDisplay(Kernel);
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

                                    // Set colour for area in new image
                                    for (uint newX = 0; newX < ImageData.PixelIncrement; newX++)
                                    {
                                        for (uint newY = 0; newY < ImageData.PixelIncrement; newY++)
                                        {
                                            if (column + newX < newEditor.width && row + newY < newEditor.height)
                                                newEditor.setPixel(column + newX - padding, row + newY - padding, (byte)newPixelValue, (byte)newPixelValue, (byte)newPixelValue);
                                        }
                                    }

                                    await TryAnimateKernel(false, newEditor, Channel.grey, context, result, resultTotal, newPixelValue);
                                }
                                await TryAnimateKernel(true, newEditor, Channel.grey, context, result, resultTotal, newPixelValue);
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

                                        // Set colour for area in new image
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

                                        await TryAnimateKernel(false, newEditor, channel, context, result, resultTotal, newPixelValue);
                                    }
                                    await TryAnimateKernel(true, newEditor, channel, context, result, resultTotal, newPixelValue);
                                }
                            }
                        }
                    }
                }
                return resultBitmap;
            }
            catch {
                return SourceBitmap;
            }
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