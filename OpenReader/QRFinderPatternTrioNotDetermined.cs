
namespace CodeReader {
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
}