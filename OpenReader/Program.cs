﻿using SixLabors.ImageSharp.Processing.Processors.Binarization;
using CodeReaderCommons;
using System.Diagnostics;
using CodeReader;

namespace QRCodeReader {
    public class Program {
        public static void Main(string[] args) {
            I2DCodeScanner scanner = new QRScanner();
            ScanResult result;
            using (Image<Rgba32> image = Image.Load<Rgba32>("../TestData/QRCodeTest1.png")) {
                result = scanner.Scan(image);
            }
            

            if (!result.Success) {
                Console.WriteLine("QR code could not be decoded.");
            }

            Console.WriteLine($"result: {result.Data}");
        }
    }
}
