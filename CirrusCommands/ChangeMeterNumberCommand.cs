using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HMS.CirrusCommands
{
     class ChangeMeterNumberCommand : CirrusCommand
    {
 
        bool regularMeterNumber = false;
        string meterNumber;
        public ChangeMeterNumberCommand(string deviceId, int responseTimeoutInSeconds,string meterNumber ) : base(deviceId, responseTimeoutInSeconds )
        {
            _payloadCirrus.command = "set meter number=" + meterNumber;
            _contentCirrusRequest.methodName = _methodName;
            _contentCirrusRequest.responseTimeoutInSeconds = responseTimeoutInSeconds;
            _contentCirrusRequest.payload = _payloadCirrus;
            this.meterNumber = meterNumber;
        }

       
        private void CheckMeterNumber(string newMeternumber) {
            char[] arrayMeterNumber = newMeternumber.ToCharArray();
            bool isNumber  = true;

            for (int i = 0; i < newMeternumber.Length; i++)
            {
                if (!char.IsDigit(arrayMeterNumber[i]))
                {
                    isNumber = false;
                    break;
                }
            }

            if (newMeternumber.Length!=3 )
            {
                setCirrusResponse("Numer licznika musi składać się z 3 cyfr");

            }
            else if (isNumber == false)
            {
                setCirrusResponse("Numer licznika musi składać się wyłącznie z cyfr ");

            }
            else {
                regularMeterNumber = true;
            }


        }

       public override void setCommandResult(Task<string> HttpClientRequest)
        {
            CheckMeterNumber(meterNumber);

            if (regularMeterNumber == true)
            {
                IList<JToken> resultMessage = JObject.Parse(HttpClientRequest.Result);
                if (((JProperty)resultMessage[0]).Name == "Message")
                {
                   // Console.WriteLine(HttpClientRequest.Result.ToString());
                    string messageContent = resultMessage[0].First.ToString();
                    JObject jsonMessageContent = JObject.Parse(messageContent);
                    string response = jsonMessageContent["errorCode"].ToString();
                  //  Console.WriteLine(response);
                   // Console.WriteLine(response.GetType());
                    // Console.WriteLine(resultMessage[0].GetType());
                    //Console.WriteLine(jsonMessageContent["errorCode"].ToString());
                    //Console.WriteLine(jsonMessageContent["errorCode"].GetType());
                    setCirrusResponse(getErrorMeassage(response));

                }
                else if (((JProperty)resultMessage[0]).Name == "status")
                {
                    string messageContent = resultMessage[0].First + 1.ToString();
                    JObject jsonMessageContent = JObject.Parse(messageContent);
                    string response = jsonMessageContent["status"].ToString();
                    setCirrusResponse(getSuccessMeassage(response));
                }
                else
                {
                    Console.WriteLine(HttpClientRequest.Result.ToString());
                    setCirrusResponse("Nieznany kod błędu skontaktuj się z administratorem");
                }



            }
            else
            {
                Console.WriteLine(HttpClientRequest.Result.ToString());
               // setCirrusResponse("");
            }
        }

    }
   
}
