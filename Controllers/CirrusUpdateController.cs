using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace HMS.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class CirrusUpdateController : ControllerBase
    { 

        private readonly ILog log = LogManager.GetLogger("mylog");
        X509Certificate2 Cert = new X509Certificate2(@"Certificates/user.p12", Security.GetSSLCertPassword(), X509KeyStorageFlags.MachineKeySet);

        [HttpPost]
        public IActionResult UplaodFile([FromForm(Name = "BinFile")] IFormFile BinFile)
        {
            try
            {
                string result = GetUplodResult(ConnectServer(BinFile));
                object apacheResponse = new { message = result};

                return Ok(apacheResponse);

            }
             
            catch (Exception e)
            {

                log.Error(e);
                
                // Console.WriteLine(e);
                object apacheError = new { message = e.InnerException.Message };

                return Ok(apacheError);

            }

        }


 
        public async Task<IActionResult> SaveFile([FromForm(Name = "BinFile")] IFormFile BinFile)
        {
            try
            {
                if (BinFile == null || BinFile.Length == 0)
                    return Ok("FileNotSelected");
                var path = Path.Combine(
                            @"CirrusUpdateFiles/",
                            BinFile.FileName);
                 
                await using var stream = new FileStream(path, FileMode.Create);
                BinFile.CopyTo(stream);
                stream.Flush();
            }
            catch (Exception ex)
            {
                log.Error(ex);
                throw;
            }
            return Ok("Ok");
        }
        


        private   async Task<string> ConnectServer([FromForm(Name = "BinFile")] IFormFile BinFile)
        {

            await SaveFile(BinFile);
                  var filePath = Path.Combine(
                             @"CirrusUpdateFiles/",BinFile.FileName);
                 using (var client = new CertificateWebClient(Cert)) {
                client.Credentials = CredentialCache.DefaultCredentials;

                var ans = await client.UploadFileTaskAsync(@"https://10.0.0.65/upload.php", "POST", @filePath);
                string response = System.Text.Encoding.UTF8.GetString(ans);
                client.Dispose();
                return response;
            }
            }
        private string GetUplodResult(Task<string> webClientConection)
        {
          
            return webClientConection.Result.ToString();
        }

    }
}



 