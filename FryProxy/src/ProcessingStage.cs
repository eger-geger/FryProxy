using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FryProxy
{
    public enum ProcessingStage {

        ReceiveRequest, ConnectToServer, ReceiveResponse, Finish

    }
}
