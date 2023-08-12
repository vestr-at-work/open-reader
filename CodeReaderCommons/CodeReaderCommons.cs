using System.Threading;
using SixLabors.ImageSharp;

namespace CodeReaderCommons {
    public static class Commons {

        /// <summary>
        /// 
        /// 
        /// </summary>
        /// <param name="image">Grayscale source image.</param>
        /// <returns>Binarized source image.</returns>
        public static Image<Rgba32> Binarize(Image<Rgba32> image) {
            
            const int windowSize = 15;
            int[,] thresholdSumMatrix = new int[image.Width + (windowSize / 2) + 1, image.Height + (windowSize / 2) + 1];

            Image<Rgba32> paddedImage = GetPaddedImage(image, windowSize / 2);


            paddedImage.ProcessPixelRows(accessor => {
                for (int y = windowSize; y < accessor.Height - windowSize - 1; y++) {
                    Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

                    // pixelRow.Length has the same value as accessor.Width,
                    // but using pixelRow.Length allows the JIT to optimize away bounds checks:
                    for (int x = windowSize; x < pixelRow.Length - windowSize - 1; x++) {
                        // Get a reference to the pixel at position x
                        ref Rgba32 pixel = ref pixelRow[x];
                        int pixelValue = (int)pixel.R;

                        for (int j = y - windowSize; j < y - 1; j++) {
                            for (int i = x - windowSize; i < x - 1; i++) {
                                //Console.WriteLine($"{i}, {j},  matrix: {image.Width + (windowSize / 2)}x{image.Height + (windowSize / 2)}");
                                thresholdSumMatrix[i,j] += pixelValue;
                            }
                        }
                    }
                }
            });

            image.ProcessPixelRows(accessor => {
                for (int y = 0; y < accessor.Height; y++) {
                    Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

                    // pixelRow.Length has the same value as accessor.Width,
                    // but using pixelRow.Length allows the JIT to optimize away bounds checks:
                    for (int x = 0; x < pixelRow.Length; x++) {
                        // Get a reference to the pixel at position x
                        ref Rgba32 pixel = ref pixelRow[x];
                        int pixelValue = pixel.R;
                        
                        if (pixelValue > (thresholdSumMatrix[x + (windowSize / 2),y + (windowSize / 2)] / (windowSize * windowSize))) {
                            pixel = Color.White;
                        }
                        else {
                            pixel = Color.Black;
                        }
                    }
                }
            });

            return image;
        }
        

        /// <summary>
        /// Internal function to pad the image to avoid boundary checks when iterating over image with a window.
        /// </summary>
        /// <param name="sourceImage"></param>
        /// <param name="padding"></param>
        /// <returns></returns>
        private static Image<Rgba32> GetPaddedImage(Image<Rgba32> sourceImage, int padding) {
            Image<Rgba32> paddedImage = new(sourceImage.Width + 2*padding, sourceImage.Height + 2*padding);

            sourceImage.ProcessPixelRows(paddedImage, (sourceAccessor, targetAccessor) => {
                for (int i = 0; i < sourceAccessor.Height; i++) {
                    Span<Rgba32> sourceRow = sourceAccessor.GetRowSpan(i);
                    Span<Rgba32> paddedRow = targetAccessor.GetRowSpan(i + padding);

                    sourceRow.CopyTo(paddedRow.Slice(padding));
                }
            });

            return paddedImage;
        }
    }
}


