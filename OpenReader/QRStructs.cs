
using System.Reflection;

namespace CodeReader {
    public struct QRFormatInfo {
        public QRErrorCorrectionLevel ErrorCorrectionLevel;
        public QRDataMask DataMask;
    }

    public struct QRVersion {
        private int _version;
        public int Version { 
            get => _version; 
            set {
                if (value <= 40 && value >= 1) {
                    _version = value;
                }
            }

        }
    }
}