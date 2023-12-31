
namespace CodeReader {
    /// <summary>
    /// Main class responsible for scanning and decoding QR codes.
    /// </summary>
    public class QRScanner : I2DCodeScanner {
        public QRScanner() {}

        /// <summary>
        /// Scans the image and decodes the data.
        /// </summary>
        /// <param name="image">Input image</param>
        public ScanResult Scan<TPixel>(Image<TPixel> image) where TPixel : unmanaged, IPixel<TPixel> {
            IQRPatternFinder patternFinder = new QRPatternFinder();
            IQRImageSampler sampler = new QRImageSampler();
            IQRInfoExtractor infoExtractor = new QRInfoExtractor();
            var symbolExtractor = new QRSymbolExtractor(patternFinder, infoExtractor, sampler);

            if (!symbolExtractor.TryParseQRCode(image, out QRCodeParsed? QRCode)) {
                return new ScanResult() { Success = false, ErrorMessage = "Could not recognize QR code symbol in the image." };
            }

            IQRFormatDecoder formatDecoder = new QRFormatDecoder();
            if (!formatDecoder.TryGetFormatInfo(QRCode!, out QRFormatInfo formatInfo)) {
                return new ScanResult() { Success = false, ErrorMessage = "Could not load format info from the QR code symbol. Possibly too corrupted image." };
            }

            IQRDataDecoder dataDecoder = new QRDataDecoder();
            if (!dataDecoder.TryGetData(QRCode!, formatInfo, out List<DecodedData> decodedData)) {
                return new ScanResult() { Success = false, ErrorMessage = "Could not load QR code data properly. Possibly too corrupted image." };
            }

            return new ScanResult() {Success = true, DecodedData = decodedData};
        }        
    }

    public struct ScanResult {
        public bool Success { get; init; }
        // Should be null if Success is true.
        public string? ErrorMessage { get; init; }
        public IEnumerable<DecodedData>? DecodedData { get; init; }
    }

    public struct DecodedData {
        public ContentType DataType { get; init; }
        public object Data {get; init; }
    }
}