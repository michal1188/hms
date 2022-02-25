using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HMS.CirrusCommands
{
     class GetLastMeasurementCommand : CirrusCommand
    {
        public GetLastMeasurementCommand(string deviceId, int responseTimeoutInSeconds) : base(deviceId, responseTimeoutInSeconds)
        {
            _payloadCirrus.command = "get last measurements";
            _contentCirrusRequest.methodName = _methodName;
            _contentCirrusRequest.responseTimeoutInSeconds = responseTimeoutInSeconds;
            _contentCirrusRequest.payload = _payloadCirrus;
        }

        public override void setCommandResult(Task<string> HttpClientRequest)
        {
            IList<JToken> resultMessage = JObject.Parse(HttpClientRequest.Result);
           // Console.WriteLine(HttpClientRequest.Result.ToString());
            if (((JProperty)resultMessage[0]).Name == "Message")
            {
                string messageContent = resultMessage[0].First.ToString();
                JObject jsonMessageContent = JObject.Parse(messageContent);
                string response = jsonMessageContent["errorCode"].ToString();
                setCirrusResponse(getErrorMeassage(response));

            }

            else if (((JProperty)resultMessage[0]).Name == "status")
            {
                string messageContent = resultMessage[0].First.ToString();
                if (messageContent == "0")
                {
                    setCirrusResponse(getErrorMeassage("CumulusError"));
                }
                if (messageContent == "1")
                {
                    string dictionaryValue = "{\"message\": "+resultMessage[1].First.ToString()+"}";
                    updateSuccessDictionaryValue("LastMeasurements", dictionaryValue);
                    string response = "LastMeasurements";
                    setCirrusResponseLastMeasurment(getSuccessMeassage(response));


                }
                else if (resultMessage[1].First["status"].ToString() == "EMPTY")
                {
                    setCirrusResponse(getSuccessMeassage("EMPTY"));
                }
            }
            else
            {
                Console.WriteLine(HttpClientRequest.Result.ToString());
                //setCirrusResponse("{\"message\":Nieznany kod błędu skontaktuj się z administratorem }");
                setCirrusResponse("Nieznany kod błędu skontaktuj się z administratorem ");
            }


        }

    }
}
