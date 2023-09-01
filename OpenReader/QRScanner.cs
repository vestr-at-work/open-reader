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

    public enum QRErrorCorrectionLevel {
        L,
        M,
        Q,
        H
    }

    public enum QRDataMask {
        Mask0,
        Mask1,
        Mask2,
        Mask3,
        Mask4,
        Mask5,
        Mask6,
        Mask7
    }

    public struct QRFormatInfo {
        public QRErrorCorrectionLevel ErrorCorrectionLevel;
        public QRDataMask DataMask;
    }

    public class ParsedQRCode {
        public ParsedQRCode(int version, int size, byte[,] data) {
            EstimatedVersion = version;
            Size = size;
            Data = data;

        }
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
            if (!QRDecoder.TryGetFormatInfo(QRCode, out QRFormatInfo formatInfo)) {
                return new ScanResult() { Success = false };
            }
            if (!QRDecoder.TryGetData(QRCode, formatInfo, out ScanResult QRCodeResult)) {
                return new ScanResult() { Success = false };
            }

            return QRCodeResult;
        }        
    }
}