
namespace CodeReader {
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
}