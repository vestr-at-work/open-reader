using MathNet.Numerics.LinearAlgebra;

namespace CodeReader {
    interface IQRImageSampler {
        public byte[,] Sample(Image<L8> binerizedImage, QRFinderPatternTrio patterns, Point<int>? mainAlignmentPatternCentroid, QRVersion version);
    }

    /// <summary>
    /// Class responsible for sampling the high resolution binerized image 
    /// into 2D array of bytes with the same width as is the number of modules a QR code of the given version.
    /// Main public method is called 'Sample'.
    /// </summary>
    class QRImageSampler : IQRImageSampler {
        /// <summary>
        /// Samples the high resolution binerized image into 2D array of bytes 
        /// with the same width as is the number of modules a QR code of the given version.
        /// Main method of the QRImageSampler class.
        /// </summary>
        /// <param name="binerizedImage">Binerized image of QR code.</param>
        /// <param name="moduleSize">Size of one module of the QR code.</param>
        /// <param name="version">Estimated version of the QR code.</param>
        /// <returns>Sampled QR code image data. Value 0 means black, value 255 means white.</returns>
        public byte[,] Sample(Image<L8> binerizedImage, QRFinderPatternTrio patterns, Point<int>? mainAlignmentPatternCentroid, QRVersion version) {
            int codeSideLength = 17 + (4 * version.value);
            int outputSize = codeSideLength;

            List<Point<double>> pointsInImage = new List<Point<double>>() {
                (Point<double>)patterns.TopLeftPattern.Centroid, 
                (Point<double>)patterns.TopRightPattern.Centroid, 
                (Point<double>)patterns.BottomLeftPattern.Centroid 
            };

            List<Point<double>> pointsInSampledCode = new List<Point<double>>() {
                new Point<double>(3.5, 3.5), 
                new Point<double>(codeSideLength - 3.5, 3.5),
                new Point<double>(3.5, codeSideLength - 3.5)
            };

            Matrix<double> transformationMatrix;
            if (version.value > 1 && mainAlignmentPatternCentroid is not null) {
                pointsInImage.Add((Point<double>)mainAlignmentPatternCentroid);
                pointsInSampledCode.Add(new Point<double>(codeSideLength - 6.5, codeSideLength - 6.5));

                transformationMatrix = GetTransformationMatrixWithFourPoints(pointsInImage, pointsInSampledCode);
            }
            else {
                transformationMatrix = GetTransformationMatrixWithThreePoints(pointsInImage, pointsInSampledCode);
            }
            
            byte[,] resampledImage = new byte[outputSize, outputSize];
            var debugImage = new Image<L8>(codeSideLength, codeSideLength);

            for (int y = 0; y < outputSize; y++) {
                for (int x = 0; x < outputSize; x++) {
                    // Map the output array coordinates to the original image using the perspective transformation
                    var arrayPoint = Matrix<double>.Build.DenseOfArray(new double[,] {
                        { x + 0.5 },
                        { y + 0.5 },
                        { 1 }
                    });

                    var transformedPoint = transformationMatrix * arrayPoint;

                    double originalX = transformedPoint[0, 0] / transformedPoint[2, 0];
                    double originalY = transformedPoint[1, 0] / transformedPoint[2, 0];

                    int pixelX = (int)Math.Round(originalX);
                    int pixelY = (int)Math.Round(originalY);

                    // Check if the pixel coordinates are within the image bounds
                    if (pixelX >= 0 && pixelX < binerizedImage.Width && pixelY >= 0 && pixelY < binerizedImage.Height) {
                        byte pixelValue = binerizedImage[pixelX, pixelY].PackedValue;

                        resampledImage[x, y] = pixelValue;
                        debugImage[x, y] = new L8(pixelValue);
                    }
                }
            }

            debugImage.Save("../DebugImages/QRCodeDebugImageAlignmentPattern.png");
            debugImage.Dispose();

            return resampledImage;
        }

