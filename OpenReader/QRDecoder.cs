
namespace CodeReader {
    /// <summary>
    /// Internal class encapsulating methods for decoding QR code.
    /// Main public methods are 'TryGetFormatInfo' and 'TryGetData'.
    /// </summary>
    class QRDecoder {
        /// <summary>
        /// Method for getting the format info (error correction level and data mask) from parsed QR code.
        /// </summary>
        /// <param name="code">Parsed QR code.</param>
        /// <param name="formatInfo">Filled FormatInfo if sucessful, else empty FormatInfo.</param>
        /// <returns>True if successful, false if failed.</returns>
        public static bool TryGetFormatInfo(QRCodeParsed code, out QRFormatInfo formatInfo) {
            ushort mainFormatInfoRawData = GetMainFormatInfoData(code);
            if (TryParseFormatInfo(mainFormatInfoRawData, out QRFormatInfo parsedFormatInfo)) {

                Console.WriteLine($"errorLevel: {parsedFormatInfo.ErrorCorrectionLevel}, dataMask: {parsedFormatInfo.DataMask}");
                formatInfo = parsedFormatInfo;
                return true;
            }

            ushort secondaryFormatInfoRawData = GetSecondaryFormatInfoData(code);
            if (TryParseFormatInfo(mainFormatInfoRawData, out parsedFormatInfo)) {
                formatInfo = parsedFormatInfo;
                return true;
            }
            
            formatInfo = new QRFormatInfo();
            return false;
        }

        public static bool TryGetData(QRCodeParsed codeData, QRFormatInfo formatInfo, out ScanResult result) {
            

            // Dummy implementation
            result = new ScanResult() {Success = true, DataType = ContentType.Text, Data = (object)"Dummy implementation string"};
            return true;
        }

        // Itterates over the QR code symbol in a "zig-zag" (in two columns from right to left) 
        // "snake-like" (two-module column up then two-module column down) fashion and returns modules unmasked.
        // Has to know about QR code parsed data, size, version, data mask.
        private class DataAreaAccesor {
            private QRCodeParsed _code;
            private Predicate<Point> _isPointOnMask;
            private DataAreaChecker _checker;

            public DataAreaAccesor(QRCodeParsed code, QRDataMask dataMask) {
                _code = code;
                _isPointOnMask = MaskDelegateFactory.GetMaskPredicate(dataMask);
                _checker = new DataAreaChecker();
            }

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
                            Point point = new Point(x, y);
                            if (_checker.PointNotInDataArea(point)) {
                                continue;
                            }

                            byte result = _code.Data[point.X, point.Y];

                            // If on mask switch value
                            // TODO: Value 255 as value of white module should be some constant somewhere!!!
                            result = _isPointOnMask(point) ? (byte)(255 - result) : result;

