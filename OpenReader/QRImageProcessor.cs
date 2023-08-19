using System.Diagnostics;
using CodeReaderCommons;


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

            public int DistanceFrom(PixelCoord other) {
                return (int)Math.Sqrt(Math.Pow(this.XCoord - other.XCoord, 2) 
                            + Math.Pow(this.YCoord - other.YCoord, 2));
            }
        }

        struct QRFinderPatterns {
            public QRFinderPattern TopLeftPattern;
            public QRFinderPattern TopRightPattern;
            public QRFinderPattern BottomLeftPattern;
        }

        struct QRFinderPattern {
            public QRFinderPattern(PixelCoord centroid, int width, int height) {
                Centroid = centroid;
                Width = width;
                Height = height;
            }
            public PixelCoord Centroid;
            public int Width;
            public int Height;
        }
        
        public static bool TryGetRawData<TPixel>(Image<TPixel> image, out RawQRData? rawDataMatrix) 
            where TPixel : unmanaged, IPixel<TPixel> {

            Stopwatch sw = new Stopwatch();
            sw.Start();
            
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



            sw.Stop();
            Console.WriteLine($"time: {sw.Elapsed}");

            binarizedImage.Save("../TestData/QRCodeTestOUTPUT.png");
            binarizedImage.Dispose();
            
            //image.Save("../TestData/QRCodeTest1OUTPUT.png");

            
            // Dummy implementation
            rawDataMatrix = new RawQRData();
            return true;
        }
        

        /// <summary>
        /// Class for finding and recognizing QR code patterns.
        /// </summary>
        static class QRPatternFinder {
            public static bool TryGetFinderPatterns(Image<L8> image, out QRFinderPatterns patterns) {

                List<QRFinderPattern> potentialFinderPatterns = GetPotentialFinderPattern(image);

                // Debug print
                foreach (var pattern in potentialFinderPatterns) {
                    Console.WriteLine($"x: {pattern.Centroid.XCoord}, y: {pattern.Centroid.YCoord}");
                }
                Console.WriteLine("-----");


                if (potentialFinderPatterns.Count < 3) {
                    // Empty assingment
                    patterns = new QRFinderPatterns();
                    return false;
                }

                var finderPatterns = FilterFinderPatterns(potentialFinderPatterns);

                foreach (var pattern in finderPatterns) {
                    Console.WriteLine($"x: {pattern.Centroid.XCoord}, y: {pattern.Centroid.YCoord}");
                }

                patterns = DetermineFinderPatternPosition(finderPatterns);
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
                    // Console.WriteLine("---");

                    // foreach (var bloc in _columnBlocs) {
                    //     if (bloc is not null) 
                    //         Console.WriteLine($"{((ColorBloc)bloc!).StartIndex}, {((ColorBloc)bloc!).EndIndex}");
                    //     else 
                    //         Console.WriteLine("-");
                    // }
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
                            pixelSpan[(finderExtractor.GetMiddleOfColumnBlocs() * image.Width) + centerOfMiddleBloc].PackedValue = 200;

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
                        sumWidth += pattern.Width;
                        sumHeight += pattern.Height;
                    }

                    var averageCentroid = new PixelCoord(sumX / count, sumY / count);
                    var clusterAveragePattern = new QRFinderPattern(averageCentroid, sumWidth / count, sumHeight / count);
                    finalThreeFinderPatterns.Add(clusterAveragePattern);   
                }

                return finalThreeFinderPatterns;
            }

            private static QRFinderPatterns DetermineFinderPatternPosition(List<QRFinderPattern> patterns) {

                return new QRFinderPatterns();
            }

        }
    }
}