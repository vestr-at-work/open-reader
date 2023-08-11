using SixLabors.ImageSharp.Processing.Processors.Binarization;
using CodeReaderCommons;

namespace QRCodeReader {
    public class Program {
        public static void Main(string[] args) {
            // Bitmap test = new Bitmap("../TestData/QRCodeTest1.jpeg");
            // CodeReaderCommons.ProcessUsingLockbitsAndUnsafeAndParallel(test);

            using (Image<Rgba32> image = Image.Load<Rgba32>("../TestData/QRCodeTest1.jpeg")) {

                image.Mutate(x => x.Grayscale());
                Commons.Binarize(image);

                //ImageProcessor<Rgba32> binarizationProcessor = CreatePixelSpecificProcessor<Rgba32>(new Configuration(), test, new Rectangle(0, 0, test.Width, test.Height));
                //binarizationProcessor.Execute();

                image.Save("../TestData/QRCodeTest1OUTPUT.png");
            }
        }
    }
}
