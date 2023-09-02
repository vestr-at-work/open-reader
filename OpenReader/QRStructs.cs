
namespace CodeReader {
    public struct QRFormatInfo {
        public QRErrorCorrectionLevel ErrorCorrectionLevel;
        public QRDataMask DataMask;
    }

    public struct QRCodeParsed {
        public QRCodeParsed(int version, int size, byte[,] data) {
            EstimatedVersion = version;
            Size = size;
            Data = data;

        }
        public int EstimatedVersion { get; set; }
        public int Size { get; set; }
        public byte[,] Data {get; set; }
    }
}