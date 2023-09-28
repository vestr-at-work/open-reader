
namespace CodeReader {
    interface IQRUnmaskedDataProvider {
        public IEnumerable<byte> GetData();
    }
    
    class QRDataAreaAccesor : IQRUnmaskedDataProvider {
        private QRCodeParsed _code;
        private Predicate<Point<int>> _isMaskedPoint;
        private DataAreaChecker _checker;

        public QRDataAreaAccesor(QRCodeParsed code, QRDataMask dataMask) {
            _code = code;
            _isMaskedPoint = MaskDelegateFactory.GetMaskPredicate(dataMask);
            _checker = DataAreaCheckerFactory.GetChecker(code.Size, code.Version);
        }

        /// <summary>
        /// Iterates over the QR code symbol in a "zig-zag" (in two columns from right to left) 
        /// "snake-like" (two-module column up then two-module column down) fashion and returns modules unmasked.
        /// remark: value of the module is converted to byte in the least significant bit (1 if nominaly dark module, 0 if white).
        /// </summary>
        public IEnumerable<byte> GetData() {
            // Setup value so the first iteration is correct
            bool isOddTwoModuleColumn = true;

            // Moving column two modules wide from right to left 
            // and within too moving always from right module to the left
            for (int i = _code.Size - 1; i > 0; i -= 2) {
                isOddTwoModuleColumn = !isOddTwoModuleColumn;
                for (int j = _code.Size - 1; j >= 0; j--) {
                    int y = isOddTwoModuleColumn ? (_code.Size - 1) - j : j;

                    for (int k = 0; k >= -1; k--) {
                        int x = i + k;
                        var point = new Point<int>(x, y);
                        if (_checker.PointNotInDataArea(point)) {
                            continue;
                        }
                        byte result = _code.Data[point.X, point.Y];

                        // If on mask switch value
                        // TODO: Value 255 as value of white module should be some constant somewhere!!!
                        result = _isMaskedPoint(point) ? (byte)(255 - result) : result;

                        yield return result;
                    }
                }
            }
        }

        private static class DataAreaCheckerFactory {
            public static DataAreaChecker GetChecker(int codeSize, QRVersion codeVersion) {
                var functionAreaPoints = new HashSet<Point<int>>();
                var version = codeVersion.value; 
                functionAreaPoints.UnionWith(GetFunctionalPointsCommonForAllVersions(codeSize));

                if (version > 6) {
                    functionAreaPoints.UnionWith(GetVersionInfoPoints(codeSize));
                }

                if (version > 1) {
                    functionAreaPoints.UnionWith(GetAlignmentPatternPoints(version));
                }

                return new DataAreaChecker(functionAreaPoints);
            }

            private static HashSet<Point<int>> GetAlignmentPatternPoints(int version) {
                var alignmentPatternPoints = new HashSet<Point<int>>();

                if (version >= _alignmentPatternCountAndCoordsByVersion.Length) {
                    throw new ArgumentException();
                }

                var alignmentPatternInfo = _alignmentPatternCountAndCoordsByVersion[version];
                int[] patternCoords = alignmentPatternInfo.rowColumnCoordinates;

                for (int l = 0; l < patternCoords.Length; l++) {
                    for (int k = 0; k < patternCoords.Length; k++) {
                        if ((k == 0 && l == 0) || (k == 0 && l == patternCoords.Length - 1) || 
                            (l == 0 && k == patternCoords.Length - 1)) {
                            continue;
                        }

                        int patternCentroidX = patternCoords[k];
                        int patternCentroidY = patternCoords[l];
                        for (int j = -2; j < 3; j++) {
                            for (int i = -2; i < 3; i++) {
                                alignmentPatternPoints.Add(new Point<int>(patternCentroidX + i, patternCentroidY + j));
                            }
                        }
                    }
                }
                
                return alignmentPatternPoints;
            }

            private static HashSet<Point<int>> GetVersionInfoPoints(int codeSize) {
                var versionInfoPoints = new HashSet<Point<int>>();
                int patternSize = 8;
                int versionInfoLongerSide = 6;
                int versionInfoSmallerSide = 3;

                // Top right version info
                for (int j = 0; j < versionInfoLongerSide; j++) {
                    for (int i = codeSize - patternSize - versionInfoSmallerSide; i < codeSize - patternSize; i++) {
                        versionInfoPoints.Add(new Point<int>(i, j));
                    }
                }

                // Bottom left version info
                for (int j = codeSize - patternSize - versionInfoSmallerSide; j < codeSize - patternSize; j++) {
                    for (int i = 0; i < versionInfoLongerSide; i++) {
                        versionInfoPoints.Add(new Point<int>(i, j));
                    }
                }

                return versionInfoPoints;
            }

