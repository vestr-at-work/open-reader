
namespace CodeReader {
    struct Point {
        public Point(int x, int y) {
            X = x;
            Y = y;
        }

        public int X;
        public int Y;

        public double DistanceFrom(Point other) {
            return Math.Sqrt(Math.Pow(this.X - other.X, 2) 
                        + Math.Pow(this.Y - other.Y, 2));
        }

        public override string ToString() {
            return $"XCoord: {X}, YCoord: {Y}";
        }
    }
}