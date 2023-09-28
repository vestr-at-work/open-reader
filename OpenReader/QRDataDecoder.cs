
using System.Text;
using MathNet.Numerics;
using STH1123.ReedSolomon;

namespace CodeReader {

    interface IQRDataDecoder {
        public bool TryGetData(QRCodeParsed codeData, QRFormatInfo formatInfo, out List<DecodedData> decodedData);
    }

    /// <summary>
    /// Class encapsulating methods for decoding QR code data.
    /// Main public method is 'TryGetData'.
    /// </summary>
    class QRDataDecoder : IQRDataDecoder{
        public bool TryGetData(QRCodeParsed codeData, QRFormatInfo formatInfo, out List<DecodedData> decodedData) {
            IQRUnmaskedDataProvider dataProvider = new DataAreaAccesor(codeData, formatInfo.DataMask);
            IQRRawCodewordProvider codewordProvider = new CodewordCompletor(dataProvider);
            IBlockErrorCorrector codewordCorrector = new ReedSolomonErrorCorrector(GenericGF.QR_CODE_FIELD_256);

            var codewordManager = new CodewordManager(codeData.Version, formatInfo.ErrorCorrectionLevel, codewordProvider, codewordCorrector);
            if (!codewordManager.TryGetDataCodewords(out byte[] dataCodewords)) {
                decodedData = new List<DecodedData>();
                return false;
            }

            if (!DataCodewordSegmenter.TrySegmentByMode(codeData.Version, dataCodewords, out List<DataSegment> dataSegments)) {
                decodedData = new List<DecodedData>();
                return false;
            }

            decodedData = new List<DecodedData>();

            foreach (var segment in dataSegments) {
                if (!DataDecoder.TryDecode(segment, out DecodedData data)) {
                    decodedData = new List<DecodedData>();
                    return false;
                }

                decodedData.Add(data);
            }

            return true;
        }

        public interface IQRUnmaskedDataProvider {
            public IEnumerable<byte> GetData();
        }
        
        public interface IQRRawCodewordProvider {
            public IEnumerable<byte> GetCodewords();
        }

        private class DataAreaAccesor : IQRUnmaskedDataProvider {
            private QRCodeParsed _code;
            private Predicate<Point<int>> _isMaskedPoint;
            private DataAreaChecker _checker;

            public DataAreaAccesor(QRCodeParsed code, QRDataMask dataMask) {
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
                        throw new InvalidParameterException(version);
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
        }

        /// <summary>
        /// Class for completing 8-bit words from unmasked module values and returning them by iterator method.
        /// Main public method of the class is 'GetCodewords'.
        /// </summary>
        private class CodewordCompletor : IQRRawCodewordProvider {
            private IQRUnmaskedDataProvider _dataProvider;

            private static int _byteSize = 8;

            public CodewordCompletor(IQRUnmaskedDataProvider dataProvider) {
                _dataProvider = dataProvider;
            }

            /// <summary>
            /// Iterator method that yields all codewords in order.
            /// </summary>
            /// <returns>Codewords in order one by one.</returns>
            public IEnumerable<byte> GetCodewords() {
                int moduleCount = 0;
                byte nextByte = 0;
                byte resultByte;
                foreach (byte module in _dataProvider.GetData()) {
                    moduleCount++;

                    // Shift orders and add module value to least significant bit
                    nextByte <<= 1;
                    if (module == 0) {
                        nextByte |= 1;
                    }

                    if (moduleCount == _byteSize) {
                        resultByte = nextByte;
                        nextByte = 0;
                        moduleCount = 0;
                        yield return resultByte;
                    }
                }

                if (moduleCount > 0) {
                    yield return (byte)(nextByte << (_byteSize - moduleCount));
                }
            }
        }

        public interface IBlockErrorCorrector {
            public bool TryCorrectBlock(int[] dataBlock, int[] errorCorrectionBlock, out int[] correctedDataBlock);
        }

