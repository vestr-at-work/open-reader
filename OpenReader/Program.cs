using SixLabors.ImageSharp.Processing.Processors.Binarization;
using CodeReaderCommons;
using System.Diagnostics;
using CodeReader;

namespace QRCodeReader {
    public class Program {
        public static void Main(string[] args) {
            I2DCodeScanner scanner = new QRScanner();

            var result = scanner.Scan("../TestData/QRCodeTest2.png");

            Console.WriteLine(result);
        }
    }
}
