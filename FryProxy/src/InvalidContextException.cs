using System;

namespace FryProxy {

    public class InvalidContextException : InvalidOperationException {

        public InvalidContextException(string message) : base(String.Format("{0} should not be null", message)) {}

    }

}