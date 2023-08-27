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

        struct PixelCoord {
            public PixelCoord(int x, int y) {
                XCoord = x;
                YCoord = y;
            }

            public int XCoord;
            public int YCoord;

            public double DistanceFrom(PixelCoord other) {
                return Math.Sqrt(Math.Pow(this.XCoord - other.XCoord, 2) 
                            + Math.Pow(this.YCoord - other.YCoord, 2));
            }

            public override string ToString() {
                return $"XCoord: {XCoord}, YCoord: {YCoord}";
            }
        }

        struct QRFinderPatterns {
            public QRFinderPatterns(QRFinderPattern topLeft, QRFinderPattern topRight, QRFinderPattern bottomLeft) {
                TopLeftPattern = topLeft;
                TopRightPattern = topRight;
                BottomLeftPattern = bottomLeft; 
            }
            public QRFinderPattern TopLeftPattern;
            public QRFinderPattern TopRightPattern;
            public QRFinderPattern BottomLeftPattern;
        }

        struct QRFinderPattern {
            public QRFinderPattern(PixelCoord centroid, int width, int height) {
                Centroid = centroid;
                EstimatedWidth = width;
                EstimatedHeight = height;
            }
            public PixelCoord Centroid;
            public int EstimatedWidth;
            public int EstimatedHeight;

            public override string ToString()
            {
                return $"QRFinderPattern: {{ Centroid: {Centroid}, Width: {EstimatedWidth}, Height: {EstimatedHeight} }}";
            }
        }
        
        public static bool TryGetRawData<TPixel>(Image<TPixel> image, out RawQRData? rawDataMatrix) 
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

            if (!QRPatternFinder.TryGetFinderPatterns(binarizedImage, out QRFinderPatterns finderPatterns)) {
                rawDataMatrix = null;
                return false;
            }

            Console.WriteLine($"topLeft: {finderPatterns.TopLeftPattern}");
            Console.WriteLine($"topRight: {finderPatterns.TopRightPattern}");
            Console.WriteLine($"bottomLeft: {finderPatterns.BottomLeftPattern}");
            
            double moduleSize = QRInfoExtractor.GetModuleSize(finderPatterns, out double rotationAngle);
            int version = QRInfoExtractor.GetVersion(finderPatterns, moduleSize, rotationAngle);

            Console.WriteLine($"moduleSize: {moduleSize}, version: {version}");

            byte[,] qrDataMatrix = QRImageResampler.Resample(binarizedImage, finderPatterns, version, out Image<L8> testImage);

            sw.Stop();
            Console.WriteLine($"time: {sw.Elapsed}");

            binarizedImage.Save("../TestData/QRCodeTestOUTPUT.png");
            binarizedImage.Dispose();

            testImage.Save("../TestData/QRCodeTestResampledOUTPUT.png");
            testImage.Dispose();
            //image.Save("../TestData/QRCodeTest1OUTPUT.png");

            
            // Dummy implementation
            rawDataMatrix = new RawQRData();
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
            public static double GetModuleSize(QRFinderPatterns patterns, out double rotationAngle) {
                var topLeft = patterns.TopLeftPattern;
                var topRight = patterns.TopRightPattern;
                var bottomLeft = patterns.BottomLeftPattern;
                Vector2 fromTopLeftToTopRight = new Vector2(topRight.Centroid.XCoord - topLeft.Centroid.XCoord, topRight.Centroid.YCoord - topLeft.Centroid.YCoord);
                int signSwitch = 1;

                // If top left pattern to the right of the top right pattern in the image
                if (patterns.TopLeftPattern.Centroid.XCoord > patterns.TopRightPattern.Centroid.XCoord) {
                    signSwitch = -1;
                }

                PixelCoord oppositeSidePoint = new PixelCoord(topLeft.Centroid.XCoord - (signSwitch * (topLeft.EstimatedWidth / 2)), topLeft.Centroid.YCoord);
                PixelCoord adjacentSidePoint = new PixelCoord(topLeft.Centroid.XCoord + (signSwitch * (topLeft.EstimatedWidth / 2)), topLeft.Centroid.YCoord);
                PixelCoord topRightReferencePoint = new PixelCoord(oppositeSidePoint.XCoord + (int)fromTopLeftToTopRight.X, oppositeSidePoint.YCoord + (int)fromTopLeftToTopRight.Y);
                var angleAdjacentToOppositeSidePoint = GetAdjacentAngle(oppositeSidePoint, adjacentSidePoint, topRightReferencePoint);
                var hypotenuse = topLeft.EstimatedWidth;
                

                // If angle from width not within correct range recalculate with height.
                // This means that top right finder pattern is much higher than top left finder patter.
                if (angleAdjacentToOppositeSidePoint > (Math.PI / 4)) {
                    oppositeSidePoint = new PixelCoord(topLeft.Centroid.XCoord, topLeft.Centroid.YCoord - (signSwitch * (topLeft.EstimatedHeight / 2)));
                    adjacentSidePoint = new PixelCoord(topLeft.Centroid.XCoord, topLeft.Centroid.YCoord + (signSwitch * (topLeft.EstimatedHeight / 2)));
                    topRightReferencePoint = new PixelCoord(oppositeSidePoint.XCoord + (int)fromTopLeftToTopRight.X, oppositeSidePoint.YCoord + (int)fromTopLeftToTopRight.Y);
                    angleAdjacentToOppositeSidePoint = GetAdjacentAngle(oppositeSidePoint, adjacentSidePoint, topRightReferencePoint);
                    hypotenuse = topLeft.EstimatedHeight;
                }
                

                var scale = (((double)topRight.EstimatedWidth / topLeft.EstimatedWidth) + ((double)bottomLeft.EstimatedWidth / topLeft.EstimatedWidth)) / 2;
                Console.WriteLine($"width: {hypotenuse}, scale: {scale}");
                double patternWidth = Math.Cos(angleAdjacentToOppositeSidePoint) * hypotenuse; 
                rotationAngle = angleAdjacentToOppositeSidePoint;
                return (patternWidth * scale) / 7;
                
            }

            /// <summary>
            /// Estimates the version of the QR code based on the distance of the Finder pattens from each other and module size.
            /// Main method of the QRInfoExtractor class.
            /// </summary>
            /// <param name="patterns">Finder patterns.</param>
            /// <param name="moduleSize">QR code's estimated module size.</param>
            /// <param name="rotationAngle"></param>
            /// <returns>Estimated version of the QR code.</returns>
            public static int GetVersion(QRFinderPatterns patterns, double moduleSize, double rotationAngle) {
                var topLeft = patterns.TopLeftPattern;
                var topRight = patterns.TopRightPattern;
                double version = (((topLeft.Centroid.DistanceFrom(topRight.Centroid) / moduleSize) * Math.Cos(rotationAngle) - 10) / 4);
                Console.WriteLine(topLeft.Centroid.DistanceFrom(topRight.Centroid));
                Console.WriteLine($"cos: {Math.Cos(rotationAngle)}, angle: {rotationAngle / (2 * Math.PI) * 360}, version: {version}");

                return Convert.ToInt32(version);
            }
        }


        /// <summary>
        /// Internal class responsible for resampling the high resolution binerized image 
        /// into 2D array of bytes with the same width as is the number of modules a QR code of the given version.
        /// Main public method is called 'Resample'.
        /// </summary>
        static class QRImageResampler {
            /// <summary>
            /// Resamples the high resolution binerized image into 2D array of bytes 
            /// with the same width as is the number of modules a QR code of the given version.
            /// Main method of the QRImageResampler class.
            /// </summary>
            /// <param name="binerizedImage">Binerized image of QR code.</param>
            /// <param name="moduleSize">Size of one module of the QR code.</param>
            /// <param name="version">Estimated version of the QR code.</param>
            /// <returns>Resampled QR code image data. Value 0 means black, value 255 means white.</returns>
            public static byte[,] Resample(Image<L8> binerizedImage, QRFinderPatterns patterns, int version, out Image<L8> image) {
                
                int codeSideLength = 17 + (4 * version);
                int outputSize = codeSideLength;

                // Calculate the affine transformation matrix
                Matrix<double> affineMatrix = CalculateAffineMatrix(patterns, codeSideLength);
                byte[,] resampledImage = new byte[outputSize, outputSize];

                var point = Matrix<double>.Build.DenseOfArray(new double[,] {
                    { 3.5 },
                    { 3.5 },
                    { 1 }
                });

                Console.WriteLine($"{affineMatrix * point}");

                image = new Image<L8>(codeSideLength, codeSideLength);

                for (int y = 0; y < outputSize; y++) {
                    for (int x = 0; x < outputSize; x++) {
                        // Map the output array coordinates to the original image using the affine transformation
                        double originalX = affineMatrix[0, 0] * (x + 0.5) + affineMatrix[0, 1] * (y + 0.5) + affineMatrix[0, 2];
                        double originalY = affineMatrix[1, 0] * (x + 0.5) + affineMatrix[1, 1] * (y + 0.5) + affineMatrix[1, 2];

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

            private static Matrix<double> CalculateAffineMatrix(QRFinderPatterns patterns, int sideLength) {
                double xTopLeft = patterns.TopLeftPattern.Centroid.XCoord;
                double yTopLeft = patterns.TopLeftPattern.Centroid.YCoord;
                double xTopRight = patterns.TopRightPattern.Centroid.XCoord;
                double yTopRight = patterns.TopRightPattern.Centroid.YCoord;
                double xBottomLeft = patterns.BottomLeftPattern.Centroid.XCoord;
                double yBottomLeft = patterns.BottomLeftPattern.Centroid.YCoord;

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
            public static bool TryGetFinderPatterns(Image<L8> image, out QRFinderPatterns patterns) {

                List<QRFinderPattern> potentialFinderPatterns = GetPotentialFinderPattern(image);

                // // Debug print
                // foreach (var pattern in potentialFinderPatterns) {
                //     Console.WriteLine($"x: {pattern.Centroid.XCoord}, y: {pattern.Centroid.YCoord}");
                // }
                // Console.WriteLine("-----");


                if (potentialFinderPatterns.Count < 3) {
                    // Empty assingment
                    patterns = new QRFinderPatterns();
                    return false;
                }

                var finderPatterns = FilterFinderPatterns(potentialFinderPatterns);

                patterns = DetermineFinderPatternRelations(finderPatterns);
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

        
            private static List<QRFinderPattern> GetPotentialFinderPattern(Image<L8> image) {
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

                        // Check vertical
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

                            var centroid = new PixelCoord(centerOfMiddleBloc, finderExtractor.GetMiddleOfColumnBlocs());
                            var pattern = new QRFinderPattern(centroid, finderExtractor.GetPatternWidth(), finderExtractor.GetPatternHeight());
                            finderPatternPixels.Add(pattern);
                        }
                        
                    }
                }

                return finderPatternPixels;
            }

            private static List<QRFinderPattern> FilterFinderPatterns(List<QRFinderPattern> patterns) {
                var clusters = GetClustersBasedOnDistance(patterns, 5);
                return GetThreeAvgPatternsFromMostPopulusClusters(clusters);
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

            private static List<QRFinderPattern> GetThreeAvgPatternsFromMostPopulusClusters(List<List<QRFinderPattern>> clusters) {
                // Sort in descending order based on number of patterns in cluster
                clusters.Sort(delegate(List<QRFinderPattern> clusterA, List<QRFinderPattern> clusterB) {
                    return clusterB.Count.CompareTo(clusterA.Count);
                });

                var finalThreeFinderPatterns = new List<QRFinderPattern>();
                for (int i = 0; i < 3; i++) {
                    int count = 0;
                    int sumX = 0;
                    int sumY = 0;
                    int sumWidth = 0;
                    int sumHeight = 0;
                    foreach (var pattern in clusters[i]) {
                        count++;
                        sumX += pattern.Centroid.XCoord;
                        sumY += pattern.Centroid.YCoord;
                        sumWidth += pattern.EstimatedWidth;
                        sumHeight += pattern.EstimatedHeight;
                    }

                    var averageCentroid = new PixelCoord(sumX / count, sumY / count);
                    var clusterAveragePattern = new QRFinderPattern(averageCentroid, sumWidth / count, sumHeight / count);
                    finalThreeFinderPatterns.Add(clusterAveragePattern);   
                }

                return finalThreeFinderPatterns;
            }

            /// <summary>
            /// Method uses angles between pattern centroids to determine which pattern is which.
            /// </summary>
            /// <param name="patterns"></param>
            /// <returns></returns>
            private static QRFinderPatterns DetermineFinderPatternRelations(List<QRFinderPattern> patterns) {
                double[] angles = new double[3];
                angles[0] = GetAdjacentAngle(patterns[0].Centroid, patterns[1].Centroid, patterns[2].Centroid);
                angles[1] = GetAdjacentAngle(patterns[1].Centroid, patterns[0].Centroid, patterns[2].Centroid);
                angles[2] = GetAdjacentAngle(patterns[2].Centroid, patterns[0].Centroid, patterns[1].Centroid);

                Console.WriteLine($"first: {angles[0]}, second: {angles[1]}, third: {angles[2]}");

                var maxAngle = Math.Max(Math.Max(angles[0], angles[1]), angles[2]);

                // TODO: ADD CHECKING IF ANGLE IS WITHIN ERROR MARGIN
                if (maxAngle == angles[0]) {
                    return GetFinalQRFinderPatterns(patterns[0], patterns[1], patterns[2]);
                }
                if (maxAngle == angles[1]) {
                    return GetFinalQRFinderPatterns(patterns[1], patterns[0], patterns[2]);
                }
                if (maxAngle == angles[2]) {
                    return GetFinalQRFinderPatterns(patterns[2], patterns[0], patterns[1]);
                }

                return new QRFinderPatterns();
            }

            private static QRFinderPatterns GetFinalQRFinderPatterns(QRFinderPattern upperLeft, QRFinderPattern otherPatternA, QRFinderPattern otherPatternB) {

                var crossProduct = ((otherPatternA.Centroid.XCoord - upperLeft.Centroid.XCoord) 
                                * (otherPatternB.Centroid.YCoord - upperLeft.Centroid.YCoord))
                                - ((otherPatternA.Centroid.YCoord - upperLeft.Centroid.YCoord) 
                                * (otherPatternB.Centroid.XCoord - upperLeft.Centroid.XCoord));

                if (crossProduct > 0) {
                    return new QRFinderPatterns(upperLeft, otherPatternA, otherPatternB);
                }

                return new QRFinderPatterns(upperLeft, otherPatternB, otherPatternA);

            }
        }

        /// <summary>
        /// Gets adjacent angle from three coordinates.
        /// </summary>
        /// <param name="mainVertex"></param>
        /// <param name="secondaryVertexA"></param>
        /// <param name="secondaryVertexB"></param>
        /// <returns>Adjacent angle in radians.</returns>
        private static double GetAdjacentAngle(PixelCoord mainVertex, PixelCoord secondaryVertexA, PixelCoord secondaryVertexB) {
            Vector2 mainToA = new Vector2(secondaryVertexA.XCoord - mainVertex.XCoord, secondaryVertexA.YCoord - mainVertex.YCoord);
            Vector2 mainToB = new Vector2(secondaryVertexB.XCoord - mainVertex.XCoord, secondaryVertexB.YCoord - mainVertex.YCoord);

            return Math.Acos((Vector2.Dot(mainToA, mainToB) / (double)(mainToA.Length() * mainToB.Length())));
        }
    }
}