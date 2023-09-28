using System.Numerics;

namespace CodeReader {
    interface IQRInfoExtractor {
        public double GetModuleSize(QRFinderPatternTrio patterns, out double rotationAngle);
        public QRVersion GetVersion(QRFinderPatternTrio patterns, double moduleSize, double rotationAngle);
    }

    /// <summary>
    /// Class responsible for extracting preliminary info about QR code.
    /// Main public methods are 'GetModuleSize' and 'GetVersion'.
    /// </summary>
    class QRInfoExtractor : IQRInfoExtractor {
        /// <summary>
        /// Calculates and returns the size of module (black or white square in the QR code) from Finder patterns and estimated width.
        /// Main method of the QRInfoExtractor class. 
        /// </summary>
        /// <param name="patterns">Finder patterns.</param>
        /// <returns>Size of the module.</returns>
        public double GetModuleSize(QRFinderPatternTrio patterns, out double rotationAngle) {
            const int sizeOfFinderPattern = 7;
            var topLeft = patterns.TopLeftPattern;
            var topRight = patterns.TopRightPattern;
            var bottomLeft = patterns.BottomLeftPattern;
            Vector2 fromTopLeftToTopRight = new Vector2(topRight.Centroid.X - topLeft.Centroid.X, topRight.Centroid.Y - topLeft.Centroid.Y);
            int signSwitch = 1;
            var scale = (((double)topRight.EstimatedWidth / topLeft.EstimatedWidth) + ((double)bottomLeft.EstimatedWidth / topLeft.EstimatedWidth)) / 2;
            double patternSideLength = 0;

            // If top left pattern is to the right of the top right pattern in the image
            if (patterns.TopLeftPattern.Centroid.X > patterns.TopRightPattern.Centroid.X) {
                signSwitch = -1;
            }

            Point<int> oppositeSidePoint = new Point<int>(topLeft.Centroid.X - (signSwitch * (topLeft.EstimatedWidth / 2)), topLeft.Centroid.Y);
            Point<int> adjacentSidePoint = new Point<int>(topLeft.Centroid.X + (signSwitch * (topLeft.EstimatedWidth / 2)), topLeft.Centroid.Y);
            Point<int> topRightReferencePoint = new Point<int>(oppositeSidePoint.X + (int)fromTopLeftToTopRight.X, oppositeSidePoint.Y + (int)fromTopLeftToTopRight.Y);
            var angleAdjacentToOppositeSidePoint = TrigonomertyHelper.GetAdjacentAngle(oppositeSidePoint, adjacentSidePoint, topRightReferencePoint);
            var hypotenuse = topLeft.EstimatedWidth;
            

            // If angle from width not within correct range recalculate with height.
            // This means that top right finder pattern is much higher than top left finder patter.
            if (angleAdjacentToOppositeSidePoint > (Math.PI / 4)) {

                // If top left pattern is to the bottom of the top right pattern in the image
                if (patterns.TopLeftPattern.Centroid.Y > patterns.TopRightPattern.Centroid.Y) {
                    signSwitch = -1;
                }
                else {
                    signSwitch = 1;
                }   

                oppositeSidePoint = new Point<int>(topLeft.Centroid.X, topLeft.Centroid.Y - (signSwitch * (topLeft.EstimatedHeight / 2)));
                adjacentSidePoint = new Point<int>(topLeft.Centroid.X, topLeft.Centroid.Y + (signSwitch * (topLeft.EstimatedHeight / 2)));
                topRightReferencePoint = new Point<int>(oppositeSidePoint.X + (int)fromTopLeftToTopRight.X, oppositeSidePoint.Y + (int)fromTopLeftToTopRight.Y);
                angleAdjacentToOppositeSidePoint = TrigonomertyHelper.GetAdjacentAngle(oppositeSidePoint, adjacentSidePoint, topRightReferencePoint);
                hypotenuse = topLeft.EstimatedHeight;
            }

            patternSideLength = Math.Cos(angleAdjacentToOppositeSidePoint) * hypotenuse;
                
            rotationAngle = angleAdjacentToOppositeSidePoint;
            return (patternSideLength * scale) / sizeOfFinderPattern;
            
        }

        /// <summary>
        /// Estimates the version of the QR code based on the distance of the Finder patterns from each other and module size.
        /// Main method of the QRInfoExtractor class.
        /// </summary>
        /// <param name="patterns">Finder patterns.</param>
        /// <param name="moduleSize">QR code's estimated module size.</param>
        /// <param name="rotationAngle"></param>
        /// <returns>Estimated version of the QR code.</returns>
        public QRVersion GetVersion(QRFinderPatternTrio patterns, double moduleSize, double rotationAngle) {
            var topLeft = patterns.TopLeftPattern;
            var topRight = patterns.TopRightPattern;

            double version = (((topLeft.Centroid.DistanceFrom(topRight.Centroid) / moduleSize) - 10) / 4);

            return new QRVersion() {value = Convert.ToInt32(version)};
        }
    }
}