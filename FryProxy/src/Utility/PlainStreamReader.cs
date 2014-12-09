using System;
using System.IO;

namespace FryProxy.Utility {

    internal class PlainStreamReader : TextReader {

        private const Int32 EmptyBuffer = Int32.MinValue;
        private int _lastPeek = EmptyBuffer;
        private int _lastRead = EmptyBuffer;
        private readonly Stream _stream;

        public PlainStreamReader(Stream stream) {
            _stream = stream;
        }

        public Boolean EndOfStream {
            get { return _lastRead == -1; }
        }

        public override int Read() {
            if (EndOfStream) {
                throw new EndOfStreamException();
            }

            if (_lastPeek == EmptyBuffer) {
                return _lastRead = _stream.ReadByte();
            }

            _lastPeek = EmptyBuffer;

            return _lastRead;
        }

        public override int Peek() {
            return _lastPeek == EmptyBuffer ? _lastPeek = Read() : _lastPeek;
        }

    }

}