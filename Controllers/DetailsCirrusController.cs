using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Globalization;
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

            DateTime dateTimeNow = DateTime.Now;
            DateTime dateTimeSubstract30Minutes = dateTimeNow.AddMinutes(-30);
            string dateTimeSubstract30MinutesWithFormatToQuery = dateTimeSubstract30Minutes.ToString("dd.MM.yyyy hh:mm:ss");

            IEnumerable<Object> listDetailsCirrus = GetDetailsCirrus(deviceId);
            IEnumerable<Object> listDetailsCirrusWithStatus= UpdateCirrusStatus(listDetailsCirrus, dateTimeSubstract30MinutesWithFormatToQuery);
            object returnedDetails = new { message = listDetailsCirrusWithStatus };

            return Ok(returnedDetails);
        }

        private IEnumerable<Object> GetDetailsCirrus(string deviceId)
        {
            SqlKata.Query detailsCirrusQuery = this.queryFactory.Query("iot.device")
                       .Select("device.device_id", "measure_setup.meter_id", "device.device_model", "device.gsm_signal_power")
                       .Select("device.device_version")
                       .SelectRaw("false AS state")
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
        private bool checkDeviceStatus(int status)
        {
            bool result;
            if (status < 0)
                result = false;
            else if (status == 0)
                result = true;
            else
                result = true;

            return result;

        }
        private IEnumerable<Object> UpdateCirrusStatus(IEnumerable<Object> listDetailsCirrus, string dataTimeMinus30)
        {
            foreach (IDictionary<string, object> row in listDetailsCirrus)
            {
              try
                {
                    if (row["update_ts"] != null)
                    {

                        DateTime dataMeasureMinus30 = DateTime.ParseExact(dataTimeMinus30, "dd.MM.yyyy hh:mm:ss",
                                      System.Globalization.CultureInfo.InvariantCulture);
                    int result = DateTime.Compare((DateTime)row["update_ts"], dataMeasureMinus30);
                    Console.WriteLine(result);
                    row["state"] = checkDeviceStatus(result);
                    }
                }
              catch (Exception e)
                {
                    log.Error(e);
                  //      Console.WriteLine(e);
                }
            }
            return listDetailsCirrus;
        }



        }
}
