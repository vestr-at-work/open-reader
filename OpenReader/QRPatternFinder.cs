
namespace CodeReader {
    interface IQRPatternFinder {
        public bool TryGetFinderPatterns(Image<L8> image, out QRFinderPatternTrio patternTrio);
        public bool TryGetAlignmentPattern(Image<L8> image, Rectangle alignmentNeighborhood, out Point<int> alignmentPatternCentroid);
        public Point<int> GetApproximateAlignmentPatternCentroid(QRFinderPatternTrio finderPatterns);
    }
    

    /// <summary>
    /// Class for finding and recognizing QR code patterns.
    /// </summary>
    class QRPatternFinder : IQRPatternFinder {
        public bool TryGetFinderPatterns(Image<L8> image, out QRFinderPatternTrio patternTrio) {
            List<QRFinderPattern> finderPatterns = GetPotentialFinderPatterns(image);

            if (finderPatterns.Count < 3) {
                // Empty assingment
                patternTrio = new QRFinderPatternTrio();
                return false;
            }

            if (!TryGetFinderPatternTrio(finderPatterns, out QRFinderPatternTrioNotDetermined patternTrioNotDetermined)) {
                // Empty assingment
                patternTrio = new QRFinderPatternTrio();
                return false;
            }

            patternTrio = DetermineFinderPatternRelations(patternTrioNotDetermined);
            return true;
        }


        public bool TryGetAlignmentPattern(Image<L8> image, Rectangle alignmentNeighborhood, out Point<int> alignmentPatternCentroid) {
            Image<L8> subimage = image.Clone(i => i.Crop(alignmentNeighborhood));
            var extractor = new AlignmentPatternExtractor(subimage);
            subimage.Save("../DebugImages/QRCodeTestAlignmentOUTPUT.png");
            subimage.Dispose();

            if (!extractor.TryGetPattern(out Point<int> localAlignmentPatternCentroid)) {
                alignmentPatternCentroid = new Point<int>();
                return false;
            }

            alignmentPatternCentroid = new Point<int>(localAlignmentPatternCentroid.X + alignmentNeighborhood.X, localAlignmentPatternCentroid.Y + alignmentNeighborhood.Y);
            return true;
        }

