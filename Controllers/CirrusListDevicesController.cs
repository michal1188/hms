using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;

namespace HMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CirrusListDevicesController : ControllerBase
    {
 

        private readonly QueryFactory db;

        private readonly ILog log = LogManager.GetLogger("mylog");
        public CirrusListDevicesController(QueryFactory db)
        {
            this.db = db;
           

        }


        [HttpPost]
        public IActionResult DevicesList(JsonElement parameters)
        {

            dynamic json = JsonConvert.DeserializeObject(parameters.ToString());

            
                        int pageNumber;
                        int rowsPerPage;
                        string sortBy;
                        bool sortDesc;

                        pageNumber = (json.pageNumber != null) ? json.pageNumber : 1;
                        rowsPerPage = (json.rowsPerPage != null) ? json.rowsPerPage : 10;
                        sortBy = (json.sortBy != null) ? json.sortBy : "device_id";
                        sortDesc = (json.sortDesc != null) ? json.sortDesc : false;


                        string searchFromJson = json.search;

                        int offset = pageNumber * rowsPerPage - rowsPerPage;
                        string searchLike = "%" + searchFromJson + "%";


            string meter_nr = "001135";
            IEnumerable<Object> test = this.SelectMaxMeter_ts(meter_nr);
            object returnedList = new { test = test };

            
            return Ok(returnedList);
        }

 


        private int FetchNumberOfPpe(string searchLike)
        {
            int totalPpe = Convert.ToInt32(this.db.Query("public.locations")
                .Where(q =>
                    q.WhereLike("locations.name", @searchLike)
                    .OrWhereRaw(" CAST(locations.id AS TEXT) LIKE ?", @searchLike)
                    .OrWhereLike("locations.device_serial", @searchLike)
                    .OrWhereLike("locations.custom_label", @searchLike)
                )
                .AsCount().First().count);

            return totalPpe;
        }

        private IEnumerable<Object> FetchSortedPpeList(string searchLike, string sortBy, bool sortDesc, int rowsPerPage, int offset)
        {
            SqlKata.Query sortedPpeQuery = this.db.Query("iot.device")
              .Select("device.id","", "device.device_model", "device.gsm_state", "device.gsm_signal_power","device.update_ts", "device.device_version")
              .SelectRaw("null AS last_measure")
              .Join("iot.device_port","device.device_id","device_port.device_id")
              .Join("iot.measure_device_setup","device_port.device_port_id", "measure_device_setup.device_port_id")
              .Join("iot.measure_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
              //tutaj join z measureelctricity ostatni pomiar
              .WhereRaw("device.device_model LIKE 'Stratus%'")
              .GroupBy("device.device_id")
              .GroupBy("measure_setup.meter_id")

              .Limit(rowsPerPage)
              .Offset(offset);

            if (sortDesc == false) { sortedPpeQuery.OrderBy(sortBy); }
            else { sortedPpeQuery.OrderByDesc(sortBy); }


            //Console.WriteLine(db.Compiler.Compile(sortedPpeQuery).Sql);

            IEnumerable<Object> sortedPpe = new List<object>();
            try
            {
                sortedPpe = sortedPpeQuery.Get();
            }
            catch (Exception e)
            {
                log.Error(e);
            }

            return sortedPpe;
        }
        private IEnumerable<Object> SelectMaxMeter_ts(string meterName)
        {
            SqlKata.Query selectMaxMeter_tsQuery = this.db.Query("iot.measurement_electricity")
               //.Select("meter_nr")
               .SelectRaw("max(meter_ts)")
               //.GroupBy("meter_nr")
               .WhereRaw("meter_nr='" + meterName + "'");
               //.OrderByDesc("meter_ts")
               //.Limit(1);
               
               

            IEnumerable<Object> xx = new List<Object>(); try
            {
                xx = selectMaxMeter_tsQuery.Get();
            }
            catch (Exception e) { log.Error(e); }


            return selectMaxMeter_tsQuery.Get();
        }


    }
}
