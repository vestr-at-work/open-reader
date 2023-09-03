using System.Diagnostics;
using CodeReaderCommons;
using System.Numerics;
using MathNet.Numerics.LinearAlgebra;


namespace CodeReader {
    /// <summary>
    /// Class responsible for processing the input image.
    /// Primarily converts image data to raw 2D matrix data for better handeling.
    /// </summary>
    static class QRImageProcessor {
        struct QRFinderPatternTrio {
            public QRFinderPatternTrio(QRFinderPattern topLeft, QRFinderPattern topRight, QRFinderPattern bottomLeft) {
                TopLeftPattern = topLeft;
                TopRightPattern = topRight;
                BottomLeftPattern = bottomLeft; 
            }
            public QRFinderPattern TopLeftPattern;
            public QRFinderPattern TopRightPattern;
            public QRFinderPattern BottomLeftPattern;
        }

        struct QRFinderPatternTrioNotDetermined {
            public QRFinderPatternTrioNotDetermined(QRFinderPattern pattern1, QRFinderPattern pattern2, QRFinderPattern pattern3) {
                Pattern1 = pattern1;
                Pattern2 = pattern2;
                Pattern3 = pattern3; 
            }
            public QRFinderPattern Pattern1;
            public QRFinderPattern Pattern2;
            public QRFinderPattern Pattern3;
        }

        struct QRFinderPattern {
            public QRFinderPattern(Point centroid, int width, int height) {
                Centroid = centroid;
                EstimatedWidth = width;
                EstimatedHeight = height;
            }
            public Point Centroid;
            public int EstimatedWidth;
            public int EstimatedHeight;

            public override string ToString()
            {
                return $"QRFinderPattern: {{ Centroid: {Centroid}, Width: {EstimatedWidth}, Height: {EstimatedHeight} }}";
            }
        }
        
        public static bool TryParseQRCode<TPixel>(Image<TPixel> image, out QRCodeParsed? rawDataMatrix) 
            where TPixel : unmanaged, IPixel<TPixel> {

            Stopwatch sw = new Stopwatch();
            sw.Start();
            
            // TODO: Make a method for this
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
            
            image.Mutate(x => x.Grayscale());
            var binarizedImage = Commons.Binarize(image);

            if (!QRPatternFinder.TryGetFinderPatterns(binarizedImage, out QRFinderPatternTrio finderPatterns)) {
                binarizedImage.Save("../TestData/QRCodeTestOUTPUT.png");
                binarizedImage.Dispose();

                rawDataMatrix = null;
                return false;
            }

            Console.WriteLine($"topLeft: {finderPatterns.TopLeftPattern}");
            Console.WriteLine($"topRight: {finderPatterns.TopRightPattern}");
            Console.WriteLine($"bottomLeft: {finderPatterns.BottomLeftPattern}");
            
            double moduleSize = QRInfoExtractor.GetModuleSize(finderPatterns, out double rotationAngle);
            int version = QRInfoExtractor.GetVersion(finderPatterns, moduleSize, rotationAngle);

            Console.WriteLine($"moduleSize: {moduleSize}, version: {version}");

            byte[,] qrDataMatrix = QRImageSampler.Sample(binarizedImage, finderPatterns, version, out Image<L8> testImage);

            sw.Stop();
            Console.WriteLine($"time: {sw.Elapsed}");

            binarizedImage.Save("../TestData/QRCodeTestOUTPUT.png");
            testImage.Save("../TestData/QRCodeTestResampledOUTPUT.png");

            binarizedImage.Dispose();
            testImage.Dispose();
            
            var size = 17 + (4 * version);
            rawDataMatrix = new QRCodeParsed(version, size, qrDataMatrix);
            return true;
        }

