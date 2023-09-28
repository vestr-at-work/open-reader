using CodeReaderCommons;

namespace CodeReader {
    /// <summary>
    /// Class responsible for processing the input image.
    /// Primarily converts image data to raw 2D matrix data for better handeling.
    /// </summary>
    class QRSymbolExtractor {
        private IQRPatternFinder _patternFinder;
        private IQRInfoExtractor _infoExtractor;
        private IQRImageSampler _sampler;

        public QRSymbolExtractor(IQRPatternFinder patternFinder, IQRInfoExtractor infoExtractor, IQRImageSampler sampler) {
            _patternFinder = patternFinder;
            _infoExtractor = infoExtractor;
            _sampler = sampler;
        }

        public bool TryParseQRCode<TPixel>(Image<TPixel> image, out QRCodeParsed? rawDataMatrix) 
            where TPixel : unmanaged, IPixel<TPixel> {
            
            ResizeImage(image);
            
            image.Mutate(x => x.Grayscale());
            var binarizedImage = Commons.Binarize(image);

            if (!_patternFinder.TryGetFinderPatterns(binarizedImage, out QRFinderPatternTrio finderPatterns)) {
                binarizedImage.Save("../DebugImages/QRCodeTestOUTPUT.png");
                binarizedImage.Dispose();

                rawDataMatrix = null;
                return false;
            }

            double moduleSize = _infoExtractor.GetModuleSize(finderPatterns, out double rotationAngle);
            QRVersion version = _infoExtractor.GetVersion(finderPatterns, moduleSize, rotationAngle);

            Point<int>? alignmentPatternCentroid = null;
            if (version.value > 1) {
                int alignmentPatternWindow = image.Width / 6;
                var approxAlignmentPatternCentroid = _patternFinder.GetApproximateAlignmentPatternCentroid(finderPatterns);
                // TODO Add checking if rectangle bounds not outside the image
                Rectangle alignmentPatternNeighborhood = new Rectangle(approxAlignmentPatternCentroid.X - alignmentPatternWindow / 2, approxAlignmentPatternCentroid.Y - alignmentPatternWindow / 2, alignmentPatternWindow, alignmentPatternWindow);

                if (!_patternFinder.TryGetAlignmentPattern(binarizedImage, alignmentPatternNeighborhood, out Point<int> alignmentPatternCentroidNonNullable)) {
                    binarizedImage.Save("../DebugImages/QRCodeTestOUTPUT.png");
                    binarizedImage.Dispose();

                    rawDataMatrix = null;
                    return false;
                }

                alignmentPatternCentroid = alignmentPatternCentroidNonNullable;
            }
            

            byte[,] qrDataMatrix = _sampler.Sample(
                binarizedImage, 
                finderPatterns,
                alignmentPatternCentroid, 
                version
            );

            binarizedImage.Save("../DebugImages/QRCodeTestOUTPUT.png");
            binarizedImage.Dispose();
            
            var size = 17 + (4 * version.value);
            rawDataMatrix = new QRCodeParsed(version, size, qrDataMatrix);
            return true;
        }

        private static void ResizeImage<TPixel>(Image<TPixel> image) 
            where TPixel : unmanaged, IPixel<TPixel> {

            if (image.Width > 300 && image.Width > image.Height) {
                //when one side is equal to 0 the side gets scaled to preserve the ratio of original image
                image.Mutate(x => x.Resize(300, 0));
            }
            else if (image.Height > 300 && image.Height > 1.7 * image.Width) {
                //when one side is equal to 0 the side gets scaled to preserve the ratio of original image
                image.Mutate(x => x.Resize(0, 450));
            }
            else if (image.Height > 300 && image.Height > image.Width) {
                //when one side is equal to 0 the side gets scaled to preserve the ratio of original image
                image.Mutate(x => x.Resize(0, 300));
            }
        }
    }
}