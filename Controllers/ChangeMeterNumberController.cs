using HMS.CirrusCommands;
using log4net;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Text.Json;


namespace HMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChangeMeterNumberController : ControllerBase
    {

        private readonly ILog log = LogManager.GetLogger("mylog");
        [HttpPost]
        public IActionResult ChangeMeterNumber(JsonElement parameters)
        {
            dynamic json = JsonConvert.DeserializeObject(parameters.ToString());

            // string meterNumber = (json.meterNumber != null) ? json.meterNumber : "xxx";
            // string deviceID = (json.deviceID != null) ? json.deviceID : "xxx";

            string deviceID = json.deviceID;
            string meterNumber = json.newMeterNumber;
            CirrusCommand changeMeterNumberCommand = new ChangeMeterNumberCommand(deviceID, 200, meterNumber);
            try
            {
                changeMeterNumberCommand.setCommandResult(changeMeterNumberCommand.sendCommand());
                object CommandResult = changeMeterNumberCommand.getCirrusResponse();
                return Ok(CommandResult);

            }

            catch (Exception e)
            {
                log.Error(e);
                // Console.WriteLine(e);
                // object Error = new { message = e };
               return Ok(e);

            }

        }

    }
}
