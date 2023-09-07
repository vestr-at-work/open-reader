
namespace CodeReader {
    public class QRCodeParsed {
        public QRCodeParsed(QRVersion version, int size, byte[,] data) {
            Version = version;
            Size = size;
            Data = data;
        }
        public QRVersion Version { get; init; }
        public int Size { get; init; }
        public byte[,] Data {get; init; }
    }
}