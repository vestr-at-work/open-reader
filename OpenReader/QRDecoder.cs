
namespace CodeReader {
    /// <summary>
    /// Internal class encapsulating methods for decoding 
    /// </summary>
    class QRDecoder {
        public static bool TryGetFormatInfo(ParsedQRCode codeData, out QRFormatInfo formatInfo) {

            // Dummy implementation
            formatInfo = new QRFormatInfo();
            return true;
        }

        public static bool TryGetData(ParsedQRCode codeData, QRFormatInfo formatInfo, out ScanResult result) {
            

            // Dummy implementation
            result = new ScanResult() {Success = true, DataType = ContentType.Text, Data = (object)"Hello World"};
            return true;
        }
    }
}