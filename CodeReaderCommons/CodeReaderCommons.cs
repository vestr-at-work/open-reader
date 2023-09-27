using System.Threading;
using SixLabors.ImageSharp;
using System.Numerics;

namespace CodeReaderCommons {
    public static class Commons {

        /// <summary>
        /// Binarizes grayscale input image based on a local tresholding algorithm.
        ///  
        /// Implements local tresholding algorithm based on a "modified Savoula's method" 
        /// described in this paper: https://www.researchgate.net/publication/275467941_2D_Barcode_Image_Decoding
        /// </summary>
        /// <param name="image">Grayscale source image.</param>
        /// <param name="windowSize">Size of the sampling window in local tresholding.</param>
        /// <returns>Binarized source image.</returns>
        public static Image<L8> Binarize<TPixel>(Image<TPixel> image, int windowSize) where TPixel : unmanaged, IPixel<TPixel> {
            // Lenght of the step when iterating over sample window (sampling done for perforamance reasons)
            const int sampleStep = 6;    
            int[,] convolutionKernel = new int[,] {{1, 1, 1}, {1, 2, 1}, {1, 1, 1}};
            const int kernelSize = 3;
            const int kernelValuesSum = 10;
            // The "+((kernelSize / 2) * 2)" is there to avoid bound checks when convolving with kernel
            float[,] thresholdT1Matrix = new float[image.Width + ((kernelSize / 2) * 2), image.Height + ((kernelSize / 2) * 2)];
            Image<L8> luminanceImage = image.CloneAs<L8>();
            // Padded image to avoid bound checks
            Image<L8> paddedImage = GetPaddedImage(luminanceImage, windowSize / 2);
            

            // First part of the algorithm. Calculate T1 from the paper.
            // Create two tasks to process the parts of the image
            Task task1 = Task.Factory.StartNew(() => ProcessImagePart(paddedImage, thresholdT1Matrix, windowSize, kernelSize, sampleStep,  windowSize / 2, paddedImage.Height / 2));

            // Could be executed as a second task
            ProcessImagePart(paddedImage, thresholdT1Matrix, windowSize, kernelSize,sampleStep, paddedImage.Height / 2, paddedImage.Height - (windowSize / 2));

            Task.WaitAll(task1);

            // ProcessImagePart(paddedImage, thresholdT1Matrix, windowSize, kernelSize,sampleStep, windowSize / 2, paddedImage.Height - (windowSize / 2));
            paddedImage.Dispose();
            
            // Second part of the algorithm. Calculate thresholdT2 and binarize image.
            Memory<L8> pixelMemory;
            luminanceImage.DangerousTryGetSinglePixelMemory(out pixelMemory);
            var pixelSpan = pixelMemory.Span;

            for (int y = 0; y < luminanceImage.Height; y++) {
                for (int x = 0; x < luminanceImage.Width; x++) {
                    float thresholdT2 = GetThresholdT2(x, y);
                    int index = (y * luminanceImage.Width) + x;
                    var pixelValue = pixelSpan[index].PackedValue;

                    pixelSpan[index].PackedValue = (pixelValue >= thresholdT2) ? (byte)255 : (byte)0;
                }
            }

            return luminanceImage; 



            float GetThresholdT2(int x, int y) {
                float convolutionSum = 0;
                for (int j = -(kernelSize / 2); j <= (kernelSize / 2); j++) {
                    for (int i = -(kernelSize / 2); i <= (kernelSize / 2); i++) {
                        convolutionSum += convolutionKernel[(kernelSize / 2) + i, (kernelSize / 2) + j] * thresholdT1Matrix[x + i + 1, y + j + 1];
                    }
                }
                return convolutionSum / kernelValuesSum;
            }
        }

        public static Image<L8> Binarize<TPixel>(Image<TPixel> image) 
            where TPixel : unmanaged, IPixel<TPixel> {

            return Binarize(image, 25);
        }

        private static void ProcessImagePart(Image<L8> paddedImage, float[,] thresholdT1Matrix, int windowSize, int kernelSize, int sampleStep, int startY, int endY) {
            Memory<L8> paddedPixelMemory;
            paddedImage.DangerousTryGetSinglePixelMemory(out paddedPixelMemory);
            var paddedPixelSpan = paddedPixelMemory.Span;

            for (int y = startY; y < endY; y++) {
                for (int x = windowSize / 2; x < paddedImage.Width - (windowSize / 2); x++) {
                    var pixelValue = paddedPixelSpan[(y * paddedImage.Width) + x].PackedValue;

                    // M2 from Welford's online algorithm. When divided by (count - 1) the square root is equal to Standard deviation.
                    // https://en.wikipedia.org/wiki/Algorithms_for_calculating_variance#Welford's_online_algorithm
                    double sampleM2 = 0;
                    double sampleMean = 0;
                    int count = 0;
                    // Sampling to calculate mean and deviation
                    for (int j = y - (windowSize / 2); j < y + (windowSize / 2) - 1; j+= sampleStep) {
                        for (int i = x - (windowSize / 2); i <= x + (windowSize / 2); i+= sampleStep) {
                            var currentPixelValue = paddedPixelSpan[(j * paddedImage.Width) + i].PackedValue;
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
                    thresholdT1Matrix[x - (windowSize / 2) + (kernelSize / 2), y - (windowSize / 2) + (kernelSize / 2)] = thresholdT1;
                }
            }
        }
        

        /// <summary>
        /// Internal function to pad the image to avoid boundary checks when iterating over image with a window.
        /// </summary>
        /// <param name="sourceImage"></param>
        /// <param name="padding"></param>
        /// <returns></returns>
        private static Image<L8> GetPaddedImage(Image<L8> sourceImage, int padding) {
            Image<L8> paddedImage = new(sourceImage.Width + 2 * padding, sourceImage.Height + 2 * padding);

            sourceImage.ProcessPixelRows(paddedImage, (sourceAccessor, targetAccessor) => {
                for (int i = 0; i < sourceAccessor.Height; i++) {
                    Span<L8> sourceRow = sourceAccessor.GetRowSpan(i);
                    Span<L8> paddedRow = targetAccessor.GetRowSpan(i + padding);

                    sourceRow.CopyTo(paddedRow.Slice(padding));
                }
            });

            return paddedImage;
        }
    }
}