        // Takes data codeword and coresponding error codeword and returns corrected data or false or smthng
        private class ReedSolomonErrorCorrector : IBlockErrorCorrector {
            private GenericGF _galoisField;
            private ReedSolomonDecoder _decoder;

            public ReedSolomonErrorCorrector(GenericGF galoisField) {
                _galoisField = galoisField;
                _decoder = new ReedSolomonDecoder(_galoisField);
            }

            public bool TryCorrectBlock(int[] dataBlock, int[] errorCorrectionBlock, out int[] correctedDataBlock) {
                int[] dataAndErrorCorrectionCodewords = dataBlock.Concat(errorCorrectionBlock).ToArray();

                if (!_decoder.Decode(dataAndErrorCorrectionCodewords, errorCorrectionBlock.Length)) {
                    correctedDataBlock = dataBlock;
                    return false;
                }

                // Extract corrected data
                correctedDataBlock = dataAndErrorCorrectionCodewords.Take(dataBlock.Length).ToArray();
                return true;
            }
        }

        private class CodewordManager {
            private IQRRawCodewordProvider _codewordProvider;
            private IBlockErrorCorrector _codewordErrorCorrector;
            // private int _codeVersion;
            // private QRErrorCorrectionLevel _codeErrorCorrectionLevel;
            private List<byte[]> _dataBlocks;
            private int _dataBlockLengthSum = 0;
            private int _shorterDataBlockCount;
            private List<byte[]> _errorCorrectionBlocks;
            private int _errorCorrectionBlockLengthSum = 0;
            private bool _blocksFilled = false;


            public CodewordManager(QRVersion codeVersion, QRErrorCorrectionLevel errorCorrectionLevel, 
                                    IQRRawCodewordProvider codewordProvider, IBlockErrorCorrector errorCorrector) {

                _codewordProvider = codewordProvider;
                _codewordErrorCorrector = errorCorrector;
                _dataBlocks = InicializeDataBlocks(codeVersion, errorCorrectionLevel);
                _shorterDataBlockCount = GetShorterDataBlockCount();
                _errorCorrectionBlocks = InicializeErrorCorrectionBlocks(codeVersion, errorCorrectionLevel);
            }

            /// <summary>
            /// Tries to get error corrected and sorted data codewords.
            /// </summary>
            /// <param name="result">Data codewords if successful, otherwise empty asignment</param>
            /// <returns>True if successful, otherwise false.</returns>
            public bool TryGetDataCodewords(out byte[] result) {
                FillBlocksWithCodewords();
                
                if (!TryCorrectErrors()) {
                    result = new byte[0];
                    return false;
                }

                result = GetDataCodewordsInOrder();
                return true;
            }

            private int GetShorterDataBlockCount() {
                int baseLength = _dataBlocks[0].Length;
                
                int count = 0;
                for (int i = 0; i < _dataBlocks.Count; i++) {
                    var length = _dataBlocks[i].Length;
                    if (length > baseLength) {
                        break;
                    }
                    count += 1;
                }

                return count;
            }

            private List<byte[]> InicializeDataBlocks(QRVersion version, QRErrorCorrectionLevel errorCorrectionLevel) {
                int[] blockLengths = QRBlockInfo.GetDataBlockLengths(version, errorCorrectionLevel);
                var blocks = new List<byte[]>();

                foreach (var length in blockLengths) {
                    _dataBlockLengthSum += length;
                    blocks.Add(new byte[length]);
                }

                return blocks;
            }

            private List<byte[]> InicializeErrorCorrectionBlocks(QRVersion version, QRErrorCorrectionLevel errorCorrectionLevel) {
                int[] blockLengths = QRBlockInfo.GetErrorCorrectionBlockLengths(version, errorCorrectionLevel);
                var blocks = new List<byte[]>();

                foreach (var length in blockLengths) {
                    _errorCorrectionBlockLengthSum += length;
                    blocks.Add(new byte[length]);
                }

                return blocks;
            }

