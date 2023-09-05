using SixLabors.ImageSharp.Processing.Processors.Binarization;
using CodeReaderCommons;
using System.Diagnostics;

namespace CodeReader {
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
            
            if (!QRImageProcessor.TryParseQRCode(image, out QRCodeParsed? QRCode)) {
                return new ScanResult() { Success = false };
            }
            if (!QRDecoder.TryGetFormatInfo((QRCodeParsed)QRCode!, out QRFormatInfo formatInfo)) {
                return new ScanResult() { Success = false };
            }
            if (!QRDecoder.TryGetData((QRCodeParsed)QRCode!, formatInfo, out ScanResult QRCodeResult)) {
                return new ScanResult() { Success = false };
            }

            return QRCodeResult;
        }        
    }

    public struct ScanResult {
        public bool Success { get; init; }
        public IEnumerable<DecodedData>? DecodedData { get; init; }
    }

    public struct DecodedData {
        public ContentType DataType { get; init; }
        public object Data {get; init; }
    }
}