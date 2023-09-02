
namespace CodeReader {
    public interface I2DCodeScanner {
        public ScanResult Scan<TPixel>(Image<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>;
    }
}