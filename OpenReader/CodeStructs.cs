
namespace CodeReader {
    struct Point : IEquatable<Point> {
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

        public bool Equals(Point other) {
            return (this.X == other.X) && (this.Y == other.Y);
        }       

        public override string ToString() {
            return $"XCoord: {X}, YCoord: {Y}";
        }
    }
}