            private void FillBlocksWithCodewords() {
                if (_blocksFilled) {
                    return;
                }

                int codewordCount = 1;
                foreach (var codeword in _codewordProvider.GetCodewords()) {
                    if (codewordCount <= _dataBlockLengthSum) {
                        AddCodewordToDataBlocks(codeword, codewordCount);
                    }
                    else if (codewordCount <= _dataBlockLengthSum + _errorCorrectionBlockLengthSum) {
                        AddCodewordToErrorCorrectionBlocks(codeword, codewordCount - _dataBlockLengthSum);
                    }
                    else {
                        break;
                    }

                    codewordCount++;
                }

                _blocksFilled = true;
            }

            private void AddCodewordToDataBlocks(byte codeword, int count) {
                int blockCount = _dataBlocks.Count;
                int blockIndex = (count - 1) % blockCount;
                int codewordIndex = (count - 1) / blockCount;
                
                if (codewordIndex > _dataBlocks[0].Length - 1) {
                    blockIndex += _shorterDataBlockCount;
                }

                _dataBlocks[blockIndex][codewordIndex] = codeword;
            }

            private void AddCodewordToErrorCorrectionBlocks(byte codeword, int count) {
                int blockCount = _errorCorrectionBlocks.Count;
                int blockIndex = (count - 1) % blockCount;
                int codewordIndex = (count - 1) / blockCount;

                _errorCorrectionBlocks[blockIndex][codewordIndex] = codeword;
            }

            private bool TryCorrectErrors() {
                for (int i = 0; i < _dataBlocks.Count; i++) {
                    int[] dataBlockInt = _dataBlocks[i].Select(codeword => (int)codeword).ToArray();
                    int[] errorCorrectionBlockInt = _errorCorrectionBlocks[i].Select(codeword => (int)codeword).ToArray();
                    if (!_codewordErrorCorrector.TryCorrectBlock(dataBlockInt, errorCorrectionBlockInt, out int[] correctedBlock)) {
                        //Console.WriteLine($"error correction of block {i}: failed");
                        return false;
                    }
                    //Console.WriteLine($"error correction of block {i}: successful");
                    _dataBlocks[i] = correctedBlock.Select(codeword => (byte)codeword).ToArray();
                }

                return true;
            }

            private byte[] GetDataCodewordsInOrder() {
                int lengthSum = 0;
                foreach (var block in _dataBlocks) {
                    lengthSum += block.Length;
                }

                var dataCodewords = new byte[lengthSum];

                int index = 0;
                foreach (var block in _dataBlocks) {
                    foreach (var codeword in block) {
                        dataCodewords[index] = codeword;
                        index++;
                    }
                }
                
                return dataCodewords;
            }
        }

        private static class DataCodewordSegmenter {
            private const int _modeIndicatorLength = 4; 
            private static int _byteSize = 8;