        /// <summary>
        /// Internal class responsible for extracting preliminary info about QR code.
        /// Main public methods are 'GetModuleSize' and 'GetVersion'.
        /// </summary>
        static class QRInfoExtractor {
            /// <summary>
            /// Calculates and returns the size of module (black or white square in the QR code) from Finder patterns and estimated width.
            /// Main method of the QRInfoExtractor class. 
            /// </summary>
            /// <param name="patterns">Finder patterns.</param>
            /// <returns>Size of the module.</returns>
            public static double GetModuleSize(QRFinderPatternTrio patterns, out double rotationAngle) {
                var topLeft = patterns.TopLeftPattern;
                var topRight = patterns.TopRightPattern;
                var bottomLeft = patterns.BottomLeftPattern;
                Vector2 fromTopLeftToTopRight = new Vector2(topRight.Centroid.X - topLeft.Centroid.X, topRight.Centroid.Y - topLeft.Centroid.Y);
                int signSwitch = 1;

                // If top left pattern is to the right of the top right pattern in the image
                if (patterns.TopLeftPattern.Centroid.X > patterns.TopRightPattern.Centroid.X) {
                    signSwitch = -1;
                }

                Point oppositeSidePoint = new Point(topLeft.Centroid.X - (signSwitch * (topLeft.EstimatedWidth / 2)), topLeft.Centroid.Y);
                Point adjacentSidePoint = new Point(topLeft.Centroid.X + (signSwitch * (topLeft.EstimatedWidth / 2)), topLeft.Centroid.Y);
                Point topRightReferencePoint = new Point(oppositeSidePoint.X + (int)fromTopLeftToTopRight.X, oppositeSidePoint.Y + (int)fromTopLeftToTopRight.Y);
                var angleAdjacentToOppositeSidePoint = GetAdjacentAngle(oppositeSidePoint, adjacentSidePoint, topRightReferencePoint);
                var hypotenuse = topLeft.EstimatedWidth;
                

                // If angle from width not within correct range recalculate with height.
                // This means that top right finder pattern is much higher than top left finder patter.
                if (angleAdjacentToOppositeSidePoint > (Math.PI / 4)) {

                    // If top left pattern is to the bottom of the top right pattern in the image
                    if (patterns.TopLeftPattern.Centroid.Y > patterns.TopRightPattern.Centroid.Y) {
                        signSwitch = -1;
                    }   

                    oppositeSidePoint = new Point(topLeft.Centroid.X, topLeft.Centroid.Y - (signSwitch * (topLeft.EstimatedHeight / 2)));
                    adjacentSidePoint = new Point(topLeft.Centroid.X, topLeft.Centroid.Y + (signSwitch * (topLeft.EstimatedHeight / 2)));
                    topRightReferencePoint = new Point(oppositeSidePoint.X + (int)fromTopLeftToTopRight.X, oppositeSidePoint.Y + (int)fromTopLeftToTopRight.Y);
                    angleAdjacentToOppositeSidePoint = GetAdjacentAngle(oppositeSidePoint, adjacentSidePoint, topRightReferencePoint);
                    hypotenuse = topLeft.EstimatedHeight;
                }
                

                var scale = (((double)topRight.EstimatedWidth / topLeft.EstimatedWidth) + ((double)bottomLeft.EstimatedWidth / topLeft.EstimatedWidth)) / 2;
                Console.WriteLine($"width: {hypotenuse}, scale: {scale}");
                double patternSideLength = Math.Cos(angleAdjacentToOppositeSidePoint) * hypotenuse; 
                rotationAngle = angleAdjacentToOppositeSidePoint;
                return (patternSideLength * scale) / 7;
                
            }

            /// <summary>
            /// Estimates the version of the QR code based on the distance of the Finder patterns from each other and module size.
            /// Main method of the QRInfoExtractor class.
            /// </summary>
            /// <param name="patterns">Finder patterns.</param>
            /// <param name="moduleSize">QR code's estimated module size.</param>
            /// <param name="rotationAngle"></param>
            /// <returns>Estimated version of the QR code.</returns>
            public static int GetVersion(QRFinderPatternTrio patterns, double moduleSize, double rotationAngle) {
                var topLeft = patterns.TopLeftPattern;
                var topRight = patterns.TopRightPattern;

                double version = (((topLeft.Centroid.DistanceFrom(topRight.Centroid) / moduleSize) * Math.Cos(rotationAngle) - 10) / 4);

                Console.WriteLine(topLeft.Centroid.DistanceFrom(topRight.Centroid));
                Console.WriteLine($"cos: {Math.Cos(rotationAngle)}, angle: {rotationAngle / (2 * Math.PI) * 360}, version: {version}");

                return Convert.ToInt32(version);
            }
        }


