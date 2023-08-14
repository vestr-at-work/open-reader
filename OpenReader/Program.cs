using SixLabors.ImageSharp.Processing.Processors.Binarization;
using CodeReaderCommons;
using System.Diagnostics;

namespace QRCodeReader {
    public class Program {
        public static void Main(string[] args) {

            using (Image<Rgba32> image = Image.Load<Rgba32>("../TestData/QRCodeTest1.jpeg")) {
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
                binarizedImage.Save("../TestData/QRCodeTest1OUTPUT.png");
                
                //image.Save("../TestData/QRCodeTest1OUTPUT.png");
            }
        }
    }
}