        public Point<int> GetApproximateAlignmentPatternCentroid(QRFinderPatternTrio finderPatterns) {
            double dx1 = finderPatterns.TopRightPattern.Centroid.X - finderPatterns.TopLeftPattern.Centroid.X;
            double dy1 = finderPatterns.TopRightPattern.Centroid.Y - finderPatterns.TopLeftPattern.Centroid.Y;
            double dx2 = finderPatterns.BottomLeftPattern.Centroid.X - finderPatterns.TopLeftPattern.Centroid.X;
            double dy2 = finderPatterns.BottomLeftPattern.Centroid.Y - finderPatterns.TopLeftPattern.Centroid.Y;

            (double x, double y) vector1 = (dx1, dy1);
            (double x, double y) vector2 = (dx2, dy2);

            double angleNumerator = vector1.x * vector2.x + vector1.y * vector2.y;
            double vector1Length = Math.Sqrt(vector1.x * vector1.x + vector1.y * vector1.y);
            double vector2Length = Math.Sqrt(vector2.x * vector2.x + vector2.y * vector2.y);
            double angleDenominator = vector1Length * vector2Length;
            double angleBetweenTwoSides = Math.Acos(angleNumerator / angleDenominator);

            
            var rotationAngle = TrigonomertyHelper.GetAdjacentAngle(
                finderPatterns.TopLeftPattern.Centroid, 
                new Point<int>(finderPatterns.TopLeftPattern.Centroid.X + (finderPatterns.TopLeftPattern.EstimatedWidth / 2), 
                    finderPatterns.TopLeftPattern.Centroid.Y), 
                finderPatterns.TopRightPattern.Centroid);

            var alignmentVectorAngle = (angleBetweenTwoSides / 2) + rotationAngle;

            // Asignment just for compiler, always should be overwritten
            (double x, double y) alignmentUnitVector = (0,0);
            var topLeftCentroid = finderPatterns.TopLeftPattern.Centroid;
            var topRightCentroid = finderPatterns.TopRightPattern.Centroid;
            if ((topLeftCentroid.X <= topRightCentroid.X && topLeftCentroid.Y <= topRightCentroid.Y) ||
                (topLeftCentroid.X >= topRightCentroid.X && topLeftCentroid.Y <= topRightCentroid.Y)) {

                alignmentUnitVector = (Math.Cos(alignmentVectorAngle), Math.Sin(alignmentVectorAngle));
            }
            else if ((topLeftCentroid.X <= topRightCentroid.X && topLeftCentroid.Y >= topRightCentroid.Y) ||
                (topLeftCentroid.X >= topRightCentroid.X && topLeftCentroid.Y >= topRightCentroid.Y)) {

                alignmentUnitVector = (Math.Sin(alignmentVectorAngle), Math.Cos(alignmentVectorAngle));
            }
            
            double meanVectorLength = (vector1Length + vector2Length) / 2;
            int patternRadius = finderPatterns.TopLeftPattern.EstimatedWidth / 2;
            int pointX = Convert.ToInt32(finderPatterns.TopLeftPattern.Centroid.X + (alignmentUnitVector.x * (Math.Sqrt(2) * (meanVectorLength - patternRadius))));
            int pointY = Convert.ToInt32(finderPatterns.TopLeftPattern.Centroid.Y + (alignmentUnitVector.y * (Math.Sqrt(2) * (meanVectorLength - patternRadius))));

            Point<int> alignmentPattern = new Point<int>(pointX, pointY);
            return alignmentPattern;
        }

        private class AlignmentPatternExtractor {
            private byte[,] _subimageMatrix;
            private int _size;
            private int[,] _whiteComponentMatrix;
            private int[,] _blackComponentMatrix;
            private List<List<Point<int>>> _neighborsToBlackComponents = new List<List<Point<int>>>();
            private List<List<Point<int>>> _neighborsToWhiteComponents = new List<List<Point<int>>>();
            private int _whiteComponentCount = 0;
            private List<List<Point<int>>> _blackComponentPoints = new List<List<Point<int>>>();

            public AlignmentPatternExtractor(Image<L8> subimage) {
                _subimageMatrix = GetMatrixFromImage(subimage);

                _size = _subimageMatrix.GetLength(0);
                _whiteComponentMatrix = new int[_size, _size];
                _blackComponentMatrix = new int[_size, _size];
                InicializeComponentMatrices(_size, -1);
            }

            public bool TryGetPattern(out Point<int> patternCentroid) {
                CalculateWhiteComponents();
                CalculateBlackComponents();

                if (!TryGetAlignmentPatternCenterComponent(out int componentNumber)) {
                    patternCentroid = new Point<int>();
                    return false;
                }

                // Calculate centroid coords and return
                patternCentroid = GetBlackComponentCentroid(componentNumber);
                return true;
            }

            private void InicializeComponentMatrices(int size, int value) {
                for (int j = 0; j < size; j++) {
                    for (int i = 0; i < size; i++) {
                        _whiteComponentMatrix[i, j] = value;
                        _blackComponentMatrix[i, j] = value;
                    }
                }
            }

            private Point<int> GetBlackComponentCentroid(int componentNumber) {
                int sumX = 0;
                int sumY = 0;
                foreach (var point in _blackComponentPoints[componentNumber]) {
                    sumX += point.X;
                    sumY += point.Y;
                }

                double meanX = sumX / (double)_blackComponentPoints[componentNumber].Count;
                double meanY = sumY / (double)_blackComponentPoints[componentNumber].Count;

                return new Point<int>(Convert.ToInt32(meanX), Convert.ToInt32(meanY));
            }

