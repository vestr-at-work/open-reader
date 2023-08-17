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
            
            public static bool TryGetRawData<TPixel>(Image<TPixel> image, out RawQRData? rawDataMatrix) 
                where TPixel : unmanaged, IPixel<TPixel> {

                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();

                    if (image.Width > 300 && image.Width > image.Height) {
                        image.Mutate(x => x.Resize(300, 0));
                    }
                    else if (image.Height > 300 && image.Height > image.Width) {
                        image.Mutate(x => x.Resize(0, 300));
                    }
                    
                    image.Mutate(x => x.Grayscale());
                    //image.Mutate(x => x.AdaptiveThreshold());
                    var binarizedImage = Commons.Binarize(image);

                    if (!QRPatternFinder.TryGetFinderPatterns(binarizedImage)) {
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
                private struct ColorBlock {
                    int startIndex;
                    int endIndex; 
                }

                public static bool TryGetFinderPatterns(Image<L8> image) {
                    return true;
                }

                public static bool TryGetAlignmentPatterns(Image<L8> image) {
                    return true;
                }


            }
        }
    }
}