        /// <summary>
        /// Internal class responsible for sampling the high resolution binerized image 
        /// into 2D array of bytes with the same width as is the number of modules a QR code of the given version.
        /// Main public method is called 'Sample'.
        /// </summary>
        static class QRImageSampler {
            /// <summary>
            /// Samples the high resolution binerized image into 2D array of bytes 
            /// with the same width as is the number of modules a QR code of the given version.
            /// Main method of the QRImageSampler class.
            /// </summary>
            /// <param name="binerizedImage">Binerized image of QR code.</param>
            /// <param name="moduleSize">Size of one module of the QR code.</param>
            /// <param name="version">Estimated version of the QR code.</param>
            /// <returns>Sampled QR code image data. Value 0 means black, value 255 means white.</returns>
            public static byte[,] Sample(Image<L8> binerizedImage, QRFinderPatternTrio patterns, int version, out Image<L8> image) {
                int codeSideLength = 17 + (4 * version);
                int outputSize = codeSideLength;

                Matrix<double> transformationMatrix = CalculateTransformationMatrix(patterns, codeSideLength);
                byte[,] resampledImage = new byte[outputSize, outputSize];

                // var point = Matrix<double>.Build.DenseOfArray(new double[,] {
                //     { 3.5 },
                //     { 3.5 },
                //     { 1 }
                // });

                // Console.WriteLine($"{transformationMatrix * point}");

                image = new Image<L8>(codeSideLength, codeSideLength);

                for (int y = 0; y < outputSize; y++) {
                    for (int x = 0; x < outputSize; x++) {
                        // Map the output array coordinates to the original image using the affine transformation
                        double originalX = transformationMatrix[0, 0] * (x + 0.5) + transformationMatrix[0, 1] * (y + 0.5) + transformationMatrix[0, 2];
                        double originalY = transformationMatrix[1, 0] * (x + 0.5) + transformationMatrix[1, 1] * (y + 0.5) + transformationMatrix[1, 2];

                        int pixelX = (int)Math.Round(originalX);
                        int pixelY = (int)Math.Round(originalY);

                        // Check if the pixel coordinates are within the image bounds
                        if (pixelX >= 0 && pixelX < binerizedImage.Width && pixelY >= 0 && pixelY < binerizedImage.Height) {
                            byte pixelValue = binerizedImage[pixelX, pixelY].PackedValue;

                            resampledImage[x, y] = pixelValue;
                            image[x, y] = new L8(pixelValue);
                        }
                    }
                }

                return resampledImage;
            }

            private static Matrix<double> CalculateTransformationMatrix(QRFinderPatternTrio patterns, int sideLength) {
                double xTopLeft = patterns.TopLeftPattern.Centroid.X;
                double yTopLeft = patterns.TopLeftPattern.Centroid.Y;
                double xTopRight = patterns.TopRightPattern.Centroid.X;
                double yTopRight = patterns.TopRightPattern.Centroid.Y;
                double xBottomLeft = patterns.BottomLeftPattern.Centroid.X;
                double yBottomLeft = patterns.BottomLeftPattern.Centroid.Y;

                Matrix<double> imagePointsMatrix = Matrix<double>.Build.DenseOfArray(new double[,] {
                    { xTopLeft, xTopRight, xBottomLeft },
                    { yTopLeft, yTopRight, yBottomLeft },
                    { 1, 1, 1 }
                });

                Matrix<double> transformedPointsMatrix = Matrix<double>.Build.DenseOfArray(new double[,] {
                    { 3.5, sideLength - 3.5 , 3.5 },
                    { 3.5, 3.5, sideLength - 3.5 },
                    { 1, 1, 1 }
                });

                Matrix<double> transformationMatrix = imagePointsMatrix * transformedPointsMatrix.Inverse();

                return transformationMatrix;

                //Console.WriteLine(transformationMatrix * transformedPointsMatrix);
            }
        }
        

