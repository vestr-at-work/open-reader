
namespace CodeReader {
    /// <summary>
    /// Internal class encapsulating methods for decoding 
    /// </summary>
    class QRDecoder {
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
}