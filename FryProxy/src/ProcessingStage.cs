namespace FryProxy {

    /// <summary>
    ///     Steps perfromed durign processing HTTP request
    /// </summary>
    public enum ProcessingStage {

        ReceiveRequest = 0,
        ConnectToServer = 1,
        ReceiveResponse = 2,
        SendResponse = 3,
        Completed = 4

    }

}