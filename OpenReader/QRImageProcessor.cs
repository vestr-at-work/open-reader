using System.Diagnostics;
using CodeReaderCommons;


namespace CodeReader {
    /// <summary>
    /// Class responsible for processing the input image.
    /// Primarily converts image data to raw 2D matrix data for better handeling.
    /// </summary>
    static class QRImageProcessor {

        struct PixelCoord {
            public int XCoord;
            public int YCoord;
        }

        struct QRFinderPatterns {
            public PixelCoord TopLeftPatternCenter;
            public PixelCoord TopRightPatternCenter;
            public PixelCoord BottomLeftPatternCenter;
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

                List<PixelCoord> potentialFinderPatterns = GetPotentialFinderPatternCoords(image);

                patterns = new QRFinderPatterns();
                return true;
            }

            public static bool TryGetAlignmentPatterns(Image<L8> image) {
                return true;
            }

            private struct ColorBloc {
                public ColorBloc(int start, int end) {
                    startIndex = start;
                    endIndex = end;
                }
                public int startIndex;
                public int endIndex; 

                public int Length { 
                    get => (endIndex - startIndex) + 1;
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

                private ColorBloc? _blackBlocLeft;
                private ColorBloc? _whiteBlocLeft;
                private ColorBloc? _blackBlocMiddle;
                private ColorBloc? _whiteBlocRight;
                private ColorBloc? _blackBlocRight;

                // Finder pattern column blocks ordered from top to bottom
                private ColorBloc?[] _columnBlocs = new ColorBloc?[5];
                private bool _currentColumnBlocIsWhite = false;
                private int _currentColumnBlocStart;

                private int _row;
                // Default value just to make _column++ 0;
                private int _column = -1;
                private int _currentRowBlocStart;
                private bool? _currentRowBlocIsWhite;

                public int TestGetMiddle() {
                    return (((ColorBloc)_blackBlocLeft!).startIndex + ((ColorBloc)_blackBlocRight!).endIndex) / 2;
                }

                public void AddNextRowPixel(byte pixelValue) {
                    IsPossibleFinderPattern = false;
                    if (pixelValue == (byte)255) {
                        AddNextPixelWhite();
                        IsPossibleFinderPattern = IsBlocRatioCorrect();
                        return;
                    }

                    AddNextPixelBlack();
                }

                private void AddNextPixelWhite() {
                    _column++;
                    // If image row begins with white
                    if (_blackBlocRight is null && _currentRowBlocIsWhite is null) {
                        _currentRowBlocStart = _column;
                        _currentRowBlocIsWhite = true;
                        return;
                    }
                    if (_currentRowBlocIsWhite is true) {
                        return;
                    }

                    _blackBlocLeft = _blackBlocMiddle;
                    _blackBlocMiddle = _blackBlocRight;
                    _blackBlocRight = new ColorBloc(_currentRowBlocStart, _column - 1);

                    _currentRowBlocStart = _column;
                    _currentRowBlocIsWhite = true;

                    return;
                }

                private void AddNextPixelBlack() {
                    _column++;
                    // If image row begins with black
                    if (_whiteBlocRight is null && _currentRowBlocIsWhite is null) {
                        _currentRowBlocStart = _column;
                        _currentRowBlocIsWhite = false;
                        return;
                    }
                    if (_currentRowBlocIsWhite is false) {
                        return;
                    }

                    _whiteBlocLeft = _whiteBlocRight;
                    _whiteBlocRight = new ColorBloc(_currentRowBlocStart, _column - 1);

                    _currentRowBlocStart = _column;
                    _currentRowBlocIsWhite = false;
                }

                public void AddNextColumnPixelUp(byte pixelValue, int row) {
                    if (pixelValue == (byte)255) {
                        AddNextColumnPixelUpWhite(row);
                        return;
                    }
                    AddNextColumnPixelUpBlack(row);
                }

                private void AddNextColumnPixelUpBlack(int row) {
                    // Top white bloc
                    if (_columnBlocs[1] is null && !_currentColumnBlocIsWhite) {
                        return;
                    }
                    if (!_currentColumnBlocIsWhite) {
                        return;
                    }

                    // Set top white bloc
                    _columnBlocs[1] = new ColorBloc(_currentColumnBlocStart, row - 1);

                    _currentColumnBlocStart = row;
                    _currentColumnBlocIsWhite = false;
                }

                private void AddNextColumnPixelUpWhite(int row) {
                    // Middle black bloc
                    if (_columnBlocs[2] is not null && _currentColumnBlocIsWhite) {
                        return;
                    }
                    if (_columnBlocs[2] is null) {
                        int invalid = Int32.MinValue;
                        _columnBlocs[2] = new ColorBloc(invalid, row - 1);

                        _currentColumnBlocStart = row;
                        _currentColumnBlocIsWhite = true;
                        return;
                    }

                    // Finish off top black bloc
                    _columnBlocs[0] = new ColorBloc(_currentColumnBlocStart, row - 1);

                }

                public void AddNextColumnPixelDown(byte pixelValue, int row) {
                    // DONT FORGET TO RESET STATE VALUES AFTER COLUMN UP METHODS!!!!

                    if (pixelValue == (byte)255) {
                        AddNextColumnPixelDownWhite(row);
                        return;
                    }
                    AddNextColumnPixelDownBlack(row);
                }

                private void AddNextColumnPixelDownBlack(int row) {

                }

                private void AddNextColumnPixelDownWhite(int row) {
                    
                }

                private bool IsBlocRatioCorrect() {
                    const float smallBlocRatio = 1/7f;
                    const float bigBlocRatio = 3/7f;
                    float errorMarginSmall = (smallBlocRatio / 100f) * 30;
                    float errorMarginBig = (bigBlocRatio / 100f) * 30;

                    if (_blackBlocLeft is null || _whiteBlocLeft is null || 
                        _blackBlocMiddle is null || _whiteBlocRight is null || _blackBlocRight is null) {
                        return false;
                    }

                    int blackBlocLeftLength = ((ColorBloc)_blackBlocLeft!).Length;
                    int whiteBlocLeftLength = ((ColorBloc)_whiteBlocLeft!).Length;
                    int blackBlocMiddleLength = ((ColorBloc)_blackBlocMiddle!).Length;
                    int whiteBlocRightLength = ((ColorBloc)_whiteBlocRight!).Length;
                    int blackBlocRightLength = ((ColorBloc)_blackBlocRight!).Length;

                    float allBlocsLength = blackBlocLeftLength + whiteBlocLeftLength + 
                                            blackBlocMiddleLength + whiteBlocRightLength + 
                                            blackBlocRightLength;

                    float blackBlocLeftRatio = blackBlocLeftLength / allBlocsLength;
                    if (BlocRatioOutsideErrorMargin(blackBlocLeftRatio, smallBlocRatio, errorMarginSmall)) {
                        return false;
                    }
                    float whiteBlocLeftRatio = whiteBlocLeftLength / allBlocsLength;
                    if (BlocRatioOutsideErrorMargin(whiteBlocLeftRatio, desiredBlocRatio: smallBlocRatio, errorMarginSmall)) {
                        return false;
                    }
                    float blackBlocMiddleRatio = blackBlocMiddleLength / allBlocsLength;
                    if (BlocRatioOutsideErrorMargin(blackBlocMiddleRatio, desiredBlocRatio: bigBlocRatio, errorMarginBig)) {
                        return false;
                    }
                    float whiteBlocRightRatio = whiteBlocRightLength / allBlocsLength;
                    if (BlocRatioOutsideErrorMargin(whiteBlocRightRatio, desiredBlocRatio: smallBlocRatio, errorMarginSmall)) {
                        return false;
                    }
                    float blackBlocRightRatio = blackBlocRightLength / allBlocsLength;
                    if (BlocRatioOutsideErrorMargin(blackBlocRightRatio, desiredBlocRatio: smallBlocRatio, errorMarginSmall)) {
                        return false;
                    }

                    return true;
                }

                bool BlocRatioOutsideErrorMargin(float blocRatio, float desiredBlocRatio, float errorMargin) {
                    return (blocRatio < desiredBlocRatio - errorMargin || 
                            blocRatio > desiredBlocRatio + errorMargin);
                }
            }

