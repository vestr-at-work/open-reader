
namespace CodeReader {
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