using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HMS.CirrusCommands
{
    class MeasurementsFreeMemCommand : CirrusCommand
    {

        public MeasurementsFreeMemCommand(string deviceId, int responseTimeoutInSeconds) : base(deviceId, responseTimeoutInSeconds)
        {
            _payloadCirrus.command = "free_space measurements";
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
                // Console.WriteLine(HttpClientRequest.Result.ToString());
                string messageContent = resultMessage[0].First.ToString();
                JObject jsonMessageContent = JObject.Parse(messageContent);
                string response = jsonMessageContent["errorCode"].ToString();
                // Console.WriteLine(response);
                //Console.WriteLine(response.GetType());
                // Console.WriteLine(resultMessage[0].GetType());
                //Console.WriteLine(jsonMessageContent["errorCode"].ToString());
                //Console.WriteLine(jsonMessageContent["errorCode"].GetType());
                setCirrusResponse(getErrorMeassage(response));

            }
            else if (((JProperty)resultMessage[0]).Name == "status")
            {
                //Console.WriteLine(HttpClientRequest.Result.ToString());
                string messageContent = resultMessage[1].First.ToString();
                JObject jsonMessageContent = JObject.Parse(messageContent);

                IList<JToken> tempVariableForDictionaryKey = JObject.Parse(jsonMessageContent.ToString());
                string dictionaryKey = ((JProperty)tempVariableForDictionaryKey[0]).Name;
                if (dictionaryKey == "status")
                {
                    setCirrusResponse(getErrorMeassage("CumulusError"));
                }
                else
                {
                    string dictionaryValue = jsonMessageContent["free_space"].ToString();
                    updateSuccessDictionaryValue(dictionaryKey, dictionaryValue);
                    string response = dictionaryKey;
                    setCirrusResponse(getSuccessMeassage(response));
                }
            }
            else
            {
                Console.WriteLine(HttpClientRequest.Result.ToString());
                setCirrusResponse("Nieznany kod błędu skontaktuj się z administratorem");
            }


        }


    }
}
