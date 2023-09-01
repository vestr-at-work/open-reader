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

    public class ParsedQRCode {
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
            
            if (!QRImageProcessor.TryParseQRCode(image, out ParsedQRCode QRCode)) {
                return new ScanResult() { Success = false };
            }
            if (!QRDecoder.TryGetFormatInfo(QRCode, out ContentType dataType)) {
                return new ScanResult() { Success = false };
            }
            if (!QRDecoder.TryGetData(QRCode, out object data)) {
                return new ScanResult() { Success = false };
            }

            return new ScanResult() { Success = true, DataType = dataType, Data = data };
        }        
    }
}