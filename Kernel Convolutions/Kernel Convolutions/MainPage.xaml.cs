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
        private uint pixelIncrement;

        private async void LoadImage(StorageFile source)
        {
            originalImage = source;
            using (var stream = await source.OpenAsync(FileAccessMode.Read))
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

            LoadImage(originalImage);
            PixelateButton.IsEnabled = true;
            PixelationSlider.IsEnabled = true;

        }

        private void PixelateButton_Click(object sender, RoutedEventArgs e)
        {
            using (SoftwareBitmapEditor editor = new SoftwareBitmapEditor(softwareBitmap))
            {
                newSoftwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, editor.width, editor.height,BitmapAlphaMode.Ignore);
                using (SoftwareBitmapEditor newEditor = new SoftwareBitmapEditor(newSoftwareBitmap))
                {
                    pixelIncrement = originalPixelIncrement * (uint)PixelationSlider.Value;
                    SoftwareBitmapPixel pixel;

                    for (uint row = pixelIncrement/2; row < editor.height + (pixelIncrement / 2); row += pixelIncrement)
                    {
                        for (uint column = pixelIncrement / 2; column < editor.width + (pixelIncrement / 2); column += pixelIncrement)
                        {
                            try
                            {
                                if (column >= editor.width && row >= editor.height)
                                    pixel = newEditor.getPixel(column - pixelIncrement, row - pixelIncrement);
                                else if (column >= editor.width)
                                    pixel = newEditor.getPixel(column - pixelIncrement, row);
                                else if (row >= editor.height)
                                    pixel = newEditor.getPixel(column, row - pixelIncrement);
                                else
                                    pixel = editor.getPixel(column, row);

                                for (int i = (int)pixelIncrement / -2; i < (int)pixelIncrement / 2; i++)
                                {
                                    if (column + i < editor.width)
                                    {
                                        for (int j = (int)pixelIncrement / -2; j < (int)pixelIncrement / 2; j++)
                                        {
                                            if (row + j < editor.height)
                                                newEditor.setPixel(column + (uint)i, row + (uint)j, pixel.r, pixel.b, pixel.g);
                                            else
                                                break;
                                        }
                                    }
                                    else
                                        break;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            SetImageOutput();
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

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            LoadImage(originalImage);
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
            softwareBitmap = newSoftwareBitmap;

            var softwareBitmapSource = new SoftwareBitmapSource();
            await softwareBitmapSource.SetBitmapAsync(softwareBitmap);

            OriginalImage.Source = softwareBitmapSource;
        }
    }
}