            /// <summary>
            /// Takes sorted array of data codewords and tries to divides them into DataSegments 
            /// according to the mode indicators and character counts.
            /// </summary>
            /// <param name="dataCodewords">Sorted array of error corrected data codewords.</param>
            /// <param name="result">List of DataSegments present in the data codewords if successful, otherwise empty asignment.</param>
            /// <exception cref="InvalidDataException">Thrown when unknown mode present in the data.</exception>
            /// <returns>True if segmentation successful, else false.</returns>
            public static bool TrySegmentByMode(QRVersion codeVersion, byte[] dataCodewords, out List<DataSegment> result) {
                var segments = new List<DataSegment>();
                int offset = 0;

                // Itterate over possible multiple segments
                for (int codewordsPosition = 0; codewordsPosition < dataCodewords.Length; codewordsPosition++) {
                    QRMode mode;
                    try {
                        if (!TryGetMode(dataCodewords[codewordsPosition], dataCodewords[codewordsPosition + 1], offset, out mode)) {
                            break;
                        }
                    }
                    catch (InvalidDataException) {
                        result = new List<DataSegment>();
                        return false;
                    }
                    
                    // Update index and offset
                    codewordsPosition = (offset + _modeIndicatorLength < _byteSize) ? codewordsPosition : codewordsPosition + 1;
                    offset = (offset + _modeIndicatorLength) % _byteSize;

                    int characterCountIndicatorLength = GetCharacterCountIndicatorLength(codeVersion, mode);
                    var characterBytes = new byte[] {dataCodewords[codewordsPosition], dataCodewords[codewordsPosition + 1], dataCodewords[codewordsPosition + 2]};
                    int characterCount = GetCharacterCount(characterBytes, characterCountIndicatorLength, offset);
                    int segmentBitCount = GetSegmentBitCount(characterCount, mode);

                    // If count longer than rest of codeword characters
                    if ((codewordsPosition * _byteSize) + offset + segmentBitCount > dataCodewords.Length * _byteSize) {
                        result = new List<DataSegment>();
                        return false;
                    }

                    // Update index and offset
                    codewordsPosition = codewordsPosition + (characterCountIndicatorLength + offset) / _byteSize;
                    offset = (offset + characterCountIndicatorLength) % _byteSize;

                    int padding = (segmentBitCount % _byteSize) + offset == 0 ? 0 : 1;
                    var segmentDataWithOffset = dataCodewords.Skip(codewordsPosition).Take((segmentBitCount / _byteSize) + padding).ToArray();
                    var segmentData = GetBytesWithoutOffset(segmentDataWithOffset, offset);

                    segments.Add(new DataSegment(mode, segmentBitCount, segmentData));

                    offset = (offset + segmentBitCount) % _byteSize;
                    codewordsPosition += segmentBitCount / _byteSize;
                }

                result = segments;
                return true;
            }

            /// <summary>
            /// Gets the length of character count indicator in bits. 
            /// Supported QRModes are `QRMode.Numeric`, `QRMode.Alphanumeric`, `QRMode.Byte`, `QRMode.Kanji`.
            /// </summary>
            /// <param name="version">QR code version.</param>
            /// <param name="mode">QR code mode.</param>
            /// <returns>Length of character count indicator in bits.</returns>
            private static int GetCharacterCountIndicatorLength(QRVersion version, QRMode mode) {
                switch (mode) {
                    case QRMode.Numeric:
                        if (version.value <= 9) {
                            return 10;
                        }
                        else if (version.value <= 26) {
                            return 12;
                        }
                        else {
                            return 14;
                        }
                    case QRMode.Alphanumeric:
                        if (version.value <= 9) {
                            return 9;
                        }
                        else if (version.value <= 26) {
                            return 11;
                        }
                        else {
                            return 13;
                        }
                    case QRMode.Byte:
                        if (version.value <= 9) {
                            return 8;
                        }
                        else {
                            return 16;
                        }
                    case QRMode.Kanji:
                        if (version.value <= 9) {
                            return 8;
                        }
                        else if (version.value <= 26) {
                            return 10;
                        }
                        else {
                            return 12;
                        }
                    default:
                        throw new NotSupportedException(message: "Mode not supported.");
                }
            }

            /// <summary>
            /// Reads character count indicator to get the number of characters in segment.
            /// Bits are in Msb order.
            /// </summary>
            /// <param name="bytes">Array of input bytes of length three.</param>
            /// <param name="characterCountIndicatorLength">Number of bits in the character indicator.</param>
            /// <param name="offset">Offset of the first valid bit in byte.</param>
            /// <returns>Number of character in segment.</returns>
            private static int GetCharacterCount(byte[] bytes, int characterCountIndicatorLength, int offset) {
                if (bytes.Length > 3) {
                    throw new InvalidParameterException(0);
                }
                if (characterCountIndicatorLength > _byteSize * 2) {
                    throw new InvalidParameterException(1);
                }

                // Get two data bytes
                var dataBytes = GetBytesWithoutOffset(bytes, offset);

                // Get character count from bits
                int characterCount = ((dataBytes[0] << _byteSize) | (dataBytes[1])) >> ((_byteSize * 2) - characterCountIndicatorLength);
                return characterCount;
            }

