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
    public class GetLastMeasurementController : ControllerBase
    {

        private readonly ILog log = LogManager.GetLogger("mylog");
        [HttpPost]
        public IActionResult GetLastMeasurement(JsonElement parameters)
        {
            dynamic json = JsonConvert.DeserializeObject(parameters.ToString());

            string deviceID = json.deviceID;

            CirrusCommand getLastMeasurementController = new GetLastMeasurementCommand(deviceID, 200);
            try
            {
                getLastMeasurementController.setCommandResult(getLastMeasurementController.sendCommand());
                object CommandResult = getLastMeasurementController.getCirrusResponse();
                return Ok(CommandResult);

            }

            catch (Exception e)
            {
                log.Error(e);
                return Ok(e);

            }

        }



    }
}