        /// <summary>
        /// Class for finding and recognizing QR code patterns.
        /// </summary>
        static class QRPatternFinder {
            public static bool TryGetFinderPatterns(Image<L8> image, out QRFinderPatternTrio patternTrio) {
                List<QRFinderPattern> finderPatterns = GetPotentialFinderPatterns(image);

                if (finderPatterns.Count < 3) {
                    // Empty assingment
                    patternTrio = new QRFinderPatternTrio();
                    return false;
                }

                if (!TryGetFinderPatternTrio(finderPatterns, out QRFinderPatternTrioNotDetermined patternTrioNotDetermined)) {
                    // Empty assingment
                    patternTrio = new QRFinderPatternTrio();
                    return false;
                }

                patternTrio = DetermineFinderPatternRelations(patternTrioNotDetermined);
                return true;
            }


            public static bool TryGetAlignmentPatterns(Image<L8> image) {
                return true;
            }

            private struct ColorBloc {
                public ColorBloc(int start, int end) {
                    StartIndex = start;
                    EndIndex = end;
                }
                public int StartIndex;
                public int EndIndex; 

                public int Length { 
                    get => Math.Abs((EndIndex - StartIndex) + 1);
                }
            }


            /// <summary>
            /// Private class used to find finder pattern based on relation of black and white blocks.
            /// Ratio of finder pattern's scan line black:white:black:white:black blocs should be 1:1:3:1:1. 
            /// This ratio is kept from all directions (we are checking first horizontal then vertical).
            /// 
            /// TODO: Explain better the public methods and sketch out the algorithm better.
            /// </summary>
            private class FinderPatternExtractor {
                public FinderPatternExtractor(int row) {
                    _row = row;
                }

                public bool IsPossibleFinderPattern = false;
                public bool IsFinderPattern = false;
                public bool NeedNextUpPixelVertical = true;
                public bool NeedNextDownPixelVertical = true;

                
                // Finder pattern column color blocs ordered from top to bottom
                private ColorBloc?[] _columnBlocs = new ColorBloc?[5];
                private bool _currentColumnBlocUpIsWhite = false;
                private bool _currentColumnBlocDownIsWhite = false;
                private int _currentColumnBlocStart;
                private bool _isColumnInfoDisposed = true;


                // Finder pattern row color blocs ordered from left to right
                private ColorBloc?[] _rowBlocs = new ColorBloc?[5];
                private int _row;
                private int _currentRowBlocStart;
                private bool? _currentRowBlocIsWhite;

                enum Bloc { 
                    FirstBlack = 0,
                    FirstWhite = 1,
                    MiddleBlack = 2,
                    LastWhite = 3,
                    LastBlack = 4

                }

                public int GetPatternWidth() {
                    return (((ColorBloc)_rowBlocs[(int)Bloc.LastBlack]!).EndIndex - ((ColorBloc)_rowBlocs[(int)Bloc.FirstBlack]!).StartIndex);
                }

                public int GetPatternHeight() {
                    return (((ColorBloc)_columnBlocs[(int)Bloc.LastBlack]!).EndIndex - ((ColorBloc)_columnBlocs[(int)Bloc.FirstBlack]!).StartIndex);
                }

                public int GetMiddleOfRowBlocs() {
                    return (((ColorBloc)_rowBlocs[(int)Bloc.FirstBlack]!).StartIndex + ((ColorBloc)_rowBlocs[(int)Bloc.LastBlack]!).EndIndex) / 2;
                }

                /// <summary>
                /// Gets middle 'y' coordinate of column blocs. Call only if IsFinderPattern = true!
                /// </summary>
                /// <returns>Middle 'y' coordinate of column blocs.</returns>
                public int GetMiddleOfColumnBlocs() {
                    return (((ColorBloc)_columnBlocs[(int)Bloc.FirstBlack]!).StartIndex + ((ColorBloc)_columnBlocs[(int)Bloc.LastBlack]!).EndIndex) / 2;
                }

                public void AddNextRowPixel(byte pixelValue, int column) {
                    if (!_isColumnInfoDisposed) {
                        DisposeColumnInfo();
                    }
                    IsPossibleFinderPattern = false;

                    if (pixelValue == (byte)255) {
                        AddNextPixelWhite(column);
                        return;
                    }

                    AddNextPixelBlack(column);
                }

