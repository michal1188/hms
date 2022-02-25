using HMS.CirrusCommands;
using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Text.Json;

namespace HMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InstrumentTransformerController : ControllerBase
    {

        private readonly ILog log = LogManager.GetLogger("mylog");
        [HttpPost]
        public IActionResult InstrumentTransformer(JsonElement parameters)
        {
            dynamic json = JsonConvert.DeserializeObject(parameters.ToString());

            // string deviceID = (json.deviceID != null) ? json.deviceID : "xxx";

            string deviceID = json.deviceID;

            CirrusCommand instrumentTransformertCommand = new InstrumentTransformerCommand(deviceID, 200);
            try
            {
                instrumentTransformertCommand.setCommandResult(instrumentTransformertCommand.sendCommand());
                object CommandResult = instrumentTransformertCommand.getCirrusResponse();
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
