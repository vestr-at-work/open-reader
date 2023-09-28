
using System.Numerics;

namespace CodeReader {
    public struct Point<TUnderlying> : IEquatable<Point<TUnderlying>>
         where TUnderlying : INumber<TUnderlying>, IConvertible {
        public Point(TUnderlying x, TUnderlying y) {
            X = x;
            Y = y;
        }

        public TUnderlying X;
        public TUnderlying Y;

        public double DistanceFrom(Point<TUnderlying> other) {
            return Math.Sqrt(Math.Pow((this.X - other.X).ToDouble(null), 2) 
                        + Math.Pow((this.Y - other.Y).ToDouble(null), 2));
        }

        public bool Equals(Point<TUnderlying> other) {
            return (this.X == other.X) && (this.Y == other.Y);
        }       

        public override string ToString() {
            return $"X: {X}, Y: {Y}";
        }

        public static explicit operator Point<double>(Point<TUnderlying> instance) {
            return new Point<double>(instance.X.ToDouble(null), instance.Y.ToDouble(null));
        }
    }
}