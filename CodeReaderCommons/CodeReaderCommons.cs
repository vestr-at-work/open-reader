using System.Threading;
using SixLabors.ImageSharp;
using System.Numerics;

namespace CodeReaderCommons {
    public static class Commons {

        private class WelfordSampleInfo {
            public WelfordSampleInfo() {}
            public int count = 0;
            public double sampleMean = 0;
            //M2 from Welford's online algorithm. When divided by (count - 1) the square root equals sampleDeviation.
            //https://en.wikipedia.org/wiki/Algorithms_for_calculating_variance#Welford's_online_algorithm
            public double sampleM2 = 0;
        }

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
            const int windowSize = 25;
            //2D array of the running sum of pixel intensity values
            // WelfordSampleInfo[,] deviationSampleMatrix = new WelfordSampleInfo[image.Width + ((windowSize / 2) * 2), image.Height + ((windowSize / 2) * 2)];
            // //TODO should be a function
            // for (int j = 0; j < image.Height + ((windowSize / 2) * 2); j++) {
            //     for (int i = 0; i < image.Width + ((windowSize / 2) * 2); i++) {
            //         deviationSampleMatrix[i, j] = new WelfordSampleInfo();
            //     }
            // }
        
            //2D array of the running sum of pixel intensity values
            //int[,] intensitySumMatrix = new int[image.Width + ((windowSize / 2) * 2), image.Height + ((windowSize / 2) * 2)];
            //2D array of the running sum of pixel intensity deviation values
            //int[,] deviationSumMatrix = new int[image.Width + ((windowSize / 2) * 2), image.Height + ((windowSize / 2) * 2)];
            //the "+ 2" is there to avoid bound checks when convolving with kernel
            Single[,] thresholdT1Matrix = new Single[image.Width + 2, image.Height + 2];
            //int[,] thresholdT2Matrix = new int[image.Width, image.Height];
            int[,] convolutionKernel = new int[,] {{1, 1, 1}, {1, 2, 1}, {1, 1, 1}};
            const int kernelSize = 3;
            const int kernelValuesSum = 10;
            //Padded image to avoid bound checks
            Image<Rgba32> paddedImage = GetPaddedImage(image, windowSize / 2);
            
            // //First part of the algorithm. Calculate T1 from the paper.
            // paddedImage.ProcessPixelRows(accessor => {
            //     for (int y = windowSize / 2; y < accessor.Height - (windowSize / 2); y++) {
            //         Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

            //         // pixelRow.Length has the same value as accessor.Width,
            //         // but using pixelRow.Length allows the JIT to optimize away bounds checks:
            //         for (int x = windowSize / 2; x < pixelRow.Length - (windowSize / 2); x++) {
            //             // Get a reference to the pixel at position x
            //             ref Rgba32 pixel = ref pixelRow[x];
            //             int pixelValue = (int)pixel.R;

            //             for (int j = y - (windowSize / 2); j <= y + (windowSize / 2); j++) {
            //                 for (int i = x - (windowSize / 2); i <= x + (windowSize / 2); i++) {
            //                     //Console.WriteLine($"{i}, {j},  matrix: {image.Width + (windowSize / 2)}x{image.Height + (windowSize / 2)}");
            //                     //intensitySumMatrix[i,j] += pixelValue;
            //                     var deviationSampleInfo = deviationSampleMatrix[i, j];
            //                     deviationSampleInfo.count++;
            //                     double delta1 = pixelValue - deviationSampleInfo.sampleMean;
            //                     deviationSampleInfo.sampleMean += (delta1 / deviationSampleInfo.count);
            //                     double delta2 = pixelValue - deviationSampleInfo.sampleMean;
            //                     deviationSampleInfo.sampleM2 += (delta1 * delta2);
            //                 }
            //             }
            //         }
            //     }
            // });


            Memory<Rgba32> pixelMemory;
            paddedImage.DangerousTryGetSinglePixelMemory(out pixelMemory);
            var pixelSpan = pixelMemory.Span;
            for (int y = windowSize / 2; y < paddedImage.Height - (windowSize / 2); y++) {
                for (int x = windowSize / 2; x < paddedImage.Width - (windowSize / 2); x++) {
                    //Console.WriteLine($"{row[x][0]}, {row[x][1]}, {row[x][2]}, {row[x][3]}");
                    var pixelValue = pixelSpan[(y * paddedImage.Width) + x].R;
                    double sampleM2 = 0;
                    double sampleMean = 0;
                    int count = 0;
                    for (int j = y - (windowSize / 2); j < y + (windowSize / 2) - 1; j+= 6) {
                        for (int i = x - (windowSize / 2); i <= x + (windowSize / 2); i+= 6) {
                            var currentPixelValue = pixelSpan[(j * paddedImage.Width) + i].R;
                            count++;
                            double delta1 = currentPixelValue - sampleMean;
                            sampleMean += (delta1 / count);
                            double delta2 = currentPixelValue - sampleMean;
                            sampleM2 += (delta1 * delta2);
                        }
                    }

                    double standardDeviation = Math.Sqrt(sampleM2 / (count - 1));
                    const int R = 1250;
                    byte thresholdT1 = (byte)(sampleMean * (1 + (standardDeviation / R)));
                    thresholdT1Matrix[x - (windowSize / 2) + 1, y - (windowSize / 2) + 1] = thresholdT1;
                }
            }