            private static int GetSegmentBitCount(int characterCount, QRMode mode) {
                switch (mode) {
                    case QRMode.Byte:
                        return characterCount * _byteSize;
                    case QRMode.Numeric:
                        int reminderBits = 0;
                        if (characterCount % 3 != 0) {
                            reminderBits = characterCount % 3 == 1 ? 4 : 7;
                        } 
                        int bitCount = ((characterCount / 3) * 10) + reminderBits;
                        return bitCount;
                    case QRMode.Alphanumeric:
                        reminderBits = characterCount % 2 == 0 ? 0 : 6;
                        bitCount = ((characterCount / 2) * 11) + reminderBits;
                        return bitCount;
                    default:
                        throw new NotSupportedException("Mode not supported.");
                }
            }

            private static bool TryGetMode(byte firstByte, byte secondByte, int offset, out QRMode result) {
                byte resultByte;
                if (offset + _modeIndicatorLength <= _byteSize) {
                    byte offsetMask = (byte)(Byte.MaxValue >> offset);
                    resultByte = (byte)((firstByte & offsetMask) >> _modeIndicatorLength);  
                }
                else {
                    int positionsInSecondByte = (offset + _modeIndicatorLength) % _byteSize;
                    byte firstPart = (byte)((firstByte << offset) >> _modeIndicatorLength);
                    byte secondPart = (byte)(secondByte >> (_byteSize - positionsInSecondByte));
                    resultByte = (byte)(firstPart | secondPart);
                }

                // If end of message 
                if (resultByte == 0) {
                    result = new QRMode();
                    return false;
                }

                try {
                    result = (QRMode)resultByte;
                    return true;
                }
                catch {
                    result = new QRMode();
                    throw new InvalidDataException(message: "Provided data does not encode any known mode.");
                }
            }

            private static byte[] GetBytesWithoutOffset(byte[] bytes, int offset) {
                if (bytes.Length == 0) {
                    throw new InvalidDataException("Byte array can not be of length 0.");
                }
                if (offset == 0) {
                    return bytes;
                }
                if (bytes.Length == 1) {
                    var returnByte = (byte)(bytes[0] << offset);
                    return new byte[] {returnByte};
                }

                var trimmedBytes = new byte[bytes.Length - 1];
                for (int i = 0; i < bytes.Length - 1; i++) {
                    byte firstBytePart = (byte)(bytes[i] << offset);
                    byte secondBytePart = (byte)(bytes[i + 1] >> (_byteSize - offset));

                    trimmedBytes[i] = (byte)(firstBytePart | secondBytePart);
                }
                
                return trimmedBytes;
            }
        }

        private static class DataDecoder {
            /// <summary>
            /// Tries to decode different kinds of data according to the modes.
            /// </summary>
            /// <param name="segment">Segment of data in one mode to be decoded.</param>
            /// <param name="result">Decoded data if successful, empty asignment otherwise.</param>
            /// <returns>True if successful, false otherwise. Result in out parametr result.</returns>
            /// <exception cref="NotSupportedException">Thrown if mode of segment not supported.</exception>
            public static bool TryDecode(DataSegment segment, out DecodedData result) {

                switch(segment.Mode) {
                    case QRMode.Alphanumeric:
                        return TryDecodeAlphanumeric(segment, out result);
                    case QRMode.Numeric:
                        return TryDecodeNumeric(segment, out result);
                    case QRMode.Byte:
                        return TryDecodeByte(segment, out result);
                    default:
                        throw new NotSupportedException("Mode of the QR code data is not supported.");
                }
            }

