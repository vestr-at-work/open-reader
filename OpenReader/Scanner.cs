using SixLabors.ImageSharp.Processing.Processors.Binarization;
using CodeReaderCommons;
using System.Diagnostics;

namespace CodeReader {

    public enum ContentType {
        Text, 
        Binary,
        Action
    }

    public class ScanResult {
        public bool Success { get; init; }
        public ContentType? DataType { get; init; }
        public object? Data { get; init; }
    }

    public class RawQRData {
        public int EstimatedVersion { get; set; }
        public int Size { get; set; }
        public byte[,]? Data {get; set; }
    }


    public interface I2DCodeScanner {
        public ScanResult Scan<TPixel>(Image<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>;
    }

    /// <summary>
    /// Main class responsible for scanning and decoding QR codes.
    /// </summary>
    public class QRScanner : I2DCodeScanner {
        public QRScanner() {}

        /// <summary>
        /// Main method of this class. Scans the image and decodes the data.
        /// </summary>
        /// <param name="image">Input image</param>
        /// <returns></returns>
        public ScanResult Scan<TPixel>(Image<TPixel> image) where TPixel : unmanaged, IPixel<TPixel> {
            
            if (!ImageProcessor.TryGetRawData(image, out RawQRData codeData)) {
                return new ScanResult() { Success = false };
            }

            if (!QRCodeDecoder.TryGetFormatInfo(codeData, out ContentType dataType)) {
                return new ScanResult() { Success = false };
            }

            if (!QRCodeDecoder.TryGetData(codeData, out object data)) {
                return new ScanResult() { Success = false };
            }


            return new ScanResult() { Success = true, DataType = dataType, Data = data };
        }

        /// <summary>
        /// Internal class encapsulating methods for decoding 
        /// </summary>
        class QRCodeDecoder {
            public static bool TryGetFormatInfo(RawQRData codeData, out ContentType dataType) {

                // Dummy implementation
                dataType = ContentType.Text;
                return true;
            }

            public static bool TryGetData(RawQRData codeData, out object decodedData) {
                

                // Dummy implementation
                decodedData = (object)"Hello World";
                return true;
            }
        }


        /// <summary>
        /// Class responsible for processing the input image.
        /// Primarily converts image data to raw 2D matrix data for better handeling.
        /// </summary>
        static class ImageProcessor {

            struct PixelCoord {
                public int XCoord;
                public int YCoord;
            }

            struct QRFinderPatterns {
                public PixelCoord TopLeftPatternCenter;
                public PixelCoord TopRightPatternCenter;
                public PixelCoord BottomLeftPatternCenter;
            }
            
            public static bool TryGetRawData<TPixel>(Image<TPixel> image, out RawQRData? rawDataMatrix) 
                where TPixel : unmanaged, IPixel<TPixel> {

                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    
                    int newWidth = 0, newHeight = 0;
                    if (image.Width > 300 && image.Width > image.Height) {
                        newWidth = 300;
                    }
                    else if (image.Height > 300 && image.Height > 1.7 * image.Width) {
                        newHeight = 450;
                    }
                    else if (image.Height > 300 && image.Height > image.Width) {
                        newHeight = 300;
                    }
                    
                    //when one side is equal to 0 the side gets scaled to preserve the ratio of original image
                    image.Mutate(x => x.Resize(newWidth, newHeight));
                    image.Mutate(x => x.Grayscale());

                    var binarizedImage = Commons.Binarize(image);

                    if (!QRPatternFinder.TryGetFinderPatterns(binarizedImage, out QRFinderPatterns finderPatterns)) {
                        rawDataMatrix = null;
                        return false;
                    }



                    sw.Stop();
                    Console.WriteLine($"time: {sw.Elapsed}");

                    binarizedImage.Save("../TestData/QRCodeTestOUTPUT.png");
                    binarizedImage.Dispose();
                    
                    //image.Save("../TestData/QRCodeTest1OUTPUT.png");
                }
                
                // Dummy implementation
                rawDataMatrix = new RawQRData();
                return true;
            }
            

            /// <summary>
            /// Class for finding and recognizing QR code patterns.
            /// </summary>
            static class QRPatternFinder {
                

                public static bool TryGetFinderPatterns(Image<L8> image, out QRFinderPatterns patterns) {
                    List<PixelCoord> potentialFinderPatterns = GetPotentialFinderPatternCoords(image);

                    patterns = new QRFinderPatterns();
                    return true;
                }

                public static bool TryGetAlignmentPatterns(Image<L8> image) {
                    return true;
                }

                private struct ColorBlock {
                    int startIndex;
                    int endIndex; 
                }

                private static List<PixelCoord> GetPotentialFinderPatternCoords(Image<L8> image) {
                    Memory<L8> pixelMemory;
                    image.DangerousTryGetSinglePixelMemory(out pixelMemory);
                    var pixelSpan = pixelMemory.Span;

                    for (int y = 0; y < image.Height; y++) {
                        for (int x = 0; x < image.Width; x++) {
                            int index = (y * image.Width) + x;
                            var pixelValue = pixelSpan[index].PackedValue;

                        }
                    }

                    return new List<PixelCoord>();
                }
            }
        }
    }
}