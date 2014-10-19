using System.IO;

namespace FryProxy.Utility {

    internal class PlainStreamReader : TextReader {

        private readonly Stream _stream;

        private int _current = -1;

        public PlainStreamReader(Stream stream) {
            _stream = stream;
        }

        public override int Read() {
            switch (_current) {
                case -1:
                    return _stream.ReadByte();
                default: {
                    var temp = _current;
                    _current = -1;
                    return temp;
                }
            }
        }

        public override int Peek() {
            return _current = Read();
        }

    }

}