            private static bool TryDecodeAlphanumeric(DataSegment segment, out DecodedData result) {

                // Dummy implementation
                result = new DecodedData();
                return false;
            }

            private static bool TryDecodeNumeric(DataSegment segment, out DecodedData result) {

                // Dummy implementation
                result = new DecodedData();
                return false;
            }

            private static bool TryDecodeByte(DataSegment segment, out DecodedData result) {
                if (segment.Mode is not QRMode.Byte) {
                    result = new DecodedData();
                    return false;
                }

                Encoding qrByte = Encoding.GetEncoding("ISO-8859-1");
                Encoding unicode = Encoding.Unicode;
                byte[] bytesInUnicodeEncoding;
                try {
                    bytesInUnicodeEncoding = Encoding.Convert(qrByte, unicode, segment.Data);
                }
                catch (DecoderFallbackException) {
                    result = new DecodedData();
                    return false;
                }
                catch (EncoderFallbackException) {
                    result = new DecodedData();
                    return false;
                }

                string data = unicode.GetString(bytesInUnicodeEncoding);

                result = new DecodedData() {DataType = ContentType.Text, Data = data};
                return true;
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
    
        private class DataSegment {
            public DataSegment(QRMode mode, int validBits, byte[] data) {
                Mode = mode;
                ValidBits = validBits;
                Data = data;
            }
            public QRMode Mode { get; init; }
            public int ValidBits { get; init; }
            public byte[] Data { get; init; }
        }

        private static class QRBlockInfo {

            public static int[] GetDataBlockLengths(QRVersion version, QRErrorCorrectionLevel errorCorrectionLevel) {
                
                var blocks = GetBlocks(version, errorCorrectionLevel);
                return blocks.DataBlockLengths;
            }

            public static int[] GetErrorCorrectionBlockLengths(QRVersion version, QRErrorCorrectionLevel errorCorrectionLevel) {
                
                var blocks = GetBlocks(version, errorCorrectionLevel);
                return blocks.ErrorCorrectionBlockLengths;
            }

            private static Blocks GetBlocks(QRVersion version, QRErrorCorrectionLevel errorCorrectionLevel) {
                var blockInfo = _blockInfoByVersion[version.value];

                switch(errorCorrectionLevel) {
                    case QRErrorCorrectionLevel.L:
                        return blockInfo.L;
                    case QRErrorCorrectionLevel.M:
                        return blockInfo.M;
                    case QRErrorCorrectionLevel.Q:
                        return blockInfo.Q;
                    case QRErrorCorrectionLevel.H:
                        return blockInfo.H;
                    default:
                        throw new InvalidParameterException();
                }
            }

            private static readonly VersionBlockInfo[] _blockInfoByVersion = {
                new VersionBlockInfo(),
                //Version 1
                new VersionBlockInfo(
                    new Blocks(new int[] {19}, new int[] {7}), 
                    new Blocks(new int[] {16}, new int[] {10}),
                    new Blocks(new int[] {13}, new int[] {13}),
                    new Blocks(new int[] {10}, new int[] {16})),
                //Version 2
                new VersionBlockInfo(
                    new Blocks(new int[] {34}, new int[] {10}), 
                    new Blocks(new int[] {28}, new int[] {16}),
                    new Blocks(new int[] {22}, new int[] {22}),
                    new Blocks(new int[] {16}, new int[] {28})),
                //Version 3
                new VersionBlockInfo(
                    new Blocks(new int[] {55}, new int[] {15}), 
                    new Blocks(new int[] {44}, new int[] {26}),
                    new Blocks(new int[] {17, 17}, new int[] {18, 18}),
                    new Blocks(new int[] {13, 13}, new int[] {22, 22})),
                //Version 4
                new VersionBlockInfo(
                    new Blocks(new int[] {80}, new int[] {20}), 
                    new Blocks(new int[] {32, 32}, new int[] {18, 18}),
                    new Blocks(new int[] {24, 24}, new int[] {26, 26}),
                    new Blocks(new int[] {9, 9, 9, 9}, new int[] {16, 16, 16, 16})),
                //Version 5
                new VersionBlockInfo(
                    new Blocks(new int[] {108}, new int[] {26}), 
                    new Blocks(new int[] {43, 43}, new int[] {24, 24}),
                    new Blocks(new int[] {15, 15, 16, 16}, new int[] {18, 18, 18, 18}),
                    new Blocks(new int[] {11, 11, 12, 12}, new int[] {22, 22, 22, 22})),
                //Version 6
                new VersionBlockInfo(
                    new Blocks(new int[] {68, 68}, new int[] {16, 16}), 
                    new Blocks(new int[] {27, 27, 27, 27}, new int[] {16, 16, 16, 16}),
                    new Blocks(new int[] {19, 19, 19, 19}, new int[] {24, 24, 24, 24}),
                    new Blocks(new int[] {15, 15, 15, 15}, new int[] {28, 28, 28, 28})),
                //Version 7
                new VersionBlockInfo(
                    new Blocks(new int[] {78, 78}, new int[] {20, 20}), 
                    new Blocks(new int[] {31, 31, 31, 31}, new int[] {18, 18, 18, 18}),
                    new Blocks(new int[] {14, 14, 15, 15, 15, 15}, new int[] {18, 18, 18, 18, 18, 18}),
                    new Blocks(new int[] {13, 13, 13, 13, 14}, new int[] {26, 26, 26, 26, 26})),
                //Version 8
                new VersionBlockInfo(
                    new Blocks(new int[] {97, 97}, new int[] {24, 24}), 
                    new Blocks(new int[] {38, 38, 39, 39}, new int[] {22, 22, 22, 22}),
                    new Blocks(new int[] {18, 18, 18, 18, 19, 19}, new int[] {22, 22, 22, 22, 22, 22}),
                    new Blocks(new int[] {14, 14, 14, 14, 15, 15}, new int[] {26, 26, 26, 26, 26, 26})),
                //Version 9
                new VersionBlockInfo(
                    new Blocks(new int[] {116, 116}, new int[] {30, 30}), 
                    new Blocks(new int[] {36, 36, 36, 37, 37}, new int[] {22, 22, 22, 22, 22}),
                    new Blocks(new int[] {16, 16, 16, 16, 17, 17, 17, 17}, new int[] {20, 20, 20, 20, 20, 20, 20, 20}),
                    new Blocks(new int[] {12, 12, 12, 12, 13, 13, 13, 13}, new int[] {24, 24, 24, 24, 24, 24, 24, 24})),
                //Version 10
                new VersionBlockInfo(
                    new Blocks(new int[] {68, 68, 69, 69}, new int[] {18, 18, 18, 18}), 
                    new Blocks(new int[] {43, 43, 43, 43, 44}, new int[] {26, 26, 26, 26, 26}),
                    new Blocks(new int[] {19, 19, 19, 19, 19, 19, 20, 20}, new int[] {24, 24, 24, 24, 24, 24, 24, 24}),
                    new Blocks(new int[] {15, 15, 15, 15, 15, 15, 16, 16}, new int[] {28, 28, 28, 28, 28, 28, 28, 28})),
                //Version 11
                new VersionBlockInfo(
                    new Blocks(new int[] {81, 81, 81, 81}, new int[] {20, 20, 20, 20}), 
                    new Blocks(new int[] {50, 51, 51, 51, 51}, new int[] {30, 30, 30, 30, 30}),
                    new Blocks(new int[] {22, 22, 22, 22, 23, 23, 23, 23}, new int[] {28, 28, 28, 28, 28, 28, 28, 28}),
                    new Blocks(new int[] {12, 12, 12, 13, 13, 13, 13, 13, 13, 13, 13}, new int[] {24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24})),
                //Version 12
                new VersionBlockInfo(
                    new Blocks(new int[] {92, 92, 93, 93}, new int[] {24, 24, 24, 24}), 
                    new Blocks(new int[] {36, 36, 36, 36, 36, 36, 37, 37}, new int[] {22, 22, 22, 22, 22, 22, 22, 22}),
                    new Blocks(new int[] {20, 20, 20, 20, 21, 21, 21, 21, 21, 21}, new int[] {26, 26, 26, 26, 26, 26, 26, 26, 26}),
                    new Blocks(new int[] {14, 14, 14, 14, 14, 14, 14, 15, 15, 15, 15}, new int[] {28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28})),
                //Version 13
                new VersionBlockInfo(
                    new Blocks(new int[] {107, 107, 107, 107}, new int[] {26, 26, 26, 26}), 
                    new Blocks(new int[] {37, 37, 37, 37, 37, 37, 37, 37, 38}, new int[] {22, 22, 22, 22, 22, 22, 22, 22, 22}),
                    new Blocks(new int[] {20, 20, 20, 20, 20, 20, 20, 20, 21, 21, 21, 21}, new int[] {24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24}),
                    new Blocks(new int[] {11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 12, 12, 12, 12}, new int[] {22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22})),
                //Version 14
                new VersionBlockInfo(
                    new Blocks(new int[] {115, 115, 115, 116}, new int[] {30, 30, 30, 30}), 
                    new Blocks(new int[] {40, 40, 40, 40, 41, 41, 41, 41, 41}, new int[] {24, 24, 24, 24, 24, 24, 24, 24, 24}),
                    new Blocks(new int[] {16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 17, 17, 17, 17, 17}, new int[] {20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20}),
                    new Blocks(new int[] {12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 13, 13, 13, 13, 13}, new int[] {24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24})),
                //Version 15
                new VersionBlockInfo(
                    new Blocks(new int[] {87, 87, 87, 87, 87, 88}, new int[] {22, 22, 22, 22, 22, 22}), 
                    new Blocks(new int[] {41, 41, 41, 41, 41, 42, 42, 42, 42, 42}, new int[] {24, 24, 24, 24, 24, 24, 24, 24, 24, 24}),
                    new Blocks(new int[] {24, 24, 24, 24, 24, 25, 25, 25, 25, 25, 25, 25}, new int[] {30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30}),
                    new Blocks(new int[] {12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 13, 13, 13, 13, 13, 13, 13}, new int[] {24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24})),
                //Version 16
                new VersionBlockInfo(
                    new Blocks(new int[] {98, 98, 98, 98, 98, 99}, new int[] {24, 24, 24, 24, 24, 24}),
                    new Blocks(new int[] {45, 45, 45, 45, 45, 45, 45, 46, 46, 46}, new int[] {28, 28, 28, 28, 28, 28, 28, 28, 28, 28}),
                    new Blocks(new int[] {19, 19, 19, 19, 19, 19, 19, 19, 19, 19, 19, 19, 19, 19, 20, 20}, new int[] {24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24}),
                    new Blocks(new int[] {15, 15, 15, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16}, new int[] {30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30})),

                // TODO: DO VERSIONS TO 40!!! HAVE TO CHANGE THE SIGNATURE TO NOT REPEAT THE VALUES SO MUCH
            
            };
            


            private struct VersionBlockInfo {
                public VersionBlockInfo(Blocks l, Blocks m, Blocks q, Blocks h) {
                    L = l;
                    M = m;
                    Q = q;
                    H = h;
                }
                public Blocks L;
                public Blocks M;
                public Blocks Q;
                public Blocks H;
            }

            private struct Blocks {
                public Blocks(int[] dataLengths, int[] errorCorrectionLengths) {
                    DataBlockLengths = dataLengths;
                    ErrorCorrectionBlockLengths = errorCorrectionLengths;
                }
                public int[] DataBlockLengths;
                public int[] ErrorCorrectionBlockLengths;
            }
        }
    }
}