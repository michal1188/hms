using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace HMS.CirrusCommands
{
    abstract class CirrusCommand
    {
        protected string _request { get; }
        protected string _tokenSAS;
        protected string _methodName;

        protected int _responseTimeoutInSeconds { get; set; }
        protected string _deviceId { get; set; }

        protected PayloadCirrus _payloadCirrus { get; set; }
        protected ContentCirrusRequest _contentCirrusRequest { get; set; }

        private AzureTokenGenerator Token = new AzureTokenGenerator();
        private readonly ILog log = LogManager.GetLogger("mylog");
        object _cirrusResponse { get; set; }
        Dictionary<string, string> errorDictionary;
        Dictionary<string, string> successDictionary; 
       
        protected CirrusCommand(string deviceId, int responseTimeoutInSeconds)
        {

            this._deviceId = deviceId;
            this._responseTimeoutInSeconds = responseTimeoutInSeconds;

            _payloadCirrus = new PayloadCirrus();
            _contentCirrusRequest = new ContentCirrusRequest();
            _methodName = "command";
            _request = "https://hsm-iot-hub.azure-devices.net/twins/" + deviceId + "/methods?api-version=2018-06-30";
            _tokenSAS = setTokenSasCommand();
            errorDictionary = new Dictionary<string, string>(){
                 {"0", "Nieznany kod błędu, skontatuj się z administratorem"},
                 {"CumulusError", "Sprawdź model urządzenia, najprawdopodobniej próbujesz wysłać komendę do urządzenia innego niż Cirrus"},
                 {"404001", "Operacja nie powiodła się, ponieważ urządzenia nie można znaleźć w usłudze IoT Hub. Urządzenie nie jest zarejestrowane"},
                 {"404103", "Operacja nie powiodła się, ponieważ  urządzenie nie jest w trybie online lub nie zarejestrowało wywołania zwrotnego metody bezpośredniej."} };
            successDictionary = new Dictionary<string, string>()
            {
                {"0", "Nieznany kod błędu, skontatuj się z administratorem"},
                {"OK", "Operacja zakończona sukcesem" },
                { "ERROR", "Błąd więcej informacji w dzienniku zdarzeń urządzenia "},
                //natężenie w amperach/napięcie w wolta
                { "transformer type","" },
                { "free_space","" }
    };
            }
        public abstract  void setCommandResult(Task<string> HttpClientRequest);
        public async  Task<string> sendCommand(){

            try
            {
                Console.WriteLine("jestem w sendCommand2");
                string contentRequest = JsonConvert.SerializeObject(_contentCirrusRequest, Formatting.Indented);

                using (var httpClient = new HttpClient())
                {
                    using (var requestHttpClient = new HttpRequestMessage(new HttpMethod("POST"), _request))
                    {

                        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
                        httpClient.DefaultRequestHeaders.Add("Authorization", _tokenSAS);
                        requestHttpClient.Content = new StringContent(contentRequest, Encoding.UTF8, "application/json");

                        var response = await httpClient.SendAsync(requestHttpClient);
                        return response.Content.ReadAsStringAsync().Result;
                        /* 
                         * Console.WriteLine(response.Content.ReadAsStringAsync().GetType());
                         * Console.WriteLine(requestHttpClient);
                         Console.WriteLine(_request);
                         Console.WriteLine(_tokenSAS);
                         Console.WriteLine(contentRequest);
                         Console.WriteLine(response.Content.ReadAsStringAsync().Result);
                          Console.WriteLine(contentRequest);
                    */

                    }
                }
            }
            catch (Exception e)
            {
                log.Error(e);
                setCirrusResponse("Błąd połączenia z Azure");
                return "MeterNumberFormat  Error";
            }
        }

        public void setCirrusResponse(string message)
        {
            _cirrusResponse = new { message = message };
        }
        public object getCirrusResponse()
        {
            return _cirrusResponse;
        }
        private string setTokenSasCommand()
        {
            try
            {
                _tokenSAS = Token.generateSasToken();
                return _tokenSAS;
                //Console.WriteLine(tokenSAS);
            }
            catch (Exception e)
            {

                log.Error(e);
                throw;
                //  return " Error generateSasToken";
            }
        }

        public string  getErrorMeassage( string key) {

           // Console.WriteLine(errorDictionary[key] );
            if (errorDictionary.ContainsKey(key))
            {
                return errorDictionary[key];
            }
            else { return errorDictionary["0"]; }

        }
        public void updateSuccessDictionaryValue(string key, string newValue) {
            successDictionary[key] = newValue;
        }
        public string getSuccessMeassage(string key)
        {
            //Console.WriteLine(successDictionary[key]);
            if (successDictionary.ContainsKey(key))
            {
                return successDictionary[key];
            }
            else { return successDictionary["0"]; }

        }


    }

}
