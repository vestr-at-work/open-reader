using System.Threading;
using SixLabors.ImageSharp;

namespace CodeReaderCommons {
    public static class Commons {

        /// <summary>
        /// Binarizes grayscale input image based on a local tresholding algorithm.
        ///  
        /// Implements local tresholding algorithm based on a "modified Savoula's method" 
        /// described in this paper: https://www.researchgate.net/publication/275467941_2D_Barcode_Image_Decoding
        /// </summary>
        /// <param name="image">Grayscale source image.</param>
        /// <returns>Binarized source image.</returns>
        public static Image<Rgba32> Binarize(Image<Rgba32> image) {
            
            //Possibly should be input parameter 
            const int windowSize = 31;
            //2D array of the running sum of pixel intensity values
            int[,] intensitySumMatrix = new int[image.Width + ((windowSize / 2) * 2), image.Height + ((windowSize / 2) * 2)];
            //2D array of the running sum of pixel intensity deviation values
            int[,] deviationSumMatrix = new int[image.Width + ((windowSize / 2) * 2), image.Height + ((windowSize / 2) * 2)];
            //the "+ 2" is there to avoid bound checks when convolving with kernel
            int[,] thresholdT1Matrix = new int[image.Width + 2, image.Height + 2];
            int[,] thresholdT2Matrix = new int[image.Width, image.Height];
            int[,] convolutionKernel = new int[,] {{1, 1, 1}, {1, 2, 1}, {1, 1, 1}};
            const int kernelSize = 3;
            const int kernelValuesSum = 10;
            //Padded image to avoid bound checks
            Image<Rgba32> paddedImage = GetPaddedImage(image, windowSize / 2);

            //First part of the algorithm. Calculate T1 from the paper.
            paddedImage.ProcessPixelRows(accessor => {
                for (int y = windowSize / 2; y < accessor.Height - (windowSize / 2) - 1; y++) {
                    Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

                    // pixelRow.Length has the same value as accessor.Width,
                    // but using pixelRow.Length allows the JIT to optimize away bounds checks:
                    for (int x = windowSize / 2; x < pixelRow.Length - (windowSize / 2) - 1; x++) {
                        // Get a reference to the pixel at position x
                        ref Rgba32 pixel = ref pixelRow[x];
                        int pixelValue = (int)pixel.R;

                        for (int j = y - (windowSize / 2); j < y + (windowSize / 2) - 1; j++) {
                            for (int i = x - (windowSize / 2); i < x + (windowSize / 2) - 1; i++) {
                                //Console.WriteLine($"{i}, {j},  matrix: {image.Width + (windowSize / 2)}x{image.Height + (windowSize / 2)}");
                                intensitySumMatrix[i,j] += pixelValue;
                            }
                        }
                    }
                }
            });

            //Calculate the deviation (this and previous step unfortunatelly cannot be done in one iteration)
            paddedImage.ProcessPixelRows(accessor => {
                for (int y = windowSize / 2; y < accessor.Height - (windowSize / 2) - 1; y++) {
                    Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

                    // pixelRow.Length has the same value as accessor.Width,
                    // but using pixelRow.Length allows the JIT to optimize away bounds checks:
                    for (int x = windowSize / 2; x < pixelRow.Length - (windowSize / 2) - 1; x++) {
                        // Get a reference to the pixel at position x
                        ref Rgba32 pixel = ref pixelRow[x];
                        int pixelValue = (int)pixel.R;

                        for (int j = y - (windowSize / 2); j < y + (windowSize / 2) - 1; j++) {
                            for (int i = x - (windowSize / 2); i < x + (windowSize / 2) - 1; i++) {
                                int windowMean = intensitySumMatrix[x - (windowSize / 2), y - (windowSize / 2)] / (windowSize * windowSize);
                                int pixelDeviation = pixelValue - windowMean;
                                deviationSumMatrix[i,j] += pixelDeviation * pixelDeviation;
                            }
                        }
                    }
                }
            });

            //Compute threshold
            for (int j = 0; j < image.Height; j++) {
                for (int i = 0; i < image.Width; i++) {
                    int windowSizeSquared = windowSize * windowSize;
                    int windowMean = intensitySumMatrix[i + (windowSize / 2), j + (windowSize / 2)] / windowSizeSquared;
                    double windowStandardDeviation = Math.Sqrt((double)(deviationSumMatrix[i + (windowSize / 2), j + (windowSize / 2)] / (windowSizeSquared - 1)));
                    const int R = 1250;
                    int thresholdT1 = (int)(windowMean * (1 + (windowStandardDeviation / R)));
                    //Console.WriteLine($"{i}, {j},  matrix: {image.Width}x{image.Height}");
                    //"+ 1" to compensate for padding
                    thresholdT1Matrix[i + 1,j + 1] = thresholdT1;
                }
            }

            //Convolve threshold matrix with kernel. Step 2 from the paper.
            for (int j = 1; j < image.Height + 1; j++) {
                for (int i = 1; i < image.Width + 1; i++) {
                    int convolutionSum = 0;
                    for (int l = -(kernelSize / 2); l < (kernelSize / 2) + 1; l++) {
                        for (int k = -(kernelSize / 2); k < (kernelSize / 2) + 1; k++) {
                            convolutionSum += convolutionKernel[(kernelSize / 2) + k, (kernelSize / 2) + l] * thresholdT1Matrix[i + k, j + l];
                        }
                    }

                    thresholdT2Matrix[i - 1, j - 1] = convolutionSum / kernelValuesSum;
                }
            }


            image.ProcessPixelRows(accessor => {
                for (int y = 0; y < accessor.Height; y++) {
                    Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

                    // pixelRow.Length has the same value as accessor.Width,
                    // but using pixelRow.Length allows the JIT to optimize away bounds checks:
                    for (int x = 0; x < pixelRow.Length; x++) {
                        // Get a reference to the pixel at position x
                        ref Rgba32 pixel = ref pixelRow[x];
                        int pixelValue = pixel.R;

                        if (pixelValue > thresholdT2Matrix[x, y]) {
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