            private static List<PixelCoord> GetPotentialFinderPatternCoords(Image<L8> image) {
                Memory<L8> pixelMemory;
                image.DangerousTryGetSinglePixelMemory(out pixelMemory);
                var pixelSpan = pixelMemory.Span;

                for (int y = 0; y < image.Height; y++) {
                    var finderExtractor = new FinderPatternExtractor(y);
                    for (int x = 0; x < image.Width; x++) {
                        int index = (y * image.Width) + x;
                        var pixelValue = pixelSpan[index].PackedValue;

                        finderExtractor.AddNextRowPixel(pixelValue);

                        if (!finderExtractor.IsPossibleFinderPattern) {
                            continue;
                        }


                        // Check vertical
                        int centerOfMiddleBloc = finderExtractor.TestGetMiddle();
                        int checkVerticalY = y;
                        // Check blocs up
                        while (checkVerticalY >= 0 && finderExtractor.NeedNextUpPixelVertical) {
                            checkVerticalY--;
                            index = (checkVerticalY * image.Width) + centerOfMiddleBloc;
                            pixelValue = pixelSpan[index].PackedValue;

                            finderExtractor.AddNextColumnPixelUp(pixelValue, checkVerticalY);
                        }
                        checkVerticalY = y;
                        // Check blocs down
                        while (checkVerticalY < image.Height && finderExtractor.NeedNextDownPixelVertical) {
                            checkVerticalY++;
                            index = (checkVerticalY * image.Width) + centerOfMiddleBloc;
                            pixelValue = pixelSpan[index].PackedValue;

                            finderExtractor.AddNextColumnPixelDown(pixelValue, checkVerticalY);
                        }

                        if (finderExtractor.IsFinderPattern) {
                            pixelSpan[(y * image.Width) + centerOfMiddleBloc].PackedValue = 127;
                        }
                        
                    }
                }

                return new List<PixelCoord>();
            }
        }
    }
}