        private static Matrix<double> GetTransformationMatrixWithThreePoints(List<Point<double>> sourcePoints, List<Point<double>> targetPoints) {
            double xPoint1Source = sourcePoints[0].X;
            double yPoint1Source = sourcePoints[0].Y;
            double xPoint2Source = sourcePoints[1].X;
            double yPoint2Source = sourcePoints[1].Y;
            double xPoint3Source = sourcePoints[2].X;
            double yPoint3Source = sourcePoints[2].Y;

            double xPoint1Target = targetPoints[0].X;
            double yPoint1Target = targetPoints[0].Y;
            double xPoint2Target = targetPoints[1].X;
            double yPoint2Target = targetPoints[1].Y;
            double xPoint3Target = targetPoints[2].X;
            double yPoint3Target = targetPoints[2].Y;

            Matrix<double> imagePointsMatrix = Matrix<double>.Build.DenseOfArray(new double[,] {
                { xPoint1Source, xPoint2Source, xPoint3Source },
                { yPoint1Source, yPoint2Source, yPoint3Source },
                { 1, 1, 1 }
            });

            Matrix<double> transformedPointsMatrix = Matrix<double>.Build.DenseOfArray(new double[,] {
                { xPoint1Target, xPoint2Target, xPoint3Target },
                { yPoint1Target, yPoint2Target, yPoint3Target },
                { 1, 1, 1 }
            });

            Matrix<double> transformationMatrix = imagePointsMatrix * transformedPointsMatrix.Inverse();

            return transformationMatrix;
        }

        private static Matrix<double> GetTransformationMatrixWithFourPoints(List<Point<double>> sourcePoints, List<Point<double>> targetPoints) {
            double xPoint1Source = sourcePoints[0].X;
            double yPoint1Source = sourcePoints[0].Y;
            double xPoint2Source = sourcePoints[1].X;
            double yPoint2Source = sourcePoints[1].Y;
            double xPoint3Source = sourcePoints[2].X;
            double yPoint3Source = sourcePoints[2].Y;
            double xPoint4Source = sourcePoints[3].X;
            double yPoint4Source = sourcePoints[3].Y;

            double xPoint1Target = targetPoints[0].X;
            double yPoint1Target = targetPoints[0].Y;
            double xPoint2Target = targetPoints[1].X;
            double yPoint2Target = targetPoints[1].Y;
            double xPoint3Target = targetPoints[2].X;
            double yPoint3Target = targetPoints[2].Y;
            double xPoint4Target = targetPoints[3].X;
            double yPoint4Target = targetPoints[3].Y;

            // Define the system of linear equations to solve for the transformation matrix coefficients
            var coefficients = Matrix<double>.Build.DenseOfArray(new double[,]
            {
                { xPoint1Target, yPoint1Target, 1, 0, 0, 0, -(xPoint1Source * xPoint1Target), -(xPoint1Source * yPoint1Target) },
                { xPoint2Target, yPoint2Target, 1, 0, 0, 0, -(xPoint2Source * xPoint2Target), -(xPoint2Source * yPoint2Target) },
                { xPoint3Target, yPoint3Target, 1, 0, 0, 0, -(xPoint3Source * xPoint3Target), -(xPoint3Source * yPoint3Target) },
                { xPoint4Target, yPoint4Target, 1, 0, 0, 0, -(xPoint4Source * xPoint4Target), -(xPoint4Source * yPoint4Target) },
                { 0, 0, 0, xPoint1Target, yPoint1Target, 1, -(yPoint1Source * xPoint1Target), -(yPoint1Source * yPoint1Target) },
                { 0, 0, 0, xPoint2Target, yPoint2Target, 1, -(yPoint2Source * xPoint2Target), -(yPoint2Source * yPoint2Target) },
                { 0, 0, 0, xPoint3Target, yPoint3Target, 1, -(yPoint3Source * xPoint3Target), -(yPoint3Source * yPoint3Target) },
                { 0, 0, 0, xPoint4Target, yPoint4Target, 1, -(yPoint4Source * xPoint4Target), -(yPoint4Source * yPoint4Target) }
            });

            var transformedPointsMatrix = Vector<double>.Build.DenseOfArray(new double[] 
                { xPoint1Source, xPoint2Source, xPoint3Source, xPoint4Source, 
                    yPoint1Source, yPoint2Source, yPoint3Source, yPoint4Source });

            // Solve the system of linear equations to find the transformation matrix coefficients
            var transformationMatrixCoefficients = coefficients.Solve(transformedPointsMatrix);

            // Construct the transformation matrix
            var transformationMatrix = Matrix<double>.Build.DenseOfArray(new double[,]
            {
                { transformationMatrixCoefficients[0], transformationMatrixCoefficients[1], transformationMatrixCoefficients[2] },
                { transformationMatrixCoefficients[3], transformationMatrixCoefficients[4], transformationMatrixCoefficients[5] },
                { transformationMatrixCoefficients[6], transformationMatrixCoefficients[7], 1 }
            });

            return transformationMatrix;
        }
    }
}