namespace HMS.CirrusCommands
{
    public class ContentCirrusRequest
    {
        public string methodName { get; set; }
        public int responseTimeoutInSeconds { get; set; }
        public PayloadCirrus payload
        { get; set; }

        public ContentCirrusRequest(string methodName, int responseTimeoutInSeconds, PayloadCirrus command)
        {
           this.methodName = methodName;
           this.responseTimeoutInSeconds = responseTimeoutInSeconds;
           this.payload = command;
        }

        public ContentCirrusRequest()
        {
        }
    }
}
