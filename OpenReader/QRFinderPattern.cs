
namespace CodeReader {
    struct QRFinderPattern {
        public QRFinderPattern(Point<int> centroid, int width, int height) {
            Centroid = centroid;
            EstimatedWidth = width;
            EstimatedHeight = height;
        }
        public Point<int> Centroid;
        public int EstimatedWidth;
        public int EstimatedHeight;

        public override string ToString()
        {
            return $"QRFinderPattern: {{ Centroid: {Centroid}, Width: {EstimatedWidth}, Height: {EstimatedHeight} }}";
        }
    }
}