                private void AddNextPixelWhite(int column) {
                    // If image row begins with white
                    if (_rowBlocs[(int)Bloc.LastBlack] is null && _currentRowBlocIsWhite is null) {
                        _currentRowBlocStart = column;
                        _currentRowBlocIsWhite = true;
                        return;
                    }
                    if (_currentRowBlocIsWhite is true) {
                        return;
                    }

                    // Move black blocs one to the left
                    _rowBlocs[(int)Bloc.FirstBlack] = _rowBlocs[(int)Bloc.MiddleBlack];
                    _rowBlocs[(int)Bloc.MiddleBlack] = _rowBlocs[(int)Bloc.LastBlack];
                    _rowBlocs[(int)Bloc.LastBlack] = new ColorBloc(_currentRowBlocStart, column - 1);

                    _currentRowBlocStart = column;
                    _currentRowBlocIsWhite = true;

                    IsPossibleFinderPattern = IsBlocRatioCorrect(_rowBlocs);
                }

                private void AddNextPixelBlack(int column) {
                    // If image row begins with black
                    if (_rowBlocs[(int)Bloc.LastWhite] is null && _currentRowBlocIsWhite is null) {
                        _currentRowBlocStart = column;
                        _currentRowBlocIsWhite = false;
                        return;
                    }
                    if (_currentRowBlocIsWhite is false) {
                        return;
                    }

                    // Move white blocs one to the left
                    _rowBlocs[(int)Bloc.FirstWhite] = _rowBlocs[(int)Bloc.LastWhite];
                    _rowBlocs[(int)Bloc.LastWhite] = new ColorBloc(_currentRowBlocStart, column - 1);

                    _currentRowBlocStart = column;
                    _currentRowBlocIsWhite = false;
                }

                public void AddNextColumnPixelUp(byte pixelValue, int row) {
                    _isColumnInfoDisposed = false;
                    if (pixelValue == (byte)255) {
                        AddNextColumnPixelUpWhite(row);
                        return;
                    }
                    AddNextColumnPixelUpBlack(row);
                }

                private void AddNextColumnPixelUpBlack(int row) {
                    // Top white bloc
                    if (_columnBlocs[(int)Bloc.FirstWhite] is null && !_currentColumnBlocUpIsWhite) {
                        return;
                    }
                    if (!_currentColumnBlocUpIsWhite) {
                        return;
                    }

                    // Set top white bloc
                    _columnBlocs[(int)Bloc.FirstWhite] = new ColorBloc(row + 1, _currentColumnBlocStart);

                    _currentColumnBlocStart = row;
                    _currentColumnBlocUpIsWhite = false;
                }

                private void AddNextColumnPixelUpWhite(int row) {
                    if (_currentColumnBlocUpIsWhite) {
                        return;
                    }
                    if (_columnBlocs[(int)Bloc.MiddleBlack] is null) {
                        int invalid = Int32.MinValue;
                        _columnBlocs[(int)Bloc.MiddleBlack] = new ColorBloc(row + 1, invalid);

                        _currentColumnBlocStart = row;
                        _currentColumnBlocUpIsWhite = true;
                        return;
                    }

                    // Finish off top black bloc
                    _columnBlocs[(int)Bloc.FirstBlack] = new ColorBloc(row + 1, _currentColumnBlocStart);
                    NeedNextUpPixelVertical = false;
                }

                public void AddNextColumnPixelDown(byte pixelValue, int row) {
                    _isColumnInfoDisposed = false;

                    if (pixelValue == (byte)255) {
                        AddNextColumnPixelDownWhite(row);
                        return;
                    }
                    AddNextColumnPixelDownBlack(row);
                }

                private void AddNextColumnPixelDownBlack(int row) {
                    if (!_currentColumnBlocDownIsWhite) {
                        return;
                    }

                    _columnBlocs[(int)Bloc.LastWhite] = new ColorBloc(_currentColumnBlocStart, row - 1);

                    _currentColumnBlocStart = row;
                    _currentColumnBlocDownIsWhite = false;
                }

