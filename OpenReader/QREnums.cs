
namespace CodeReader {
    public enum ContentType {
        Text, 
        Binary,
        Action
    }

    public enum QRMode {
        Numeric = 0b0001,
        Alphanumeric = 0b0010,
        Byte = 0b0100,
        Kanji = 0b1000,
        StructuredAppend = 0b0011,
        ExtendedChannelInterpretation = 0b0111,
        FNC1First = 0b0101,
        FNC1Second = 0b1010
    }

    public enum QRErrorCorrectionLevel {
        L = 0b01,
        M = 0b00,
        Q = 0b11,
        H = 0b10
    }

    public enum QRDataMask {
        Mask0,
        Mask1,
        Mask2,
        Mask3,
        Mask4,
        Mask5,
        Mask6,
        Mask7
    }
}