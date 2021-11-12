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

        private IEnumerable<Object> SelectMaxMeter_ts(string meterName)
        {
            SqlKata.Query selectMaxMeter_tsQuery = this.db.Query("iot.measurement_electricity")
               //.Select("meter_nr")
               .SelectRaw("max(meter_ts)")
               //.SelectRaw("max(meter_ts)")
               //.GroupBy("meter_nr")
               .WhereRaw("meter_nr='" + meterName + "'");
              // .OrderByDesc("meter_ts")
               ///.Limit(1);
               
               

            IEnumerable<Object> xx = new List<Object>(); try
            {
                xx = selectMaxMeter_tsQuery.Get();
            }
            catch (Exception e) { log.Error(e); }


            return selectMaxMeter_tsQuery.Get();
        }


    }
}