                private void AddNextColumnPixelDownWhite(int row) {
                    if (_currentColumnBlocDownIsWhite) {
                        return;
                    }
                    // Bottom white bloc
                    if (!_currentColumnBlocDownIsWhite && _columnBlocs[(int)Bloc.LastWhite] is null) {
                        _columnBlocs[(int)Bloc.MiddleBlack] = 
                            new ColorBloc(((ColorBloc)_columnBlocs[(int)Bloc.MiddleBlack]!).StartIndex, row - 1);

                        _currentColumnBlocDownIsWhite = true;
                        _currentColumnBlocStart = row;
                        return;
                    }

                    _columnBlocs[(int)Bloc.LastBlack] = new ColorBloc(_currentColumnBlocStart, row - 1);
                    NeedNextDownPixelVertical = false;

                    IsFinderPattern = IsBlocRatioCorrect(_columnBlocs);
                }

                private bool IsBlocRatioCorrect(ColorBloc?[] blocs) {
                    const float smallBlocRatio = 1/7f;
                    const float bigBlocRatio = 3/7f;
                    float errorMarginSmall = (smallBlocRatio / 100f) * 40;
                    float errorMarginBig = (bigBlocRatio / 100f) * 40;

                    float allBlocsLength = 0;
                    foreach (var bloc in blocs) {
                        if (bloc is null) {
                            return false;
                        }

                        allBlocsLength += ((ColorBloc)bloc).Length;
                    }


                    float blackBlocFirstRatio = ((ColorBloc)blocs[(int)Bloc.FirstBlack]!).Length / allBlocsLength;
                    if (BlocRatioOutsideErrorMargin(blackBlocFirstRatio, smallBlocRatio, errorMarginSmall)) {
                        return false;
                    }
                    float whiteBlocFirstRatio = ((ColorBloc)blocs[(int)Bloc.FirstWhite]!).Length / allBlocsLength;
                    if (BlocRatioOutsideErrorMargin(whiteBlocFirstRatio, desiredBlocRatio: smallBlocRatio, errorMarginSmall)) {
                        return false;
                    }
                    float blackBlocMiddleRatio = ((ColorBloc)blocs[(int)Bloc.MiddleBlack]!).Length / allBlocsLength;
                    if (BlocRatioOutsideErrorMargin(blackBlocMiddleRatio, desiredBlocRatio: bigBlocRatio, errorMarginBig)) {
                        return false;
                    }
                    float whiteBlocLastRatio = ((ColorBloc)blocs[(int)Bloc.LastWhite]!).Length / allBlocsLength;
                    if (BlocRatioOutsideErrorMargin(whiteBlocLastRatio, desiredBlocRatio: smallBlocRatio, errorMarginSmall)) {
                        return false;
                    }
                    float blackBlocLastRatio = ((ColorBloc)blocs[(int)Bloc.LastBlack]!).Length / allBlocsLength;
                    if (BlocRatioOutsideErrorMargin(blackBlocLastRatio, desiredBlocRatio: smallBlocRatio, errorMarginSmall)) {
                        return false;
                    }

                    return true;
                }

                bool BlocRatioOutsideErrorMargin(float blocRatio, float desiredBlocRatio, float errorMargin) {
                    return (blocRatio < desiredBlocRatio - errorMargin || 
                            blocRatio > desiredBlocRatio + errorMargin);
                }

