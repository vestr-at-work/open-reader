using SixLabors.ImageSharp.Processing.Processors.Binarization;
using CodeReaderCommons;
using System.Diagnostics;

namespace CodeReader {

    public enum ContentType {
        Text, 
        Binary,
        Action
    }

    public class ScanResult {
        public bool Success { get; init; }
        public ContentType? DataType { get; init; }
        public object? Data { get; init; }
    }

    public class RawQRData {
        public int EstimatedVersion { get; set; }
        public int Size { get; set; }
        public byte[,]? Data {get; set; }
    }


    public interface I2DCodeScanner {
        public ScanResult Scan<TPixel>(Image<TPixel> image) where TPixel : unmanaged, IPixel<TPixel>;
    }

    /// <summary>
    /// Main class responsible for scanning and decoding QR codes.
    /// </summary>
    public class QRScanner : I2DCodeScanner {
        public QRScanner() {}

        /// <summary>
        /// Main method of this class. Scans the image and decodes the data.
        /// </summary>
        /// <param name="image">Input image</param>
        /// <returns></returns>
        public ScanResult Scan<TPixel>(Image<TPixel> image) where TPixel : unmanaged, IPixel<TPixel> {
            
            if (!ImageProcessor.TryGetRawData(image, out RawQRData codeData)) {
                return new ScanResult() { Success = false };
            }

            if (!QRCodeDecoder.TryGetFormatInfo(codeData, out ContentType dataType)) {
                return new ScanResult() { Success = false };
            }

            if (!QRCodeDecoder.TryGetData(codeData, out object data)) {
                return new ScanResult() { Success = false };
            }


            return new ScanResult() { Success = true, DataType = dataType, Data = data };
        }

        /// <summary>
        /// Internal class encapsulating methods for decoding 
        /// </summary>
        class QRCodeDecoder {
            public static bool TryGetFormatInfo(RawQRData codeData, out ContentType dataType) {

                // Dummy implementation
                dataType = ContentType.Text;
                return true;
            }

            public static bool TryGetData(RawQRData codeData, out object decodedData) {
                

                // Dummy implementation
                decodedData = (object)"Hello World";
                return true;
            }
        }


        /// <summary>
        /// Class responsible for processing the input image.
        /// Primarily converts image data to raw 2D matrix data for better handeling.
        /// </summary>
        static class ImageProcessor {

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

                {
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
                }
                
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

                    private ColorBloc? _blackBlocLeft;
                    private ColorBloc? _whiteBlocLeft;
                    private ColorBloc? _blackBlocMiddle;
                    private ColorBloc? _whiteBlocRight;
                    private ColorBloc? _blackBlocRight;

                    private int _row;
                    // Default value just to make _column++ 0;
                    private int _column = -1;
                    private int _currentBlocStart;
                    private bool? _currentBlocIsWhite;

                    public int TestGetMiddle() {
                        return (((ColorBloc)_blackBlocLeft!).startIndex + ((ColorBloc)_blackBlocRight!).endIndex) / 2;
                    }

                    public bool AddNextPixelWhite() {
                        _column++;
                        // If image row begins with white
                        if (_blackBlocRight is null && _currentBlocIsWhite is null) {
                            _currentBlocStart = _column;
                            _currentBlocIsWhite = true;
                            return false;
                        }
                        if (_currentBlocIsWhite is true) {
                            return false;
                        }

                        _blackBlocLeft = _blackBlocMiddle;
                        _blackBlocMiddle = _blackBlocRight;
                        _blackBlocRight = new ColorBloc(_currentBlocStart, _column - 1);

                        _currentBlocStart = _column;
                        _currentBlocIsWhite = true;

                        return IsBlocRatioCorrect();
                    }

                    public void AddNextPixelBlack() {
                        _column++;
                        // If image row begins with black
                        if (_whiteBlocRight is null && _currentBlocIsWhite is null) {
                            _currentBlocStart = _column;
                            _currentBlocIsWhite = false;
                            return;
                        }
                        if (_currentBlocIsWhite is false) {
                            return;
                        }

                        _whiteBlocLeft = _whiteBlocRight;
                        _whiteBlocRight = new ColorBloc(_currentBlocStart, _column - 1);

                        _currentBlocStart = _column;
                        _currentBlocIsWhite = false;
                    }

                    private bool IsBlocRatioCorrect() {
                        const float smallBlocRatio = 1/7f;
                        const float bigBlocRatio = 3/7f;
                        float errorMargin = (smallBlocRatio / 100f) * 15;

                        if (_blackBlocLeft is null || _whiteBlocLeft is null || 
                            _blackBlocMiddle is null || _whiteBlocRight is null || _blackBlocRight is null) {
                            return false;
                        }

                        // TODO Can this be done better?
                        for (int i = -1; i <= 1; i++) {
                            for (int j = -1; j <= 1; j++) {
                                for (int k = -1; k <= 1; k++) {
                                    for (int l = -1; l <= 1; l++) {
                                        for (int m = -1; m <= 1; m++) {
                                            
                                            int blackBlocLeftLength = ((ColorBloc)_blackBlocLeft!).Length + i;
                                            int whiteBlocLeftLength = ((ColorBloc)_whiteBlocLeft!).Length + j;
                                            int blackBlocMiddleLength = ((ColorBloc)_blackBlocMiddle!).Length + k;
                                            int whiteBlocRightLength = ((ColorBloc)_whiteBlocRight!).Length + l;
                                            int blackBlocRightLength = ((ColorBloc)_blackBlocRight!).Length + m;

                                            float allBlocsLength = blackBlocLeftLength + 
                                                                 whiteBlocLeftLength +
                                                                 blackBlocMiddleLength +
                                                                 whiteBlocRightLength +
                                                                 blackBlocRightLength;

                                            float blackBlocLeftRatio = blackBlocLeftLength / allBlocsLength;
                                            if (BlocRatioOutsideErrorMargin(blackBlocLeftRatio, smallBlocRatio, errorMargin)) {
                                                continue;
                                            }
                                            float whiteBlocLeftRatio = whiteBlocLeftLength / allBlocsLength;
                                            if (BlocRatioOutsideErrorMargin(whiteBlocLeftRatio, desiredBlocRatio: smallBlocRatio, errorMargin)) {
                                                continue;
                                            }
                                            float blackBlocMiddleRatio = blackBlocMiddleLength / allBlocsLength;
                                            if (BlocRatioOutsideErrorMargin(blackBlocMiddleRatio, desiredBlocRatio: bigBlocRatio, errorMargin)) {
                                                continue;
                                            }
                                            float whiteBlocRightRatio = whiteBlocRightLength / allBlocsLength;
                                            if (BlocRatioOutsideErrorMargin(whiteBlocRightRatio, desiredBlocRatio: smallBlocRatio, errorMargin)) {
                                                continue;
                                            }
                                            float blackBlocRightRatio = blackBlocRightLength / allBlocsLength;
                                            if (BlocRatioOutsideErrorMargin(blackBlocRightRatio, desiredBlocRatio: smallBlocRatio, errorMargin)) {
                                                continue;
                                            }

                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                        return false;
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

                            if (pixelValue == (byte)255) {
                                if (finderExtractor.AddNextPixelWhite()) {
                                    pixelSpan[(y * image.Width) + finderExtractor.TestGetMiddle()].PackedValue = 127;
                                }
                            }
                            else {
                                finderExtractor.AddNextPixelBlack();
                            }
                        }
                    }

                    return new List<PixelCoord>();
                }
            }
        }
    }
}