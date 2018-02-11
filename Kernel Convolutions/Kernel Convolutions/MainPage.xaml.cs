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
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private StorageFile originalImage;
        private SoftwareBitmap softwareBitmap;
        private uint originalPixelIncrement = 1;
        private SoftwareBitmap newSoftwareBitmap;
        private SoftwareBitmap previewBitmap;
        private uint pixelIncrement;
        private bool originalGreyscale = false;
        private bool resultGreyscale = false;

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
            using (var stream = await originalImage.OpenAsync(FileAccessMode.Read))
            {
                // this is a PNG file so we need to decode it to raw pixel data.
                var bitmapDecoder = await BitmapDecoder.CreateAsync(stream);

                // grab the pixels in a byte[] array.
                var pixelProvider = await bitmapDecoder.GetPixelDataAsync();
                var bits = pixelProvider.DetachPixelData();

                // make a software bitmap to decode it into
                softwareBitmap = new SoftwareBitmap(
                  BitmapPixelFormat.Bgra8,
                  (int)bitmapDecoder.PixelWidth,
                  (int)bitmapDecoder.PixelHeight,
                  BitmapAlphaMode.Premultiplied);

                // copy the pixels.
                softwareBitmap.CopyFromBuffer(bits.AsBuffer());

                // we now need something to glue this into a XAML Image object via
                // something derived from ImageSource.
                var softwareBitmapSource = new SoftwareBitmapSource();
                await softwareBitmapSource.SetBitmapAsync(softwareBitmap);

                OriginalImage.Source = softwareBitmapSource;
            }

            NewImage.Source = null;
            originalPixelIncrement = 1;
            originalGreyscale = IsGreyScale(softwareBitmap);
            resultGreyscale = originalGreyscale;
            newSoftwareBitmap = null;
            UpdateButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
            ResetButton.IsEnabled = false;
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
            originalImage = await fileOpenPicker.PickSingleFileAsync();

            if (originalImage == null)
                return;

            LoadImage();
            PixelateButton.IsEnabled = true;
            PixelationSlider.IsEnabled = true;
            GreyscaleButton.IsEnabled = true;
            GaussianButton.IsEnabled = true;
            MeanButton.IsEnabled = true;
            SobelButton.IsEnabled = true;
            AnimationSpeedSlider.IsEnabled = true;
        }

        private async void SetImageOutput()
        {
            var softwareBitmapSource = new SoftwareBitmapSource();
            await softwareBitmapSource.SetBitmapAsync(newSoftwareBitmap);

            NewImage.Source = softwareBitmapSource;
            UpdateButton.IsEnabled = true;
            SaveButton.IsEnabled = true;
            ResetButton.IsEnabled = true;
        }

        private async Task SetPreviewImage()
        {
            var softwareBitmapSource = new SoftwareBitmapSource();
            await softwareBitmapSource.SetBitmapAsync(previewBitmap);

            NewImage.Source = softwareBitmapSource;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            LoadImage();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (originalImage == null)
                return;

            FileSavePicker fileSavePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                SuggestedFileName = originalImage.DisplayName + "(edited)"
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

                bitmapEncoder.SetSoftwareBitmap(newSoftwareBitmap);
                await bitmapEncoder.FlushAsync();
            }

        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            originalPixelIncrement = pixelIncrement;
            originalGreyscale = resultGreyscale;
            softwareBitmap = newSoftwareBitmap;

            var softwareBitmapSource = new SoftwareBitmapSource();
            await softwareBitmapSource.SetBitmapAsync(softwareBitmap);

            OriginalImage.Source = softwareBitmapSource;
        }

        private void PixelateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                pixelIncrement = originalPixelIncrement * (uint)PixelationSlider.Value;
                using (SoftwareBitmapEditor editor = new SoftwareBitmapEditor(softwareBitmap))
                {
                    newSoftwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, editor.width - (int)(editor.width % pixelIncrement), editor.height - (int)(editor.height % pixelIncrement), BitmapAlphaMode.Ignore);
                    using (SoftwareBitmapEditor newEditor = new SoftwareBitmapEditor(newSoftwareBitmap))
                    {
                        SoftwareBitmapPixel pixel;
                        for (uint row = 0; row < editor.height; row += pixelIncrement)
                        {
                            for (uint column = 0; column < editor.width; column += pixelIncrement)
                            {
                                int pixelCount = (int)Math.Pow(pixelIncrement, 2);
                                int totalRed = 0;
                                int totalGreen = 0;
                                int totalBlue = 0;

                                // Fetch the colour values for area in original image
                                for (uint x = 0; x < pixelIncrement; x++)
                                {
                                    for (uint y = 0; y < pixelIncrement; y++)
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
                                for (uint newX = 0; newX < pixelIncrement; newX++)
                                {
                                    for (uint newY = 0; newY < pixelIncrement; newY++)
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

        private void GreyscaleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                pixelIncrement = originalPixelIncrement;
                using (SoftwareBitmapEditor editor = new SoftwareBitmapEditor(softwareBitmap))
                {
                    newSoftwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, editor.width - (int)(editor.width % pixelIncrement), editor.height - (int)(editor.height % pixelIncrement), BitmapAlphaMode.Ignore);
                    using (SoftwareBitmapEditor newEditor = new SoftwareBitmapEditor(newSoftwareBitmap))
                    {
                        SoftwareBitmapPixel pixel;

                        for (uint row = 0; row < editor.height; row += pixelIncrement)
                        {
                            for (uint column = 0; column < editor.width; column += pixelIncrement)
                            {
                                int pixelCount = (int)Math.Pow(pixelIncrement, 2);
                                int totalRed = 0;
                                int totalGreen = 0;
                                int totalBlue = 0;

                                // Fetch the colour values for area in original image
                                for (uint x = 0; x < pixelIncrement; x++)
                                {
                                    for (uint y = 0; y < pixelIncrement; y++)
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
                                for (uint newX = 0; newX < pixelIncrement; newX++)
                                {
                                    for (uint newY = 0; newY < pixelIncrement; newY++)
                                    {
                                        if (column + newX < newEditor.width && row + newY < newEditor.height)
                                            newEditor.setPixel(column + newX, row + newY, grey, grey, grey);
                                    }
                                }
                            }
                        }
                    }
                }
                resultGreyscale = true;
                SetImageOutput();
            }
            catch { }
        }

        private int[,] kernel;
        private int[,] context;
        private int[,] result;
        private int resultTotal;
        private int kernelTotal;
        private int NewPixel;

        private void SetKernelDisplay()
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

        private void SetResultDisplay()
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
                NewPixelTextBlock.Text = NewPixel.ToString();
            }
        }

        private async void RunConvolution()
        {
            try
            {
                SetKernelDisplay();
                pixelIncrement = originalPixelIncrement;

                if (originalGreyscale)
                {
                    using (SoftwareBitmapEditor editor = new SoftwareBitmapEditor(softwareBitmap))
                    {
                        newSoftwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, editor.width - (int)(editor.width % pixelIncrement), editor.height - (int)(editor.height % pixelIncrement), BitmapAlphaMode.Ignore);
                        using (SoftwareBitmapEditor newEditor = new SoftwareBitmapEditor(newSoftwareBitmap))
                        {
                            for (uint row = 0; row < editor.height; row += pixelIncrement)
                            {
                                for (uint column = 0; column < editor.width; column += pixelIncrement)
                                {
                                    context = new int[3, 3];
                                    result = new int[3, 3];
                                    kernelTotal = 0;
                                    resultTotal = 0;
                                    NewPixel = 0;
                                    for (int x = -1; x <= 1; x++)
                                    {
                                        for (int y = -1; y <= 1; y++)
                                        {
                                            var xPixel = column + (x * pixelIncrement);
                                            var yPixel = row + (y * pixelIncrement);
                                            if (xPixel >= 0 && xPixel < editor.width && yPixel >= 0 && yPixel < editor.height)
                                            {
                                                context[y + 1, x + 1] = editor.getPixel((uint)xPixel, (uint)yPixel).r;
                                                result[y + 1, x + 1] = context[y + 1, x + 1] * kernel[y + 1, x + 1];
                                                kernelTotal += kernel[y + 1, x + 1];
                                                resultTotal += result[y + 1, x + 1];
                                            }
                                        }
                                    }
                                    NewPixel = resultTotal / kernelTotal;
                                    SetResultDisplay();

                                    // Set colour for area in new image
                                    for (uint newX = 0; newX < pixelIncrement; newX++)
                                    {
                                        for (uint newY = 0; newY < pixelIncrement; newY++)
                                        {
                                            if (column + newX < newEditor.width && row + newY < newEditor.height)
                                                newEditor.setPixel(column + newX, row + newY, (byte)NewPixel, (byte)NewPixel, (byte)NewPixel);
                                        }
                                    }

                                    previewBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, newEditor.width, newEditor.height, BitmapAlphaMode.Premultiplied);
                                    using (SoftwareBitmapEditor previewEditor = new SoftwareBitmapEditor(previewBitmap))
                                    {
                                        for (uint i = 0; i < previewEditor.width; i++)
                                        {
                                            for (uint j = 0; j < previewEditor.height; j++)
                                            {
                                                var pixel = newEditor.getPixel(i, j);
                                                previewEditor.setPixel(i, j, pixel.r, pixel.g, pixel.b);
                                            }
                                        }
                                    }
                                    await SetPreviewImage();

                                    if (AnimationSpeedSlider.Value < 100)
                                        await Task.Delay((int)(1000 / AnimationSpeedSlider.Value));
                                }
                            }
                        }
                    }
                    SetImageOutput();
                }
                else
                {
                    using (SoftwareBitmapEditor editor = new SoftwareBitmapEditor(softwareBitmap))
                    {
                        SoftwareBitmap redBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, editor.width - (int)(editor.width % pixelIncrement), editor.height - (int)(editor.height % pixelIncrement), BitmapAlphaMode.Ignore);
                        using (SoftwareBitmapEditor newEditor = new SoftwareBitmapEditor(redBitmap))
                        {
                            for (uint row = 0; row < editor.height; row += pixelIncrement)
                            {
                                for (uint column = 0; column < editor.width; column += pixelIncrement)
                                {
                                    context = new int[3, 3];
                                    result = new int[3, 3];
                                    kernelTotal = 0;
                                    resultTotal = 0;
                                    NewPixel = 0;
                                    for (int x = -1; x <= 1; x++)
                                    {
                                        for (int y = -1; y <= 1; y++)
                                        {
                                            var xPixel = column + (x * pixelIncrement);
                                            var yPixel = row + (y * pixelIncrement);
                                            if (xPixel >= 0 && xPixel < editor.width && yPixel >= 0 && yPixel < editor.height)
                                            {
                                                context[y + 1, x + 1] = editor.getPixel((uint)xPixel, (uint)yPixel).r;

                                                result[y + 1, x + 1] = context[y + 1, x + 1] * kernel[y + 1, x + 1];
                                                kernelTotal += kernel[y + 1, x + 1];
                                                resultTotal += result[y + 1, x + 1];
                                            }
                                        }
                                    }
                                    NewPixel = resultTotal / kernelTotal;
                                    SetResultDisplay();

                                    // Set colour for area in new image
                                    for (uint newX = 0; newX < pixelIncrement; newX++)
                                    {
                                        for (uint newY = 0; newY < pixelIncrement; newY++)
                                        {
                                            if (column + newX < newEditor.width && row + newY < newEditor.height)
                                                newEditor.setPixel(column + newX, row + newY, (byte)NewPixel, 0, 0);
                                        }
                                    }

                                    previewBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, newEditor.width, newEditor.height, BitmapAlphaMode.Premultiplied);
                                    using (SoftwareBitmapEditor previewEditor = new SoftwareBitmapEditor(previewBitmap))
                                    {
                                        for (uint i = 0; i < previewEditor.width; i++)
                                        {
                                            for (uint j = 0; j < previewEditor.height; j++)
                                            {
                                                var pixel = newEditor.getPixel(i, j);
                                                previewEditor.setPixel(i, j, pixel.r, pixel.g, pixel.b);
                                            }
                                        }
                                    }
                                    await SetPreviewImage();
                                    
                                    if(AnimationSpeedSlider.Value < 100)
                                        await Task.Delay((int)(1000 / AnimationSpeedSlider.Value));
                                }
                            }
                        }

                        SoftwareBitmap greenBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, editor.width - (int)(editor.width % pixelIncrement), editor.height - (int)(editor.height % pixelIncrement), BitmapAlphaMode.Ignore);
                        using (SoftwareBitmapEditor newEditor = new SoftwareBitmapEditor(greenBitmap))
                        {
                            for (uint row = 0; row < editor.height; row += pixelIncrement)
                            {
                                for (uint column = 0; column < editor.width; column += pixelIncrement)
                                {
                                    context = new int[3, 3];
                                    result = new int[3, 3];
                                    kernelTotal = 0;
                                    resultTotal = 0;
                                    NewPixel = 0;
                                    for (int x = -1; x <= 1; x++)
                                    {
                                        for (int y = -1; y <= 1; y++)
                                        {
                                            var xPixel = column + (x * pixelIncrement);
                                            var yPixel = row + (y * pixelIncrement);
                                            if (xPixel >= 0 && xPixel < editor.width && yPixel >= 0 && yPixel < editor.height)
                                            {
                                                context[y + 1, x + 1] = editor.getPixel((uint)xPixel, (uint)yPixel).g;

                                                result[y + 1, x + 1] = context[y + 1, x + 1] * kernel[y + 1, x + 1];
                                                kernelTotal += kernel[y + 1, x + 1];
                                                resultTotal += result[y + 1, x + 1];
                                            }
                                        }
                                    }
                                    NewPixel = resultTotal / kernelTotal;
                                    SetResultDisplay();

                                    // Set colour for area in new image
                                    for (uint newX = 0; newX < pixelIncrement; newX++)
                                    {
                                        for (uint newY = 0; newY < pixelIncrement; newY++)
                                        {
                                            if (column + newX < newEditor.width && row + newY < newEditor.height)
                                                newEditor.setPixel(column + newX, row + newY, (byte)NewPixel, (byte)NewPixel, (byte)NewPixel);
                                        }
                                    }

                                    previewBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, newEditor.width, newEditor.height, BitmapAlphaMode.Premultiplied);
                                    using (SoftwareBitmapEditor previewEditor = new SoftwareBitmapEditor(previewBitmap))
                                    {
                                        for (uint i = 0; i < previewEditor.width; i++)
                                        {
                                            for (uint j = 0; j < previewEditor.height; j++)
                                            {
                                                var pixel = newEditor.getPixel(i, j);
                                                previewEditor.setPixel(i, j, 0, pixel.g,0);
                                            }
                                        }
                                    }
                                    await SetPreviewImage();
                                    
                                    if (AnimationSpeedSlider.Value < 100)
                                        await Task.Delay((int)(1000 / AnimationSpeedSlider.Value));
                                }
                            }
                        }

                        SoftwareBitmap blueBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, editor.width - (int)(editor.width % pixelIncrement), editor.height - (int)(editor.height % pixelIncrement), BitmapAlphaMode.Ignore);
                        using (SoftwareBitmapEditor newEditor = new SoftwareBitmapEditor(blueBitmap))
                        {
                            for (uint row = 0; row < editor.height; row += pixelIncrement)
                            {
                                for (uint column = 0; column < editor.width; column += pixelIncrement)
                                {
                                    context = new int[3, 3];
                                    result = new int[3, 3];
                                    kernelTotal = 0;
                                    resultTotal = 0;
                                    NewPixel = 0;
                                    for (int x = -1; x <= 1; x++)
                                    {
                                        for (int y = -1; y <= 1; y++)
                                        {
                                            var xPixel = column + (x * pixelIncrement);
                                            var yPixel = row + (y * pixelIncrement);
                                            if (xPixel >= 0 && xPixel < editor.width && yPixel >= 0 && yPixel < editor.height)
                                            {
                                                context[y + 1, x + 1] = editor.getPixel((uint)xPixel, (uint)yPixel).b;

                                                result[y + 1, x + 1] = context[y + 1, x + 1] * kernel[y + 1, x + 1];
                                                kernelTotal += kernel[y + 1, x + 1];
                                                resultTotal += result[y + 1, x + 1];
                                            }
                                        }
                                    }
                                    NewPixel = resultTotal / kernelTotal;
                                    SetResultDisplay();

                                    // Set colour for area in new image
                                    for (uint newX = 0; newX < pixelIncrement; newX++)
                                    {
                                        for (uint newY = 0; newY < pixelIncrement; newY++)
                                        {
                                            if (column + newX < newEditor.width && row + newY < newEditor.height)
                                                newEditor.setPixel(column + newX, row + newY, (byte)NewPixel, (byte)NewPixel, (byte)NewPixel);
                                        }
                                    }

                                    previewBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, newEditor.width, newEditor.height, BitmapAlphaMode.Premultiplied);
                                    using (SoftwareBitmapEditor previewEditor = new SoftwareBitmapEditor(previewBitmap))
                                    {
                                        for (uint i = 0; i < previewEditor.width; i++)
                                        {
                                            for (uint j = 0; j < previewEditor.height; j++)
                                            {
                                                var pixel = newEditor.getPixel(i, j);
                                                previewEditor.setPixel(i, j, 0, 0, pixel.b);
                                            }
                                        }
                                    }
                                    await SetPreviewImage();

                                    if (AnimationSpeedSlider.Value < 100)
                                        await Task.Delay((int)(1000 / AnimationSpeedSlider.Value));
                                }
                            }
                        }

                        newSoftwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, editor.width - (int)(editor.width % pixelIncrement), editor.height - (int)(editor.height % pixelIncrement), BitmapAlphaMode.Ignore);
                        using (SoftwareBitmapEditor newEditor = new SoftwareBitmapEditor(newSoftwareBitmap))
                        {
                            SoftwareBitmapEditor rEditor = new SoftwareBitmapEditor(redBitmap);
                            SoftwareBitmapEditor bEditor = new SoftwareBitmapEditor(greenBitmap);
                            SoftwareBitmapEditor gEditor = new SoftwareBitmapEditor(blueBitmap);

                            for (uint i = 0; i < newEditor.width; i++)
                            {
                                for (uint j = 0; j < newEditor.height; j++)
                                {
                                    byte r = rEditor.getPixel(i, j).r;
                                    byte g = gEditor.getPixel(i, j).g;
                                    byte b = bEditor.getPixel(i, j).b;
                                    newEditor.setPixel(i, j, r, g, b);
                                }
                            }

                            rEditor.Dispose();
                            bEditor.Dispose();
                            gEditor.Dispose();
                        }
                        SetImageOutput();

                    }
                }
            }
            catch { }
        }

        private void GaussianButton_Click(object sender, RoutedEventArgs e)
        {
            kernel = new int[3,3] { { 1, 2, 1 }, { 2, 4, 2 }, { 1, 2, 1 } };
            RunConvolution();
        }

        private void MeanButton_Click(object sender, RoutedEventArgs e)
        {
            kernel = new int[3, 3] { { 1, 1, 1 }, { 1, 1, 1 }, { 1, 1, 1 } };
            RunConvolution();
        }

        private void SobelButton_Click(object sender, RoutedEventArgs e)
        {/*
            kernel = new int[3, 3] { { -1, 0, 1 }, { -1, 0, 1 }, { -1, 0, 1 } };
            RunConvolution();*/
        }
    }
}