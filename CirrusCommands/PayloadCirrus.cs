namespace HMS.CirrusCommands
{
    public class PayloadCirrus
    {
        public string command{ get; set; }

        public PayloadCirrus(string command)
        {
            this.command = command;
        }

        public PayloadCirrus()
        {
        }
    }
}