                            yield return result;
                        }
                    }
                }
            }

            /// <summary>
            /// Private class for checking if point in QR code symbol is in data area.
            /// Main public method is 'PointInDataArea'.
            /// </summary>
            private class DataAreaChecker {
                /// <summary>
                /// Checks if point is in QR code symbol data area.
                /// </summary>
                /// <param name="point">Point in the QR code symbol.</param>
                /// <returns>True if coordinates in data area, else returns false.</returns>
                public bool PointInDataArea(Point point) {
                    
                    // Dummy implementation
                    return true;
                }

                /// <summary>
                /// Checks if point is not in QR code symbol data area.
                /// </summary>
                /// <param name="point">Point in the QR code symbol.</param>
                /// <returns>False if coordinates in data area, else returns true.</returns>
                public bool PointNotInDataArea(Point point) {
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
                public static Predicate<Point> GetMaskPredicate(QRDataMask mask) {
                    switch(mask) {
                        case QRDataMask.Mask0:
                            return (Point p) => { return (p.X + p.Y) % 2 == 0; };
                        case QRDataMask.Mask1:
                            return (Point p) => { return (p.Y) % 2 == 0; };
                        case QRDataMask.Mask2:
                            return (Point p) => { return (p.X) % 3 == 0; };
                        case QRDataMask.Mask3:
                            return (Point p) => { return (p.X + p.Y) % 3 == 0; };
                        case QRDataMask.Mask4:
                            return (Point p) => { return ((p.X / (float)3) + (p.Y / (float)2)) % 2 is < 0.0001f and > -0.0001f; };
                        case QRDataMask.Mask5:
                            return (Point p) => { return ((p.X * p.Y) % 3) + ((p.X * p.Y) % 2) == 0; };
                        case QRDataMask.Mask6:
                            return (Point p) => { return (((p.X * p.Y) % 3) + ((p.X * p.Y) % 2)) % 2 == 0; };
                        case QRDataMask.Mask7:
                            return (Point p) => { return (((p.X * p.Y) % 3) + ((p.X + p.Y) % 2)) % 2 == 0; };
                        default:
                            // Can not happen if all masks implemented
                            throw new NotSupportedException();
                    }
                }
            }
        }

        /// <summary>
        /// Class for completing 8-bit words from unmasked module values and returning them by iterator method.
        /// Main public method of the class is 'GetCodewords'.
        /// </summary>
        private class CodewordCompletor {
            private DataAreaAccesor _dataAccesor;

            public CodewordCompletor(QRCodeParsed code, QRDataMask dataMask) {
                _dataAccesor = new DataAreaAccesor(code, dataMask);
            }

            /// <summary>
            /// Iterator method that yields all codewords in order.
            /// </summary>
            /// <returns>Codewords in order one by one.</returns>
            public IEnumerable<byte> GetCodewords() {
                int moduleCount = 0;
                byte nextByte = 0;
                byte resultByte = 0;
                foreach (byte module in _dataAccesor.GetData()) {
                    moduleCount++;

                    // Shift orders and add module value to least significant bit
                    nextByte <<= 1;
                    if (module == 0) {
                        nextByte |= 1;
                    }

                    if (moduleCount == 8) {
                        resultByte = nextByte;
                        nextByte = 0;
                        moduleCount = 0;
                        yield return resultByte;
                    }
                }

                // This should never happen when reading QR code data becouse of the alignment to 8-bit words.
                if (moduleCount > 0) {
                    throw new InvalidDataException("QR code data words not correctly aligned to 8 bits");
                }
            }
        }

        // Takes data codeword and coresponding error codeword and returns corrected data or false or smthng
        private class CodewordErrorCorrector {

        }

        // Has structures for data and error blocks. Gets codewords and places them in the correct block.
        // Has to know error correction level
        private class BlockManager {
            private CodewordCompletor _codewordCompletor;
            private CodewordErrorCorrector _codewordErrorCorrector;

            public BlockManager(QRCodeParsed code, QRFormatInfo formatInfo) {
                _codewordCompletor = new CodewordCompletor(code, formatInfo.DataMask);
                _codewordErrorCorrector = new CodewordErrorCorrector();
            }
        }

        // Takes corrected array of data codewords in order and divides them into segments 
        // according to the mode indicators and character counts.
        private class DataCodewordsSegmentor {

        }

        // Decodes different kinds of data according to the modes.
        private class DataDecoder {

        }

        /// <summary>
        /// Gets the main format info data located around the top left finder pattern.
        /// </summary>
        /// <param name="code">Parsed QR code.</param>
        /// <returns>15 bit format info in the MSb order in ushort type.</returns>
        private static ushort GetMainFormatInfoData(QRCodeParsed code) {
            var accesor = new FormatInfoAccesor(code.Data, code.Size, true);
            return GetFormatInfoAsNumber(accesor);
        }

        /// <summary>
        /// Gets the secondary (aka redundant) format info data located 
        /// under the top right finder pattern and to the right of the bottom left finder pattern.
        /// </summary>
        /// <param name="code">Parsed QR code.</param>
        /// <returns>15 bit format info in the MSb order in ushort type.</returns>
        private static ushort GetSecondaryFormatInfoData(QRCodeParsed code) {
            var accesor = new FormatInfoAccesor(code.Data, code.Size, false);
            return GetFormatInfoAsNumber(accesor);
        }

        private static ushort GetFormatInfoAsNumber(FormatInfoAccesor accesor) {
            ushort result = 0;
            ushort oneAtOrder = 1;
            foreach(var module in accesor.GetFormatModules()) {
                if (module == 0) {
                    result |= oneAtOrder;
                }

                // Move to next order
                oneAtOrder <<= 1;
            }

            return result;
        }

        /// <summary>
        /// Private class for accesing the module values in the format info areas.
        /// Main public method is itterator method 'GetFormatModules'.
        /// </summary>
        private class FormatInfoAccesor {
            /// <summary>
            /// FormatInfoAccesor Ctor.
            /// </summary>
            /// <param name="data">QR code data matrix.</param>
            /// <param name="size">QR code size.</param>
            /// <param name="isMain">Set true if accesor is accesing main format info, set flase if accesing secondary one.</param>
            public FormatInfoAccesor(byte[,] data, int size, bool isMain) {
                _data = data;
                _size = size;
                _isMain = isMain;
            }
            // True if accesor is accesing main format info, flase if accesing secondary one
            private bool _isMain;
            // QR code data matrix
            private byte[,] _data;
            // QR code size
            private int _size;

            /// <summary>
            /// Itterates over format info module values and yield returns them.
            /// </summary>
            /// <returns>Format info module values.</returns>
            public IEnumerable<byte> GetFormatModules() {
                if (_isMain) {
                    int x = 8;
                    int y = 0;
                    while (y < 8) {
                        if (y == 6) {
                            y++;
                            continue;
                        }

                        yield return _data[x, y];
                        y++;
                    }

                    while (x >= 0) {
                        if (x == 6) {
                            x--;
                            continue;
                        }

                        yield return _data[x, y];
                        x--;
                    }
                }
                else {
                    int x = _size - 1;
                    int y = 7;
                    while (x > _size - 1 - 8) {
                        yield return _data[x, y];
                        x--;
                    }

                    x = 8;
                    y = _size - 1 - 7;
                    while  (y < _size) {
                        yield return _data[x, y];
                        y++;
                    }
                }
            }
        }

        private static bool TryParseFormatInfo(ushort rawFormatInfoData, out QRFormatInfo parsedFormatInfo) {
            ushort XorMask = 0b101_0100_0001_0010;
            ushort unmaskedData = (ushort)(rawFormatInfoData ^ XorMask); 

            ushort lowestDistance = ushort.MaxValue;
            ushort? closestValidFormatInfoSequence = null;
            foreach (var validFormatInfoSequence in _validFomatInfoSequences) {
                ushort distanceInBinaryOnes = (ushort)(unmaskedData ^ validFormatInfoSequence);
                ushort distance = GetNumberOfOnesInBinary(distanceInBinaryOnes);

                if (distance < lowestDistance) {
                    lowestDistance = distance;
                    closestValidFormatInfoSequence = validFormatInfoSequence;
                }
            }

            if (lowestDistance > 3 || closestValidFormatInfoSequence is null) {
                parsedFormatInfo = new QRFormatInfo();
                return false;
            }

            // To mask only the bits of interest
            ushort errorCorrectionLevelMask = 0b110_0000_0000_0000;
            ushort dataMaskMask = 0b001_1100_0000_0000;

            QRErrorCorrectionLevel errorCorrectionLevel = (QRErrorCorrectionLevel)((closestValidFormatInfoSequence & errorCorrectionLevelMask) >> 13);
            QRDataMask dataMask = (QRDataMask)((closestValidFormatInfoSequence & dataMaskMask) >> 10);

            parsedFormatInfo = new QRFormatInfo() {ErrorCorrectionLevel = errorCorrectionLevel, DataMask = dataMask};
            return true;
        }


        // Maybe could be generic
        private static ushort GetNumberOfOnesInBinary(ushort number) {
            int ordersCount = sizeof(ushort) * 8;

            ushort oneCount = 0;
            for (int i  = 0; i < ordersCount; i++) {
                if(number % 2 == 1) {
                    oneCount++;
                }
                number /= 2;
            }

            return oneCount;
        }


        /// <summary>
        /// Readonly list of all of the valid format info sequences. Bits in sequences are in MSb order.
        /// </summary>
        /// <value></value>
        private static readonly ushort[] _validFomatInfoSequences = {
            0b000_0000_0000_0000,
            0b000_0101_0011_0111,
            0b000_1010_0110_1110,
            0b000_1111_0101_1001,
            0b001_0001_1110_1011,
            0b001_0100_1101_1100,
            0b001_1011_1000_0101,
            0b001_1110_1011_0010,
            0b010_0011_1101_0110,
            0b010_0110_1110_0001,
            0b010_1001_1011_1000,
            0b010_1100_1000_1111,
            0b011_0010_0011_1101,
            0b011_0111_0000_1010,
            0b011_1000_0101_0011,
            0b011_1101_0110_0100,
            0b100_0010_1001_1011,
            0b100_0111_1010_1100,
            0b100_1000_1111_0101,
            0b100_1101_1100_0010,
            0b101_0011_0111_0000,
            0b101_0110_0100_0111,
            0b101_1001_0001_1110,
            0b101_1100_0010_1001,
            0b110_0001_0100_1101,
            0b110_0100_0111_1010,
            0b110_1011_0010_0011,
            0b110_1110_0001_0100,
            0b111_0000_1010_0110,
            0b111_0101_1001_0001,
            0b111_1010_1100_1000,
            0b111_1111_1111_1111
        };
    }
}