using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DetailsCirrusController : ControllerBase
    {

        private readonly QueryFactory queryFactory;
        private readonly ILog log = LogManager.GetLogger("mylog");
        public DetailsCirrusController(QueryFactory queryFactory)
        {
            this.queryFactory = queryFactory;

        }


        [HttpPost]
        public IActionResult DetailsCirrus(JsonElement parameters)
        {

            dynamic json = JsonConvert.DeserializeObject(parameters.ToString());

            string deviceId = json.deviceID ;

            IEnumerable<Object> listDetailsCirrus = GetDetailsCirrus(deviceId);

            object returnedDetails = new { message = listDetailsCirrus };

            return Ok(returnedDetails);
        }

        private IEnumerable<Object> GetDetailsCirrus(string deviceId)
        {
            SqlKata.Query detailsCirrusQuery = this.queryFactory.Query("iot.device")
                       .Select("device.device_id", "measure_setup.meter_id", "device.device_model", "device.gsm_signal_power")
                       .Select("device.device_version", "device.gsm_state")
                       .SelectRaw("device.update_ts   at time zone 'Europe/Warsaw'AS update_ts")
                       .SelectRaw("measurement_electricity.meter_ts   at time zone 'Europe/Warsaw'AS meter_ts")
                       .Select("measurement_electricity.pa", "measurement_electricity.ma", "measurement_electricity.pri", "measurement_electricity.mrc")
                       .Select("measurement_electricity.l1", "measurement_electricity.l2", "measurement_electricity.l3", "measure_setup.measure_setup_id", "device.sim_card_id")
                       .Join("iot.device_port", "device.device_id", "device_port.device_id")
                       .Join("iot.measure_device_setup", "device_port.device_port_id", "measure_device_setup.device_port_id")
                       .Join("iot.measure_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
                       .Join("iot.measurement_electricity", "measure_setup.meter_id", "measurement_electricity.meter_nr")
                       .Where("iot.device.device_id", "=", deviceId)
                       .OrderByDesc("measurement_electricity.meter_ts")
                       .Limit(1);

            IEnumerable<Object> detailsCirrus = new List<object>();
            try
            {
                detailsCirrus = detailsCirrusQuery.Get();
            }
            catch (Exception e)
            {
                log.Error(e);
            }
            return detailsCirrus;
        }

    }
}
