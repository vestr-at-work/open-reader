
namespace CodeReader {
    public class QRCodeParsed {
        public QRCodeParsed(int version, int size, byte[,] data) {
            Version = version;
            Size = size;
            Data = data;
        }
        public int Version { get; set; }
        public int Size { get; set; }
        public byte[,] Data {get; set; }
    }
}