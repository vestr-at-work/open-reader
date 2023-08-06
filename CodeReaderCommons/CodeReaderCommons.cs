using System.Threading;

namespace CodeReaderCommons;
public class CodeReaderCommons {
    // public void ProcessUsingLockbitsAndUnsafeAndParallel(Bitmap processedBitmap) {
    //     unsafe {
    //         BitmapData bitmapData = processedBitmap.LockBits(new Rectangle(0, 0, processedBitmap.Width, processedBitmap.Height), ImageLockMode.ReadWrite, processedBitmap.PixelFormat);

    //         int bytesPerPixel = Bitmap.GetPixelFormatSize(processedBitmap.PixelFormat) / 8;
    //         int heightInPixels = bitmapData.Height;
    //         int widthInBytes = bitmapData.Width * bytesPerPixel;
    //         byte* PtrFirstPixel = (byte*)bitmapData.Scan0;

    //         Parallel.For(0, heightInPixels, y => {
    //             byte* currentLine = PtrFirstPixel + (y * bitmapData.Stride);
    //             for (int x = 0; x < widthInBytes; x = x + bytesPerPixel)
    //             {
    //                 int oldBlue = currentLine[x];
    //                 int oldGreen = currentLine[x + 1];
    //                 int oldRed = currentLine[x + 2];

    //                 currentLine[x] = (byte)oldBlue;
    //                 currentLine[x + 1] = (byte)oldGreen;
    //                 currentLine[x + 2] = (byte)oldRed;
    //             }
    //         });
    //         processedBitmap.UnlockBits(bitmapData);
    //     }
    // }
}