            private bool TryGetAlignmentPatternCenterComponent(out int componentNumber) {
                for (int i = 0; i < _neighborsToBlackComponents.Count; i++) {
                    List<int> whiteNeighborComponents = GetComponentNumbers(_neighborsToBlackComponents[i], _whiteComponentMatrix, out bool outsideBounds);

                    if (whiteNeighborComponents.Count != 1 || outsideBounds) {
                        continue;
                    }

                    int whiteNeighborComponentIndex = whiteNeighborComponents[0];
                    List<int> blackNeighborsToWhiteComponent = GetComponentNumbers(_neighborsToWhiteComponents[whiteNeighborComponentIndex], _blackComponentMatrix, out _);

                    // If only one more black component is a neighbor
                    if (blackNeighborsToWhiteComponent.Count == 2) {
                        componentNumber = i;
                        return true;
                    }
                }

                componentNumber = 0;
                return false;
            }

            private List<int> GetComponentNumbers(List<Point<int>> points, int[,] componentMatrix, out bool outsideBounds) {
                var components = new List<int>();
                outsideBounds = false;
                foreach (var point in points) {
                    if (IsOutsideBoundary(point.X, point.Y)) {
                        outsideBounds = true;
                        continue;
                    }

                    int componentNumber = componentMatrix[point.X, point.Y];
                    if (components.Contains(componentNumber)) {
                        continue;
                    }

                    components.Add(componentNumber);
                }

                return components;
            }


            private void CalculateWhiteComponents() {
                for (int j = 0; j < _size; j++) {
                    for (int i = 0; i < _size; i++) {
                        if (_subimageMatrix[i, j] == 255 && _whiteComponentMatrix[i, j] == -1) {
                            Point<int> entryPoint = new Point<int>(i, j);
                            WhiteComponentDFS(entryPoint, _whiteComponentCount++);
                        }
                    }
                }
            }

            private void CalculateBlackComponents() {
                for (int j = 0; j < _size; j++) {
                    for (int i = 0; i < _size; i++) {
                        if (_subimageMatrix[i, j] == 0 && _blackComponentMatrix[i, j] == -1) {
                            int newComponentNumber = _blackComponentPoints.Count;
                            Point<int> entryPoint = new Point<int>(i, j);
                            _blackComponentPoints.Add(new List<Point<int>>() {entryPoint});
                            BlackComponentDFS(entryPoint, newComponentNumber);
                        }
                    }
                }
            }

            private void WhiteComponentDFS(Point<int> newPoint, int componentNumber) {
                _whiteComponentMatrix[newPoint.X, newPoint.Y] = componentNumber;

                for (int j = -1; j < 2; j++) {
                    for (int i = -1; i < 2; i++) {
                        var neighbor = new Point<int>(newPoint.X + i, newPoint.Y + j);
                        if (IsOutsideBoundary(neighbor.X, neighbor.Y) || _subimageMatrix[neighbor.X, neighbor.Y] == 0) {
                            AddNeighborToList(_neighborsToWhiteComponents, componentNumber, neighbor);
                            continue;
                        }

                        if (_subimageMatrix[neighbor.X, neighbor.Y] == 255 && _whiteComponentMatrix[neighbor.X, neighbor.Y] == -1) {  
                            WhiteComponentDFS(neighbor, componentNumber);
                        }
                    }
                }
            }

            private void BlackComponentDFS(Point<int> newPoint, int componentNumber) {
                _blackComponentMatrix[newPoint.X, newPoint.Y] = componentNumber;

                for (int j = -1; j < 2; j++) {
                    for (int i = -1; i < 2; i++) {
                        var neighbor = new Point<int>(newPoint.X + i, newPoint.Y + j);
                        if (IsOutsideBoundary(neighbor.X, neighbor.Y) || _subimageMatrix[neighbor.X, neighbor.Y] == 255) {
                            AddNeighborToList(_neighborsToBlackComponents, componentNumber, neighbor);
                            continue;
                        }

                        if (_subimageMatrix[neighbor.X, neighbor.Y] == 0 && _blackComponentMatrix[neighbor.X, neighbor.Y] == -1) {
                            _blackComponentPoints[componentNumber].Add(neighbor);
                            BlackComponentDFS(neighbor, componentNumber);
                        }
                    }
                }
            }

