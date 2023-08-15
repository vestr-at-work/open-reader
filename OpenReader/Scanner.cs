using SixLabors.ImageSharp.Processing.Processors.Binarization;
using CodeReaderCommons;
using System.Diagnostics;

namespace CodeReader {

    public class ScanResult {
        public bool Succses;
        public Type? DataType;
        public object? Data;
    }


    public interface I2DCodeScanner {
        //TODO: really should not be just supporting usage through file path....
        public ScanResult Scan(string pathToInputImage);
    }

    /// <summary>
    /// Main class responsible for scanning and decoding QR codes.
    /// </summary>
    public class QRScanner : I2DCodeScanner {
        public QRScanner() {}

        /// <summary>
        /// Main method of this class. Scans the image and decodes the data.
        /// </summary>
        /// <param name="path">Path to the image file</param>
        /// <returns></returns>
        public ScanResult Scan(string path) {
            using (Image<Rgba32> image = Image.Load<Rgba32>(path)) {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                image.Mutate(x => x.Resize(300,0));
                image.Mutate(x => x.Grayscale());
                //image.Mutate(x => x.AdaptiveThreshold());
                var binarizedImage = Commons.Binarize(image);


                // ImageProcessor<Rgba32> binarizationProcessor = CreatePixelSpecificProcessor<Rgba32>(new Configuration(), image, new Rectangle(0, 0, image.Width, image.Height));
                // binarizationProcessor.Execute();
                sw.Stop();
                Console.WriteLine($"time: {sw.Elapsed}");
                binarizedImage.Save("../TestData/QRCodeTestOUTPUT.png");
                
                //image.Save("../TestData/QRCodeTest1OUTPUT.png");
            }

            return new ScanResult();
        }


        /// <summary>
        /// Class responsible for processing the input image.
        /// Primarily converts image data to raw 2D matrix data for better handeling.
        /// </summary>
        class ImageProcessor {
            

            

            /// <summary>
            /// Class for finding and recognizing QR code patterns.
            /// </summary>
            class PatternFinder {
                private struct ColorBlock {
                    int startIndex;
                    int endIndex; 
                }
            }
        }

        
    }
}