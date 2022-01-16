using System;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;


namespace HMS.CirrusCommands
{
    public class AzureTokenGenerator
    {
        public string tokenKey { get; }
        int expiryInSeconds { get; set; }
        string resourceUri { get; set; }
        string policyName { get; set; }
        public AzureTokenGenerator() {
            tokenKey = Security.GetTokenSASkey();
            expiryInSeconds = 3600;
            resourceUri= "hsm-iot-hub.azure-devices.net";
            policyName = "iothubowner";
        }

        public string generateSasToken()
        {
            TimeSpan fromEpochStart = DateTime.UtcNow - new DateTime(1970, 1, 1);
            string expiry = Convert.ToString((int)fromEpochStart.TotalSeconds + expiryInSeconds);
            string stringToSign = WebUtility.UrlEncode(resourceUri) + "\n" + expiry;
            HMACSHA256 hmac = new HMACSHA256(Convert.FromBase64String(tokenKey));
            string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            string token = String.Format(CultureInfo.InvariantCulture, "SharedAccessSignature sr={0}&sig={1}&se={2}", WebUtility.UrlEncode(resourceUri), WebUtility.UrlEncode(signature), expiry);
            if (!String.IsNullOrEmpty(policyName))
            {
                token += "&skn=" + policyName;
            }
            return token;
        }
    }



}
