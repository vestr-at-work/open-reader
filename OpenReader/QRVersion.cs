
namespace CodeReader {
    public struct QRVersion {
        private int _value;
        public int value { 
            get => _value; 
            set {
                if (value <= 40 && value >= 1) {
                    _value = value;
                }
            }
        }
    }
}