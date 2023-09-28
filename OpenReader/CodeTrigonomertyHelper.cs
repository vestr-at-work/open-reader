using System.Numerics;

namespace CodeReader {
    public static class TrigonomertyHelper {
        /// <summary>
        /// Gets adjacent angle from three coordinates.
        /// </summary>
        /// <param name="mainVertex"></param>
        /// <param name="secondaryVertexA"></param>
        /// <param name="secondaryVertexB"></param>
        /// <returns>Adjacent angle in radians.</returns>
        public static double GetAdjacentAngle<TUnderlying>(Point<TUnderlying> mainVertex, Point<TUnderlying> secondaryVertexA, Point<TUnderlying> secondaryVertexB) 
            where TUnderlying : INumber<TUnderlying>, IConvertible {
            Vector2 mainToA = new Vector2((float)(secondaryVertexA.X - mainVertex.X).ToDouble(null), (float)(secondaryVertexA.Y - mainVertex.Y).ToDouble(null));
            Vector2 mainToB = new Vector2((float)(secondaryVertexB.X - mainVertex.X).ToDouble(null), (float)(secondaryVertexB.Y - mainVertex.Y).ToDouble(null));

            return Math.Acos((Vector2.Dot(mainToA, mainToB) / (double)(mainToA.Length() * mainToB.Length())));
        }
    }
}