                private void DisposeColumnInfo() {
                    IsFinderPattern = false;
                    _columnBlocs = new ColorBloc?[5];
                    _currentColumnBlocUpIsWhite = false;
                    _isColumnInfoDisposed = true;
                    NeedNextDownPixelVertical = true;
                    NeedNextUpPixelVertical = true;
                }
            }

        
            private static List<QRFinderPattern> GetPotentialFinderPatterns(Image<L8> image) {
                List<QRFinderPattern> finderPatternPixels = new List<QRFinderPattern>();

                Memory<L8> pixelMemory;
                image.DangerousTryGetSinglePixelMemory(out pixelMemory);
                var pixelSpan = pixelMemory.Span;

                for (int y = 0; y < image.Height; y++) {
                    var finderExtractor = new FinderPatternExtractor(y);
                    for (int x = 0; x < image.Width; x++) {
                        int index = (y * image.Width) + x;
                        var pixelValue = pixelSpan[index].PackedValue;

                        finderExtractor.AddNextRowPixel(pixelValue, x);

                        if (!finderExtractor.IsPossibleFinderPattern) {
                            continue;
                        }

                        // Check vertical ---- TODO: SHOULD BE A FUNCTION

                        int centerOfMiddleBloc = finderExtractor.GetMiddleOfRowBlocs();
                        int checkVerticalY = y;
                        // Check blocs up
                        while (checkVerticalY > 0 && finderExtractor.NeedNextUpPixelVertical) {
                            checkVerticalY--;
                            index = (checkVerticalY * image.Width) + centerOfMiddleBloc;
                            pixelValue = pixelSpan[index].PackedValue;

                            finderExtractor.AddNextColumnPixelUp(pixelValue, checkVerticalY);
                        }
                        checkVerticalY = y;
                        // Check blocs down
                        while (checkVerticalY < image.Height - 1 && finderExtractor.NeedNextDownPixelVertical) {
                            checkVerticalY++;
                            index = (checkVerticalY * image.Width) + centerOfMiddleBloc;
                            pixelValue = pixelSpan[index].PackedValue;

                            finderExtractor.AddNextColumnPixelDown(pixelValue, checkVerticalY);
                        }

                        if (finderExtractor.IsFinderPattern) {
                            //pixelSpan[(finderExtractor.GetMiddleOfColumnBlocs() * image.Width) + centerOfMiddleBloc].PackedValue = 200;

                            var centroid = new Point(centerOfMiddleBloc, finderExtractor.GetMiddleOfColumnBlocs());
                            var pattern = new QRFinderPattern(centroid, finderExtractor.GetPatternWidth(), finderExtractor.GetPatternHeight());
                            finderPatternPixels.Add(pattern);
                        }
                        
                    }
                }

                return finderPatternPixels;
            }

            private static bool TryGetFinderPatternTrio(List<QRFinderPattern> patterns, out QRFinderPatternTrioNotDetermined patternTrio) {
                var clusters = GetClustersBasedOnDistance(patterns, 5);
                if (clusters.Count < 3) {
                    // empty asignment
                    patternTrio = new QRFinderPatternTrioNotDetermined();
                    return false;
                }

                patternTrio = GetFinderPatternTrioFromClusters(clusters);
                return true;
            }

            private static List<List<QRFinderPattern>> GetClustersBasedOnDistance(List<QRFinderPattern> patterns, int distanceThreshold) {
                List<List<QRFinderPattern>> clusters = new List<List<QRFinderPattern>>();
                foreach (var pattern in patterns) {
                    if (clusters.Count == 0) {
                        clusters.Add(new List<QRFinderPattern>() { pattern });
                        continue;
                    }
                    bool gotAdded = false;
                    foreach (var cluster in clusters) {
                        if (pattern.Centroid.DistanceFrom(cluster[0].Centroid) <= distanceThreshold) {
                            cluster.Add(pattern);
                            gotAdded = true;
                            break;
                        }
                    }
                    if (!gotAdded) {
                        clusters.Add(new List<QRFinderPattern>() { pattern });
                    }
                }
                return clusters;
            }

            private static QRFinderPatternTrioNotDetermined GetFinderPatternTrioFromClusters(List<List<QRFinderPattern>> clusters) {
                // Sort in descending order based on number of patterns in cluster
                clusters.Sort(delegate(List<QRFinderPattern> clusterA, List<QRFinderPattern> clusterB) {
                    return clusterB.Count.CompareTo(clusterA.Count);
                });

                var finderPatternTrio = new QRFinderPattern[3];
                for (int i = 0; i < 3; i++) {
                    int count = 0;
                    int sumX = 0;
                    int sumY = 0;
                    int sumWidth = 0;
                    int sumHeight = 0;
                    foreach (var pattern in clusters[i]) {
                        count++;
                        sumX += pattern.Centroid.X;
                        sumY += pattern.Centroid.Y;
                        sumWidth += pattern.EstimatedWidth;
                        sumHeight += pattern.EstimatedHeight;
                    }

                    var averageCentroid = new Point(sumX / count, sumY / count);
                    var clusterAveragePattern = new QRFinderPattern(averageCentroid, sumWidth / count, sumHeight / count);
                    finderPatternTrio[i] = (clusterAveragePattern);   
                }

                return new QRFinderPatternTrioNotDetermined(finderPatternTrio[0], finderPatternTrio[1], finderPatternTrio[2]);
            }

