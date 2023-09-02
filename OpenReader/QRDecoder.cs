
namespace CodeReader {
    /// <summary>
    /// Internal class encapsulating methods for decoding 
    /// </summary>
    class QRDecoder {
        private static ushort[] validFomatInfoSequences = {
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

        public static bool TryGetFormatInfo(ParsedQRCode code, out QRFormatInfo formatInfo) {
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

        public static bool TryGetData(ParsedQRCode codeData, QRFormatInfo formatInfo, out ScanResult result) {
            

            // Dummy implementation
            result = new ScanResult() {Success = true, DataType = ContentType.Text, Data = (object)"Dummy implementation string"};
            return true;
        }

        private static ushort GetMainFormatInfoData(ParsedQRCode code) {
            var data = code.Data;
            var size = code.Size;

            var accesor = new FormatInfoAccesor(data, size, true);
            return GetFormatInfoAsNumber(accesor);
        }

        private static ushort GetFormatInfoAsNumber(FormatInfoAccesor accesor) {
            ushort result = 0;
            ushort oneAtOrder = 1;
            foreach(var module in accesor.GetFormatModules()) {
                if (module == 0) {
                    result |= oneAtOrder;
                }
                oneAtOrder <<= 1;
            }

            return result;
        }

        private class FormatInfoAccesor {
            public FormatInfoAccesor(byte[,] data, int size, bool isMain) {
                _data = data;
                _size = size;
                _isMain = isMain;
            }
            private bool _isMain;
            private byte[,] _data;
            private int _size;

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

        private static ushort GetSecondaryFormatInfoData(ParsedQRCode code) {

            return 0;
        }

        private static bool TryParseFormatInfo(ushort rawFormatInfoData, out QRFormatInfo parsedFormatInfo) {
            ushort XorMask = 0b101_0100_0001_0010;
            ushort unmaskedData = (ushort)(rawFormatInfoData ^ XorMask); 

            ushort lowestDistance = ushort.MaxValue;
            ushort? closestValidFormatInfoSequence = null;
            foreach (var validFormatInfoSequence in validFomatInfoSequences) {
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
    }
}