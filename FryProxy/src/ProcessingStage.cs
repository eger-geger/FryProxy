namespace FryProxy {

    public enum ProcessingStage {

        ReceiveRequest = 0,
        ConnectToServer = 1,
        ReceiveResponse = 2,
        SendResponse = 3,
        Completed = 4

    }

}