            /// <summary>
            /// Method uses angles between pattern centroids to determine patterns relation and position.
            /// </summary>
            /// <param name="patterns"></param>
            /// <returns></returns>
            private static QRFinderPatternTrio DetermineFinderPatternRelations(QRFinderPatternTrioNotDetermined patternTrio) {
                var angleByPattern1 = GetAdjacentAngle(patternTrio.Pattern1.Centroid, patternTrio.Pattern2.Centroid, patternTrio.Pattern3.Centroid);
                var angleByPattern2 = GetAdjacentAngle(patternTrio.Pattern2.Centroid, patternTrio.Pattern1.Centroid, patternTrio.Pattern3.Centroid);
                var angleByPattern3 = GetAdjacentAngle(patternTrio.Pattern3.Centroid, patternTrio.Pattern1.Centroid, patternTrio.Pattern2.Centroid);

                Console.WriteLine($"first: {angleByPattern1}, second: {angleByPattern2}, third: {angleByPattern3}");

                var maxAngle = Math.Max(Math.Max(angleByPattern1, angleByPattern2), angleByPattern3);

                // TODO: ADD CHECKING IF ANGLEs ARE WITHIN ERROR MARGIN

                // Maximal angle is by the upper left pattern. Now we determine the other two patterns.
                if (maxAngle == angleByPattern1) {
                    return GetFinalQRFinderPatterns(patternTrio.Pattern1, patternTrio.Pattern2, patternTrio.Pattern3);
                }
                else if (maxAngle == angleByPattern2) {
                    return GetFinalQRFinderPatterns(patternTrio.Pattern2, patternTrio.Pattern1, patternTrio.Pattern3);
                }
                else if (maxAngle == angleByPattern3) {
                    return GetFinalQRFinderPatterns(patternTrio.Pattern3, patternTrio.Pattern1, patternTrio.Pattern2);
                }

                // Unreachable code
                return new QRFinderPatternTrio();
            }

            private static QRFinderPatternTrio GetFinalQRFinderPatterns(QRFinderPattern upperLeft, QRFinderPattern otherPatternA, QRFinderPattern otherPatternB) {
                var crossProduct = ((otherPatternA.Centroid.X - upperLeft.Centroid.X) 
                                * (otherPatternB.Centroid.Y - upperLeft.Centroid.Y))
                                - ((otherPatternA.Centroid.Y - upperLeft.Centroid.Y) 
                                * (otherPatternB.Centroid.X - upperLeft.Centroid.X));

                if (crossProduct > 0) {
                    return new QRFinderPatternTrio(upperLeft, otherPatternA, otherPatternB);
                }

                return new QRFinderPatternTrio(upperLeft, otherPatternB, otherPatternA);

            }
        }

        /// <summary>
        /// Gets adjacent angle from three coordinates.
        /// </summary>
        /// <param name="mainVertex"></param>
        /// <param name="secondaryVertexA"></param>
        /// <param name="secondaryVertexB"></param>
        /// <returns>Adjacent angle in radians.</returns>
        private static double GetAdjacentAngle(Point mainVertex, Point secondaryVertexA, Point secondaryVertexB) {
            Vector2 mainToA = new Vector2(secondaryVertexA.X - mainVertex.X, secondaryVertexA.Y - mainVertex.Y);
            Vector2 mainToB = new Vector2(secondaryVertexB.X - mainVertex.X, secondaryVertexB.Y - mainVertex.Y);

            return Math.Acos((Vector2.Dot(mainToA, mainToB) / (double)(mainToA.Length() * mainToB.Length())));
        }
    }
}