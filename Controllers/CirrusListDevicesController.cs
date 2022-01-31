using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;


namespace HMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CirrusListDevicesController : ControllerBase
    {
        private readonly QueryFactory queryFactory;
        private readonly ILog log = LogManager.GetLogger("mylog");
        public CirrusListDevicesController(QueryFactory queryFactory)
        {
            this.queryFactory = queryFactory; 

        }


        [HttpPost]
        public IActionResult DevicesList(JsonElement parameters)
        {
          
            dynamic json = JsonConvert.DeserializeObject(parameters.ToString());
            

            int  pageNumber = (json.pageNumber != null) ? json.pageNumber : 1;
            int rowsPerPage = (json.rowsPerPage != null) ? json.rowsPerPage : 10;
            string sortBy = (json.sortBy != null) ? json.sortBy : "device_id";
            bool sortDesc = (json.sortDesc != null) ? json.sortDesc : false;

            string searchFromJson = json.search;
            int offset = pageNumber * rowsPerPage - rowsPerPage;
            string searchLike = "%" + searchFromJson + "%";

            searchLike = searchLike.Replace(";", "");
            searchLike = searchLike.ToLower();
            

            DateTime dateTimeNow = DateTime.Now;
            DateTime dateTimeSubstract30Minutes = dateTimeNow.AddMinutes(-30);
            //string dateTimeNowWithFormatToQuery = dateTimeNow.ToString("yyyy-MM-dd hh:mm:ss");
            string dateTimeSubstract30MinutesWithFormatToQuery = dateTimeSubstract30Minutes.ToString("dd.MM.yyyy hh:mm:ss");

            IEnumerable<Object> sortedDevices = this.FetchSortedDevicesList(searchLike, sortBy, sortDesc, rowsPerPage, offset);
            IEnumerable<Object> sortedDevicesMaxMeter = SelectMaxMeter_ts(sortedDevices, dateTimeSubstract30MinutesWithFormatToQuery);
            int totalDevices = sortedDevicesMaxMeter.Count();
            object returnedList = new { sortedDevicesMaxMeter = sortedDevicesMaxMeter, totalDevices = totalDevices };

            return Ok(returnedList);
        }
        private bool checkDeviceStatus(int status)
        {
            bool result;
            if (status < 0)
                result=false;
            else if (status == 0)
                result = true;
            else
                result = true;

            return result;

        }
      

        private IEnumerable<Object> FetchSortedDevicesList(string searchLike, string sortBy, bool sortDesc, int rowsPerPage, int offset)
        { 
            SqlKata.Query sortedDevicesQuery = this.queryFactory.Query("iot.device")
                       //.SelectRaw("DISTINCT ON(device.device_id, measure_setup.meter_id) device.device_id,  device.device_model, device.gsm_state, device.gsm_signal_power, device.device_version, measure_setup.measure_setup_id, measure_setup.meter_id")
                       .SelectRaw("DISTINCT ON(device.device_id, measure_setup.meter_id,  device.gsm_signal_power, device.device_version, device.device_model, update_ts) device.device_id,  device.device_model, device.gsm_signal_power, device.device_version, measure_setup.meter_id, measure_setup.measure_setup_id")
                       .SelectRaw("device.update_ts at time zone 'Europe/Warsaw'AS update_ts ")
                       .SelectRaw("null AS last_measure")
                       .SelectRaw("false AS state")
                       .Join("iot.device_port", "device.device_id", "device_port.device_id")
                       .Join("iot.measure_device_setup", "device_port.device_port_id", "measure_device_setup.device_port_id")
                       .Join("iot.measure_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
                     .WhereRaw("(device.device_model Like 'Stratus%' or device.device_model Like 'Cirrus%')")
                       .Where(q =>
                                   q.WhereLike("device.device_id", @searchLike)
                                    .OrWhereLike("device.device_model", @searchLike)
                                    .OrWhereLike("measure_setup.meter_id", @searchLike)
                                    //.OrWhereRaw(" CAST(measure_setup.measure_setup_id AS TEXT) LIKE ?", @searchLike)
                                    .OrWhereRaw(" CAST(device.gsm_signal_power AS TEXT) LIKE ?", @searchLike)                      
                                    .OrWhereRaw(" CAST(device.update_ts AS TEXT) LIKE ?", @searchLike)
                                    .OrWhereLike("device.device_version", @searchLike)
                               // .OrWhereRaw(" CAST(sub.last_measure AS TEXT) LIKE ?", @searchLike)
                               )
           .Limit(rowsPerPage)
           .Offset(offset);
            if (sortDesc == false) { sortedDevicesQuery.OrderBy(sortBy); }
            else { sortedDevicesQuery.OrderByDesc(sortBy); }
            
            IEnumerable<Object> sortedDevices = new List<object>();
            try
            {
                sortedDevices = sortedDevicesQuery.Get();
            }
            catch (Exception e)
            {
                log.Error(e);
            }
            return sortedDevices;
        }

        private IEnumerable<Object> SelectMaxMeter_ts(IEnumerable<Object> sortedDevices, string dataTimeMinus30)
        {
            SqlKata.Query selectedMaxMeter_ts;
            IList<Object> latestMeasureSetupId = new List<object>();
            foreach (IDictionary<string, object> row in sortedDevices)
                 {
                     //W przypadku przypisywania wartosci po meter_id dla Stratus i Cumulusa gdy na danym liczniku jest podpięte kolejne urządzenie to dla starych urzadzeniu
                     //które były na nym liczniku zaczytywana jest data najnowszego pomiaru jaki został wykonany na urządzeniu które jest obecnie podłączone  
                     //Druga opcja łaczenie po measure_setup_id 
                     selectedMaxMeter_ts = this.queryFactory.Query("iot.measurement_electricity")
                     .SelectRaw("meter_ts  at time zone 'Europe/Warsaw'")
                     .Select("measure_setup_id")
                     .WhereRaw("meter_nr= '" + row["meter_id"]+"'" )
                     .OrderByDesc("meter_ts")
                     .Limit(1);
                    // Console.WriteLine(queryFactory.Compiler.Compile(selectedMaxMeter_ts).Sql);
                     IDictionary<string, object> max = selectedMaxMeter_ts.FirstOrDefault();
                    try
                    {
                         if (max is null)
                         {
                             row["last_measure"] = "Brak odczytu";
                        row["update_ts"] = row["update_ts"].ToString().Replace("T", "");
                        continue;
                        
                         }
                         row["last_measure"] = max["timezone"];
                         row["last_measure"] = row["last_measure"].ToString().Replace("-", ".");
                         row["update_ts"] = row["update_ts"].ToString().Replace("T", "");
                        //Console.WriteLine(row["last_measure"].GetType());
                        if (row["last_measure"] != "Brak odczytu") {

                        DateTime dataMeasureMinus30 = DateTime.ParseExact(dataTimeMinus30, "dd.MM.yyyy hh:mm:ss",
                                       System.Globalization.CultureInfo.InvariantCulture);
                        DateTime lastMeasure = DateTime.ParseExact(row["last_measure"].ToString(), "dd.MM.yyyy hh:mm:ss",
                                       System.Globalization.CultureInfo.InvariantCulture);

                        int result = DateTime.Compare(lastMeasure,dataMeasureMinus30);
                        row["state"]=checkDeviceStatus(result);
                        row["last_measure"] = row["last_measure"].ToString().Replace("T", "");
                        if (row["measure_setup_id"].ToString().Equals(max["measure_setup_id"].ToString()))
                        {
                            latestMeasureSetupId.Add(row);
                        }
                    }
                }
                    catch (Exception e)
                {
                    log.Error(e);
                    //    Console.WriteLine(e);
                }

             };
                return sortedDevices;
        }
    }
}
