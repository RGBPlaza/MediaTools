// Learn more about F# at http://fsharp.org

open System
open System.IO
open System.Drawing
open System.Drawing.Imaging

let pixels (image:Bitmap) =
    let width = image.Width
    let height = image.Height
    let rect = Rectangle(0,0,width,height)

    // Lock the image for access
    let data = image.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb)

    // Copy the data
    let ptr = data.Scan0
    let stride = data.Stride
    let bytes = stride * data.Height
    let values : byte[] = Array.zeroCreate bytes
    System.Runtime.InteropServices.Marshal.Copy(ptr,values,0,bytes)

    // Unlock the image
    image.UnlockBits(data)
    
    let pixelSize = 3 // <-- calculate this from the PixelFormat

    // Create and return a 3D-array with the copied data
    Array3D.init width height 4 (fun x y i -> if i < 3 then values.[stride * y + x * pixelSize + i] else 255uy)

let greenness r g b = g * Math.Max((g - r),0.0) * Math.Max((g - b), 0.0)

let chromaKey (data: byte[,,]) = 
    let (width, height, pixelSize) = (Array3D.length1 data, Array3D.length2 data, 4)
    let stride = width * pixelSize
    // Calculate a greeness threshold based on the average value across the image
    let threshold = 0.020 * (Array.init (width * height) (fun i -> greenness (float data.[(i % width) / pixelSize, i / stride, 0] / 255.0) (float data.[(i % width) / pixelSize, i / stride, 1] / 255.0) (float data.[(i % width) / pixelSize, i / stride, 2] / 255.0)) |> Array.filter(fun x -> x >= 0.0) |> Array.average)
    //printfn "Threshold %f" threshold
    let pass = data |> Array3D.mapi (fun x y i b -> 
      if i < 3 then b else (
        let colour = Color.FromArgb(int data.[x, y, i - 3], int data.[x, y, i - 2], int data.[x, y, i - 1])
        //let brightness = colour.GetBrightness()
        let (fR, fG, fB) = ((float data.[x, y, i - 3]) / 255.0, (float data.[x, y, i - 2]) / 255.0, (float data.[x, y, i - 1]) / 255.0)
        if greenness fR fG fB > threshold then 0uy else 255uy))
    // apply blackness to all pixels with transparency for shadowns
    pass |> Array3D.mapi (fun x y i b -> if i = 3 || pass.[x, y, 3] = 255uy then b else 0uy)

// To handle transparency, substitute into line 44 where 0uy is;
let getAlphaForBrightness brightness = (if brightness > 0.4f then 0uy else byte (brightness * 255.0f))

let save path (data: byte[,,]) = 
    let (width, height) = (Array3D.length1 data, Array3D.length2 data)
    use outputBmp = new Bitmap(width, height, PixelFormat.Format32bppArgb)
    let locc = outputBmp.LockBits(Rectangle(0,0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
    
    let (loccStride, loccHeight, pixelSize) = (locc.Stride, locc.Height, 4)
    let rawOutputData = Array.init (loccStride * loccHeight) (fun i -> data.[(i % loccStride) / pixelSize,(i / (loccStride)), i % pixelSize])
    System.Runtime.InteropServices.Marshal.Copy(rawOutputData, 0, locc.Scan0, rawOutputData.Length)

    outputBmp.UnlockBits(locc)    
    outputBmp.Save(new FileStream(path, FileMode.Create, FileAccess.Write), ImageFormat.Png)

let prompt text =
    printfn "%s" text
    Console.ReadLine()

[<EntryPoint>]
let main argv =
    let inputPath = if argv.Length > 0 then argv.[0] else prompt "Enter Folder Path:"
    if Directory.Exists(inputPath) then
        let outputDir = Directory.CreateDirectory(inputPath + @"\chroma_results")
        let filePaths = Array.filter (fun (x: string) -> x.ToLower().EndsWith(".jpg") || x.ToLower().EndsWith(".jpeg")) (Directory.GetFiles(inputPath))
        printfn "Found %i Images" filePaths.Length
        filePaths |> Array.iteri (fun i path -> 
            printfn "Processing %i of %i..." (i + 1) filePaths.Length
            use inputStream = new FileStream(path, FileMode.Open, FileAccess.Read)
            use bmp = new Bitmap(inputStream)    
            let outputFilePath = sprintf "%s\\%s.png" outputDir.FullName ((FileInfo path).Name.Split('.').[0])
            bmp |> pixels |> chromaKey |> save outputFilePath)
        printfn "Done!"
        printfn "Output Location: %s" outputDir.FullName
    else printfn "Input Folder Not Found!"
    Console.ReadLine() |> ignore
    0 // return an integer exit code
