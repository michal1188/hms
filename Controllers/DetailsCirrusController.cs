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
                       .Select("device.device_version", "device.sim_card_id", "measure_setup.measure_setup_id")
                       .SelectRaw("device.update_ts   at time zone 'Europe/Warsaw'AS update_ts")
                       .SelectRaw("false AS state, null as meter_ts, null as pa, null as ma, null as pri, null as mrc, null as l1, null as l2, null as l3")
                       .LeftJoin("iot.device_port", "device.device_id", "device_port.device_id")
                       .LeftJoin("iot.measure_device_setup", "device_port.device_port_id", "measure_device_setup.device_port_id")
                       .LeftJoin("iot.measure_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
                       .Where("iot.device.device_id", "=", deviceId)
                       .OrderByRaw("device.update_ts  DESC NULLS LAST")
                       .Limit(1);
            Console.WriteLine(queryFactory.Compiler.Compile(detailsCirrusQuery).Sql);
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
            Console.WriteLine("checkDeviceStatus");
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
            Console.WriteLine("UpdateCirrusStatus");
            SqlKata.Query selectedMaxMeter_ts = null ;

            foreach (IDictionary<string, object> row in listDetailsCirrus)
            {
                selectedMaxMeter_ts = this.queryFactory.Query("iot.measurement_electricity")
                .SelectRaw("meter_ts  at time zone 'Europe/Warsaw',pa,ma,pri,mrc,l1,l2,l3")
                .WhereRaw("meter_nr= '" + row["meter_id"] + "'")
                .OrderByDesc("meter_ts")
                .Limit(1);
                IDictionary<string, object> max = selectedMaxMeter_ts.FirstOrDefault();

                try
                {
                    if (max is null)
                    {
                        row["meter_ts"] = "Brak odczytu";
                        row["pa"] = "Brak odczytu";
                        row["ma"] = "Brak odczytu";
                        row["pri"] = "Brak odczytu";
                        row["mrc"] = "Brak odczytu";
                        row["l1"] = "Brak odczytu";
                        row["l2"] = "Brak odczytu";
                        row["l3"] = "Brak odczytu";
                        continue;
                    }
                    row["meter_ts"] = max["timezone"];
               
                    if (row["meter_ts"] != "Brak odczytu")
                    {
                        DateTime dataMeasureMinus30 = DateTime.ParseExact(dataTimeMinus30, "dd.MM.yyyy hh:mm:ss",
                                                           System.Globalization.CultureInfo.InvariantCulture);
                        int result = DateTime.Compare((DateTime)row["meter_ts"], dataMeasureMinus30);
                        Console.WriteLine(result);
                        row["state"] = checkDeviceStatus(result);
                        row["pa"] = max["pa"];
                        row["ma"] = max["ma"];
                        row["pri"] = max["pri"];
                        row["mrc"] = max["mrc"];
                        row["l1"] = max["l1"];
                        row["l2"] = max["l2"];
                        row["l3"] = max["l3"];
                    }
                }
              catch (Exception e)
                {
                    log.Error(e);
                  //        Console.WriteLine(e);
                  
                }
            }
            return listDetailsCirrus;
        }



        }
}
