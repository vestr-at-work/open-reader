
using STH1123.ReedSolomon;

namespace CodeReader {
    public interface IBlockErrorCorrector {
        public bool TryCorrectBlock(int[] dataBlock, int[] errorCorrectionBlock, out int[] correctedDataBlock);
    }

    // Takes data codeword and coresponding error codeword and returns corrected data or false or smthng
    class ReedSolomonErrorCorrector : IBlockErrorCorrector {
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
}