            private void AddNeighborToList(List<List<Point<int>>> componentNeighbors, int componentNumber, Point<int> neighbor) {
                if (componentNeighbors.Count <= componentNumber) {
                    componentNeighbors.Add(new List<Point<int>>() {neighbor});
                }
                else {
                    componentNeighbors[componentNumber].Add(neighbor);
                }
            }

            private bool IsOutsideBoundary(int x, int y) {
                return x < 0 || x >= _size || y < 0 || y >= _size;
            }

            private byte[,] GetMatrixFromImage(Image<L8> image) {
                byte[,] matrix = new byte[image.Width, image.Height];

                image.ProcessPixelRows(accessor => {
                    for (int y = 0; y < accessor.Height; y++) {
                        Span<L8> pixelRow = accessor.GetRowSpan(y);

                        for (int x = 0; x < pixelRow.Length; x++) {
                            byte pixelValue = pixelRow[x].PackedValue;
                            matrix[x, y] = pixelValue;
                        }
                    }
                });

                return matrix;
            } 
        }

        private struct ColorBlock {
            public ColorBlock(int start, int end) {
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
        /// 
        /// TODO: Explain better the public methods and sketch out the algorithm better.
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
            private ColorBlock?[] _columnBlocks = new ColorBlock?[5];
            private bool _currentColumnBlockUpIsWhite = false;
            private bool _currentColumnBlockDownIsWhite = false;
            private int _currentColumnBlockStart;
            private bool _isColumnInfoDisposed = true;


            // Finder pattern row color blocs ordered from left to right
            private ColorBlock?[] _rowBlocks = new ColorBlock?[5];
            private int _row;
            private int _currentRowBlockStart;
            private bool? _currentRowBlockIsWhite;

            enum Block { 
                FirstBlack = 0,
                FirstWhite = 1,
                MiddleBlack = 2,
                LastWhite = 3,
                LastBlack = 4

            }

            public int GetPatternWidth() {
                return (((ColorBlock)_rowBlocks[(int)Block.LastBlack]!).EndIndex - ((ColorBlock)_rowBlocks[(int)Block.FirstBlack]!).StartIndex);
            }

            public int GetPatternHeight() {
                return (((ColorBlock)_columnBlocks[(int)Block.LastBlack]!).EndIndex - ((ColorBlock)_columnBlocks[(int)Block.FirstBlack]!).StartIndex);
            }

            public int GetMiddleOfRowBlocks() {
                return (((ColorBlock)_rowBlocks[(int)Block.FirstBlack]!).StartIndex + ((ColorBlock)_rowBlocks[(int)Block.LastBlack]!).EndIndex) / 2;
            }

            /// <summary>
            /// Gets middle 'y' coordinate of column blocs. Call only if IsFinderPattern = true!
            /// </summary>
            /// <returns>Middle 'y' coordinate of column blocs.</returns>
            public int GetMiddleOfColumnBlocks() {
                return (((ColorBlock)_columnBlocks[(int)Block.FirstBlack]!).StartIndex + ((ColorBlock)_columnBlocks[(int)Block.LastBlack]!).EndIndex) / 2;
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
                if (_rowBlocks[(int)Block.LastBlack] is null && _currentRowBlockIsWhite is null) {
                    _currentRowBlockStart = column;
                    _currentRowBlockIsWhite = true;
                    return;
                }
                if (_currentRowBlockIsWhite is true) {
                    return;
                }

                // Move black blocs one to the left
                _rowBlocks[(int)Block.FirstBlack] = _rowBlocks[(int)Block.MiddleBlack];
                _rowBlocks[(int)Block.MiddleBlack] = _rowBlocks[(int)Block.LastBlack];
                _rowBlocks[(int)Block.LastBlack] = new ColorBlock(_currentRowBlockStart, column - 1);

                _currentRowBlockStart = column;
                _currentRowBlockIsWhite = true;

                IsPossibleFinderPattern = IsBlockRatioCorrect(_rowBlocks);
            }

            private void AddNextPixelBlack(int column) {
                // If image row begins with black
                if (_rowBlocks[(int)Block.LastWhite] is null && _currentRowBlockIsWhite is null) {
                    _currentRowBlockStart = column;
                    _currentRowBlockIsWhite = false;
                    return;
                }
                if (_currentRowBlockIsWhite is false) {
                    return;
                }

                // Move white blocs one to the left
                _rowBlocks[(int)Block.FirstWhite] = _rowBlocks[(int)Block.LastWhite];
                _rowBlocks[(int)Block.LastWhite] = new ColorBlock(_currentRowBlockStart, column - 1);

                _currentRowBlockStart = column;
                _currentRowBlockIsWhite = false;
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
                if (_columnBlocks[(int)Block.FirstWhite] is null && !_currentColumnBlockUpIsWhite) {
                    return;
                }
                if (!_currentColumnBlockUpIsWhite) {
                    return;
                }

                // Set top white bloc
                _columnBlocks[(int)Block.FirstWhite] = new ColorBlock(row + 1, _currentColumnBlockStart);

                _currentColumnBlockStart = row;
                _currentColumnBlockUpIsWhite = false;
            }

            private void AddNextColumnPixelUpWhite(int row) {
                if (_currentColumnBlockUpIsWhite) {
                    return;
                }
                if (_columnBlocks[(int)Block.MiddleBlack] is null) {
                    int invalid = Int32.MinValue;
                    _columnBlocks[(int)Block.MiddleBlack] = new ColorBlock(row + 1, invalid);

                    _currentColumnBlockStart = row;
                    _currentColumnBlockUpIsWhite = true;
                    return;
                }

                // Finish off top black bloc
                _columnBlocks[(int)Block.FirstBlack] = new ColorBlock(row + 1, _currentColumnBlockStart);
                NeedNextUpPixelVertical = false;
            }

            public void AddNextColumnPixelDown(byte pixelValue, int row) {
                _isColumnInfoDisposed = false;
                if (NeedNextUpPixelVertical) {
                    return;
                }

                if (pixelValue == (byte)255) {
                    AddNextColumnPixelDownWhite(row);
                    return;
                }
                AddNextColumnPixelDownBlack(row);
            }

            private void AddNextColumnPixelDownBlack(int row) {
                if (!_currentColumnBlockDownIsWhite) {
                    return;
                }

                _columnBlocks[(int)Block.LastWhite] = new ColorBlock(_currentColumnBlockStart, row - 1);

                _currentColumnBlockStart = row;
                _currentColumnBlockDownIsWhite = false;
            }

            private void AddNextColumnPixelDownWhite(int row) {
                if (_currentColumnBlockDownIsWhite) {
                    return;
                }
                // Bottom white bloc
                if (!_currentColumnBlockDownIsWhite && _columnBlocks[(int)Block.LastWhite] is null) {
                    _columnBlocks[(int)Block.MiddleBlack] = 
                        new ColorBlock(((ColorBlock)_columnBlocks[(int)Block.MiddleBlack]!).StartIndex, row - 1);

                    _currentColumnBlockDownIsWhite = true;
                    _currentColumnBlockStart = row;
                    return;
                }

                _columnBlocks[(int)Block.LastBlack] = new ColorBlock(_currentColumnBlockStart, row - 1);
                NeedNextDownPixelVertical = false;

                IsFinderPattern = IsBlockRatioCorrect(_columnBlocks);
            }

            private bool IsBlockRatioCorrect(ColorBlock?[] blocks) {
                const float smallBlocRatio = 1/7f;
                const float bigBlocRatio = 3/7f;
                float errorMarginSmall = (smallBlocRatio / 100f) * 40;
                float errorMarginBig = (bigBlocRatio / 100f) * 40;

                float allBlocksLength = 0;
                foreach (var block in blocks) {
                    if (block is null) {
                        return false;
                    }

                    allBlocksLength += ((ColorBlock)block).Length;
                }


                float blackBlockFirstRatio = ((ColorBlock)blocks[(int)Block.FirstBlack]!).Length / allBlocksLength;
                if (BlockRatioOutsideErrorMargin(blackBlockFirstRatio, smallBlocRatio, errorMarginSmall)) {
                    return false;
                }
                float whiteBlockFirstRatio = ((ColorBlock)blocks[(int)Block.FirstWhite]!).Length / allBlocksLength;
                if (BlockRatioOutsideErrorMargin(whiteBlockFirstRatio, desiredBlockRatio: smallBlocRatio, errorMarginSmall)) {
                    return false;
                }
                float blackBlockMiddleRatio = ((ColorBlock)blocks[(int)Block.MiddleBlack]!).Length / allBlocksLength;
                if (BlockRatioOutsideErrorMargin(blackBlockMiddleRatio, desiredBlockRatio: bigBlocRatio, errorMarginBig)) {
                    return false;
                }
                float whiteBlockLastRatio = ((ColorBlock)blocks[(int)Block.LastWhite]!).Length / allBlocksLength;
                if (BlockRatioOutsideErrorMargin(whiteBlockLastRatio, desiredBlockRatio: smallBlocRatio, errorMarginSmall)) {
                    return false;
                }
                float blackBlockLastRatio = ((ColorBlock)blocks[(int)Block.LastBlack]!).Length / allBlocksLength;
                if (BlockRatioOutsideErrorMargin(blackBlockLastRatio, desiredBlockRatio: smallBlocRatio, errorMarginSmall)) {
                    return false;
                }

                return true;
            }

            bool BlockRatioOutsideErrorMargin(float blockRatio, float desiredBlockRatio, float errorMargin) {
                return (blockRatio < desiredBlockRatio - errorMargin || 
                        blockRatio > desiredBlockRatio + errorMargin);
            }

            private void DisposeColumnInfo() {
                IsFinderPattern = false;
                _columnBlocks = new ColorBlock?[5];
                _currentColumnBlockUpIsWhite = false;
                _isColumnInfoDisposed = true;
                NeedNextDownPixelVertical = true;
                NeedNextUpPixelVertical = true;
            }
        }

    
        private static List<QRFinderPattern> GetPotentialFinderPatterns(Image<L8> image) {
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

                    // Check vertical ---- This section could be a function

                    int centerOfMiddleBloc = finderExtractor.GetMiddleOfRowBlocks();
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

                        var centroid = new Point<int>(centerOfMiddleBloc + 1, finderExtractor.GetMiddleOfColumnBlocks());
                        var pattern = new QRFinderPattern(centroid, finderExtractor.GetPatternWidth(), finderExtractor.GetPatternHeight());
                        finderPatternPixels.Add(pattern);
                    }
                    
                }
            }

