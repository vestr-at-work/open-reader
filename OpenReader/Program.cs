
namespace QRCodeReader {
    public class Program {
        public static void Main(string[] args) {
            // Bitmap test = new Bitmap("../TestData/QRCodeTest1.jpeg");
            // CodeReaderCommons.ProcessUsingLockbitsAndUnsafeAndParallel(test);

            using (Image test = Image.Load("../TestData/QRCodeTest1.jpeg")) {

                test.Mutate(x => x.Grayscale());

                test.Save("../TestData/QRCodeTest1OUTPUT.png");
            }
        }
    }
}