            private static HashSet<Point<int>> GetFunctionalPointsCommonForAllVersions(int codeSize) {
                var commonFunctionalPoints = new HashSet<Point<int>>();
                int patternSize = 8;

                // Top left pattern and top left main format info
                for (int j = 0; j <= patternSize; j++) {
                    for (int i = 0; i <= patternSize; i++) {
                        commonFunctionalPoints.Add(new Point<int>(i, j));
                    }
                }

                // Top right pattern and top right part of secondary format info
                for (int j = 0; j <= patternSize; j++) {
                    for (int i = codeSize - patternSize; i < codeSize; i++) {
                        commonFunctionalPoints.Add(new Point<int>(i, j));
                    }
                }

                // Bottom left pattern and bottom left part of secondary format info
                for (int j = codeSize - patternSize; j < codeSize; j++) {
                    for (int i = 0; i <= patternSize; i++) {
                        commonFunctionalPoints.Add(new Point<int>(i, j));
                    }
                }

                // Vertical timing pattern
                int x = 6;
                for (int j = patternSize; j < codeSize - patternSize; j++) {
                    commonFunctionalPoints.Add(new Point<int>(x, j));
                }

                // Horizontal timing pattern
                int y = 6;
                for (int i = patternSize; i < codeSize - patternSize; i++) {
                    commonFunctionalPoints.Add(new Point<int>(i, y));
                }

                return commonFunctionalPoints;
            }
        }

        /// <summary>
        /// Private class for checking if point in QR code symbol is in data area.
        /// Main public method is 'PointInDataArea'.
        /// </summary>
        private class DataAreaChecker {
            private HashSet<Point<int>> _functionAreaPoints;

            public DataAreaChecker(HashSet<Point<int>> functionAreaPoints) {
                _functionAreaPoints = functionAreaPoints;
            }

            /// <summary>
            /// Checks if point is in QR code symbol data area.
            /// </summary>
            /// <param name="point">Point in the QR code symbol.</param>
            /// <returns>True if coordinates in data area, else returns false.</returns>
            public bool PointInDataArea(Point<int> point) {
                return !_functionAreaPoints.TryGetValue(point, out Point<int> _);
            }

            /// <summary>
            /// Checks if point is not in QR code symbol data area.
            /// </summary>
            /// <param name="point">Point in the QR code symbol.</param>
            /// <returns>False if coordinates in data area, else returns true.</returns>
            public bool PointNotInDataArea(Point<int> point) {
                return !PointInDataArea(point);
            }
        }

        /// <summary>
        /// Factory class for QR code data mask predicates.
        /// Main public method is 'GetMaskPredicate'.
        /// </summary>
        private static class MaskDelegateFactory {
            /// <summary>
            /// Gets QR code data mask predicate that returns true if point on mask else returns false.
            /// </summary>
            /// <param name="mask">QR code data mask.</param>
            /// <returns></returns>
            public static Predicate<Point<int>> GetMaskPredicate(QRDataMask mask) {
                switch(mask) {
                    case QRDataMask.Mask0:
                        return (Point<int> p) => { return (p.X + p.Y) % 2 == 0; };
                    case QRDataMask.Mask1:
                        return (Point<int> p) => { return (p.Y) % 2 == 0; };
                    case QRDataMask.Mask2:
                        return (Point<int> p) => { return (p.X) % 3 == 0; };
                    case QRDataMask.Mask3:
                        return (Point<int> p) => { return (p.X + p.Y) % 3 == 0; };
                    case QRDataMask.Mask4:
                        return (Point<int> p) => { return ((p.X / (float)3) + (p.Y / (float)2)) % 2 is < 0.0001f and > -0.0001f; };
                    case QRDataMask.Mask5:
                        return (Point<int> p) => { return ((p.X * p.Y) % 3) + ((p.X * p.Y) % 2) == 0; };
                    case QRDataMask.Mask6:
                        return (Point<int> p) => { return (((p.X * p.Y) % 3) + ((p.X * p.Y) % 2)) % 2 == 0; };
                    case QRDataMask.Mask7:
                        return (Point<int> p) => { return (((p.X * p.Y) % 3) + ((p.X + p.Y) % 2)) % 2 == 0; };
                    default:
                        // Can not happen if all masks implemented
                        throw new NotSupportedException();
                }
            }
        }

        private static readonly (int count, int[] rowColumnCoordinates)[] _alignmentPatternCountAndCoordsByVersion = {
            // Version 0 does not exist
            (0, new int[0]),
            // Version 1 does not have alignment patterns
            (0, new int[0]),
            // Version 2
            (1, new int[] {6, 18}),
            // Version 3
            (1, new int[] {6, 22}),
            // Version 4
            (1, new int[] {6, 26}),
            // Version 5
            (1, new int[] {6, 30}),
            // Version 6
            (1, new int[] {6, 34}),
            // Version 7
            (6, new int[] {6, 22, 38}),
            // Version 8
            (6, new int[] {6, 24, 42}),
            // Version 9
            (6, new int[] {6, 26, 46}),
            // Version 10
            (6, new int[] {6, 28, 50}),
            // Version 11
            (6, new int[] {6, 30, 54}),
            // Version 12
            (6, new int[] {6, 32, 58}),
            // Version 13
            (6, new int[] {6, 34, 62}),
            // Version 14
            (13, new int[] {6, 26, 46, 66}),
            // Version 15
            (13, new int[] {6, 26, 48, 70}),
            // Version 16
            (13, new int[] {6, 26, 50, 74}),
            // Version 17
            (13, new int[] {6, 30, 52, 78}),
            // Version 18
            (13, new int[] {6, 30, 54, 82}),
            // Version 19
            (13, new int[] {6, 30, 56, 86}),
            // Version 20
            (13, new int[] {6, 33, 58, 90}),
            
            // TODO: Complete the data to version 40!!!!
        };
    }
}