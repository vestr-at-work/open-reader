using System.Diagnostics;
using CodeReader;

namespace QRCodeReader {
    public class Program {
        public static void Main(string[] args) {
            if (args.Length < 1) {
                Console.WriteLine($"Error: Need path to the QR code image as a first position argument.");
                return;
            }

            Image<Rgba32> image;
            try {
                image = Image.Load<Rgba32>(args[0]);
            }
            catch (Exception e) {
                if (e is NotSupportedException or
                    InvalidImageContentException or
                    UnknownImageFormatException or
                    IOException ) {

                    Console.WriteLine($"Error: {e.Message}");
                    return;
                }

                throw;
            }

            I2DCodeScanner scanner = new QRScanner();
            ScanResult result = scanner.Scan(image);
            
            if (!result.Success || result.DecodedData is null) {
                Console.WriteLine($"Error: {result.ErrorMessage}");
                image.Dispose();
                return;
            }

            foreach (var data in result.DecodedData) {
                Console.WriteLine($"Result:\n\"{data.Data}\"");
            }
            image.Dispose();
        }
    }
}