            return finderPatternPixels;
        }

        private static bool TryGetFinderPatternTrio(List<QRFinderPattern> patterns, out QRFinderPatternTrioNotDetermined patternTrio) {
            var clusters = GetClustersBasedOnDistance(patterns, 5);
            if (clusters.Count < 3) {
                // empty asignment
                patternTrio = new QRFinderPatternTrioNotDetermined();
                return false;
            }

            patternTrio = GetFinderPatternTrioFromClusters(clusters);
            return true;
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

        private static QRFinderPatternTrioNotDetermined GetFinderPatternTrioFromClusters(List<List<QRFinderPattern>> clusters) {
            // Sort in descending order based on number of patterns in cluster
            clusters.Sort(delegate(List<QRFinderPattern> clusterA, List<QRFinderPattern> clusterB) {
                return clusterB.Count.CompareTo(clusterA.Count);
            });

            var finderPatternTrio = new QRFinderPattern[3];
            for (int i = 0; i < 3; i++) {
                int count = 0;
                int sumX = 0;
                int sumY = 0;
                int sumWidth = 0;
                int sumHeight = 0;
                foreach (var pattern in clusters[i]) {
                    count++;
                    sumX += pattern.Centroid.X;
                    sumY += pattern.Centroid.Y;
                    sumWidth += pattern.EstimatedWidth;
                    sumHeight += pattern.EstimatedHeight;
                }

                var averageCentroid = new Point<int>(sumX / count, sumY / count);
                var clusterAveragePattern = new QRFinderPattern(averageCentroid, sumWidth / count, sumHeight / count);
                finderPatternTrio[i] = (clusterAveragePattern);   
            }

            return new QRFinderPatternTrioNotDetermined(finderPatternTrio[0], finderPatternTrio[1], finderPatternTrio[2]);
        }

        /// <summary>
        /// Method uses angles between pattern centroids to determine patterns relation and position.
        /// </summary>
        /// <param name="patterns"></param>
        /// <returns></returns>
        private static QRFinderPatternTrio DetermineFinderPatternRelations(QRFinderPatternTrioNotDetermined patternTrio) {
            var angleByPattern1 = TrigonomertyHelper.GetAdjacentAngle(patternTrio.Pattern1.Centroid, patternTrio.Pattern2.Centroid, patternTrio.Pattern3.Centroid);
            var angleByPattern2 = TrigonomertyHelper.GetAdjacentAngle(patternTrio.Pattern2.Centroid, patternTrio.Pattern1.Centroid, patternTrio.Pattern3.Centroid);
            var angleByPattern3 = TrigonomertyHelper.GetAdjacentAngle(patternTrio.Pattern3.Centroid, patternTrio.Pattern1.Centroid, patternTrio.Pattern2.Centroid);

            var maxAngle = Math.Max(Math.Max(angleByPattern1, angleByPattern2), angleByPattern3);

            // TODO: ADD CHECKING IF ANGLEs ARE WITHIN ERROR MARGIN

            // Maximal angle is by the upper left pattern. Now we determine the other two patterns.
            if (maxAngle == angleByPattern1) {
                return GetFinalQRFinderPatterns(patternTrio.Pattern1, patternTrio.Pattern2, patternTrio.Pattern3);
            }
            else if (maxAngle == angleByPattern2) {
                return GetFinalQRFinderPatterns(patternTrio.Pattern2, patternTrio.Pattern1, patternTrio.Pattern3);
            }
            else if (maxAngle == angleByPattern3) {
                return GetFinalQRFinderPatterns(patternTrio.Pattern3, patternTrio.Pattern1, patternTrio.Pattern2);
            }

            // Unreachable code
            return new QRFinderPatternTrio();
        }

        private static QRFinderPatternTrio GetFinalQRFinderPatterns(QRFinderPattern upperLeft, QRFinderPattern otherPatternA, QRFinderPattern otherPatternB) {
            var crossProduct = ((otherPatternA.Centroid.X - upperLeft.Centroid.X) 
                            * (otherPatternB.Centroid.Y - upperLeft.Centroid.Y))
                            - ((otherPatternA.Centroid.Y - upperLeft.Centroid.Y) 
                            * (otherPatternB.Centroid.X - upperLeft.Centroid.X));

            if (crossProduct > 0) {
                return new QRFinderPatternTrio(upperLeft, otherPatternA, otherPatternB);
            }

            return new QRFinderPatternTrio(upperLeft, otherPatternB, otherPatternA);

        }
    } 
}