            //  //First part of the algorithm. Calculate T1 from the paper.
            // image.Mutate(c => c.ProcessPixelRowsAsVector4(row => {
            //     for (int x = windowSize / 2; x < row.Length - (windowSize / 2); x++) {
            //         //Console.WriteLine($"{row[x][0]}, {row[x][1]}, {row[x][2]}, {row[x][3]}");
            //         var pixelValue = (byte)(row[x][0] * 255);
            //         double sampleM2 = 0;
            //         double sampleMean = 0;
            //         int count = 0;
            //         for (int i = x - (windowSize / 2); i <= x + (windowSize / 2); i++) {
            //             var currentPixelValue = (byte)(row[i][0] * 255);
            //             count++;
            //             double delta1 = currentPixelValue - sampleMean;
            //             sampleMean += (delta1 / count);
            //             double delta2 = currentPixelValue - sampleMean;
            //             sampleM2 += (delta1 * delta2);
            //         }

            //         double standardDeviation = Math.Sqrt(sampleM2 / (count - 1));
            //         const int R = 1250;
            //         byte thresholdT1 = (byte)(sampleMean * (1 + (standardDeviation / R)));

            //         if (pixelValue > thresholdT1) {
            //             row[x] = new Vector4(1, 1, 1, 1); //white
            //         }
            //         else {
            //             row[x] = new Vector4(0, 0, 0, 1); //black
            //         }
            //     }
            // }));

            // //Calculate the deviation (this and previous step unfortunatelly cannot be done in one iteration)
            // paddedImage.ProcessPixelRows(accessor => {
            //     for (int y = windowSize / 2; y < accessor.Height - (windowSize / 2) - 1; y++) {
            //         Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

            //         // pixelRow.Length has the same value as accessor.Width,
            //         // but using pixelRow.Length allows the JIT to optimize away bounds checks:
            //         for (int x = windowSize / 2; x < pixelRow.Length - (windowSize / 2) - 1; x++) {
            //             // Get a reference to the pixel at position x
            //             ref Rgba32 pixel = ref pixelRow[x];
            //             int pixelValue = (int)pixel.R;

            //             for (int j = y - (windowSize / 2); j < y + (windowSize / 2) - 1; j++) {
            //                 for (int i = x - (windowSize / 2); i < x + (windowSize / 2) - 1; i++) {
            //                     int windowMean = intensitySumMatrix[x - (windowSize / 2), y - (windowSize / 2)] / (windowSize * windowSize);
            //                     int pixelDeviation = pixelValue - windowMean;
            //                     deviationSumMatrix[i,j] += pixelDeviation * pixelDeviation;
            //                 }
            //             }
            //         }
            //     }
            // });

            // //Compute threshold
            // for (int j = 0; j < image.Height; j++) {
            //     for (int i = 0; i < image.Width; i++) {
            //         int windowSizeSquared = windowSize * windowSize;
            //         int windowMean = intensitySumMatrix[i + (windowSize / 2), j + (windowSize / 2)] / windowSizeSquared;
            //         double windowStandardDeviation = Math.Sqrt((double)(deviationSumMatrix[i + (windowSize / 2), j + (windowSize / 2)] / (windowSizeSquared - 1)));
            //         const int R = 1250;
            //         int thresholdT1 = (int)(windowMean * (1 + (windowStandardDeviation / R)));
            //         //Console.WriteLine($"{i}, {j},  matrix: {image.Width}x{image.Height}");
            //         //"+ 1" to compensate for padding
            //         thresholdT1Matrix[i + 1,j + 1] = thresholdT1;
            //     }
            // }

            // //Compute threshold
            // for (int j = 0; j < image.Height; j++) {
            //     for (int i = 0; i < image.Width; i++) {
            //         var welfordInfo = deviationSampleMatrix[i + (windowSize / 2), j + (windowSize / 2)];
            //         double windowMean = welfordInfo.sampleMean;
            //         //Console.WriteLine($"sample: {windowMean}, real: {(double)intensitySumMatrix[i + (windowSize / 2), j + (windowSize / 2)] / (windowSize * windowSize)}");
            //         double windowStandardDeviation = Math.Sqrt(welfordInfo.sampleM2 / (welfordInfo.count - 1));
            //         //double windowStandardDeviationNotSample = Math.Sqrt((double)(deviationSumMatrix[i + (windowSize / 2), j + (windowSize / 2)] / ((windowSize * windowSize) - 1)));
            //         //Console.WriteLine($"sample: {windowStandardDeviation}, real: {windowStandardDeviationNotSample}");
            //         const int R = 1250;
            //         Single thresholdT1 = (Single)(windowMean * (1 + (windowStandardDeviation / R)));
            //         //Console.WriteLine($"{i}, {j},  matrix: {image.Width}x{image.Height}");
            //         //"+ 1" to compensate for padding
            //         thresholdT1Matrix[i + 1,j + 1] = thresholdT1;
            //     }
            // }


            image.ProcessPixelRows(accessor => {
                for (int y = 0; y < accessor.Height; y++) {
                    Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

                    // pixelRow.Length has the same value as accessor.Width,
                    // but using pixelRow.Length allows the JIT to optimize away bounds checks:
                    for (int x = 0; x < pixelRow.Length; x++) {
                        //TODO: make function from convolution
                        Single convolutionSum = 0;
                        for (int j = -(kernelSize / 2); j <= (kernelSize / 2); j++) {
                            for (int i = -(kernelSize / 2); i <= (kernelSize / 2); i++) {
                                convolutionSum += convolutionKernel[(kernelSize / 2) + i, (kernelSize / 2) + j] * thresholdT1Matrix[x + i + 1, y + j + 1];
                            }
                        }
                        Single thresholdT2 = convolutionSum / kernelValuesSum;
                        //Console.WriteLine($"thresholdt1: {thresholdT1Matrix[x + 1, y + 1]}, thresholdt2: {thresholdT2}");
                        // Get a reference to the pixel at position x
                        ref Rgba32 pixel = ref pixelRow[x];
                        int pixelValue = pixel.R;

                        if (pixelValue >= thresholdT2) { //thresholdT2Matrix[x, y]
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


