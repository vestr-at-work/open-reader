using System.Threading;
using SixLabors.ImageSharp;

namespace CodeReaderCommons {
    public static class Commons {
        public static void Binarize(Image<Rgba32> image) {
            image.ProcessPixelRows(accessor => {
                // 
                //Rgba32 transparent = Color.Transparent;

                for (int y = 0; y < accessor.Height; y++) {
                    Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

                    // pixelRow.Length has the same value as accessor.Width,
                    // but using pixelRow.Length allows the JIT to optimize away bounds checks:
                    for (int x = 0; x < pixelRow.Length; x++) {
                        // Get a reference to the pixel at position x
                        ref Rgba32 pixel = ref pixelRow[x];

                        //could be any channel since the image is in grayscale
                        if (pixel.R > 127) {

                            // Color is pixel-agnostic, but it's implicitly convertible to the Rgba32 pixel type
                            // Overwrite the pixel referenced by 'ref Rgba32 pixel':
                            pixel = Color.White;
                        }
                        else {
                            pixel = Color.Black;
                        } 
                    }
                }
            });
        }
    }
}


