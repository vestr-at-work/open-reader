
namespace CodeReader {
    interface IQRRawCodewordProvider {
        public IEnumerable<byte> GetCodewords();
    }

    /// <summary>
    /// Class for completing 8-bit words from unmasked module values and returning them by iterator method.
    /// Main public method of the class is 'GetCodewords'.
    /// </summary>
    class QRCodewordCompletor : IQRRawCodewordProvider {
        private IQRUnmaskedDataProvider _dataProvider;

        private static int _byteSize = 8;

        public QRCodewordCompletor(IQRUnmaskedDataProvider dataProvider) {
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
}