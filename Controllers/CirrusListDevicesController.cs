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
                        sortBy = (json.sortBy != null) ? json.sortBy : "sub.device_id";
                        sortDesc = (json.sortDesc != null) ? json.sortDesc : false;

                        string searchFromJson = json.search;
                        int offset = pageNumber * rowsPerPage - rowsPerPage;
                        string searchLike = "%" + searchFromJson + "%";
                       
                        searchLike = searchLike.Replace(";", "");
                        
                        int totalPpe = this.FetchNumberOfDevices(searchLike);
                        IEnumerable<Object> sortedDevices = SelectMaxMeter_ts(this.FetchSortedDevicesList(searchLike, sortBy, sortDesc, rowsPerPage, offset));


            //string meter_nr = "001135";
           // IEnumerable<Object> test = this.SelectMaxMeter_ts(meter_nr);
            object returnedList = new { sortedDevices = sortedDevices, totalPpe= totalPpe };

            
            return Ok(returnedList);
        }

 


        private int FetchNumberOfDevices(string searchLike)
        {
            SqlKata.Query queryCount = this.db.Query()
                .From(subQuery => subQuery.Select("device.device_id", "measure_setup.meter_id", "device.device_model", "device.gsm_state", "device.gsm_signal_power", "device.device_version")
                        .SelectRaw("device.update_ts at time zone 'Europe/Warsaw'AS update_ts ")
                        .SelectRaw("null AS last_measure")
                        .From("iot.device")
                        .Join("iot.device_port", "device.device_id", "device_port.device_id")
                        .Join("iot.measure_device_setup", "device_port.device_port_id", "measure_device_setup.device_port_id")
                        .Join("iot.measure_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
                        .GroupBy("device.device_id")
                        .GroupBy("measure_setup.meter_id")
                        .WhereRaw("device.device_model LIKE 'Stratus%' ")
                        
                        .As("sub")).Where(q =>
                                    q.WhereLike("sub.device_id", @searchLike)
                                     .OrWhereLike("sub.device_model", @searchLike)
                                     .OrWhereLike("sub.meter_id", @searchLike)
                                     .OrWhereRaw(" CAST(sub.gsm_signal_power AS TEXT) LIKE ?", @searchLike)
                                     .OrWhereRaw(" CAST(sub.update_ts AS TEXT) LIKE ?", @searchLike)
                                     .OrWhereLike("sub.device_version", @searchLike)
                                     .OrWhereRaw(" CAST(sub.last_measure AS TEXT) LIKE ?", @searchLike)
                                )

                .AsCount();
                        
            //Console.WriteLine(db.Compiler.Compile(zapytanie).Sql);
            int totalPpe = Convert.ToInt32(queryCount.First().count);
            
            return totalPpe;
        }
        
        private IEnumerable<Object> FetchSortedDevicesList(string searchLike, string sortBy, bool sortDesc, int rowsPerPage, int offset)
        {
            SqlKata.Query sortedDevicesQuery = this.db.Query().
                From(subQuery => subQuery
                    .From("iot.device")
                    .Select("device.device_id", "measure_setup.meter_id", "device.device_model", "device.gsm_state", "device.gsm_signal_power", "device.device_version")
                    .SelectRaw("device.update_ts at time zone 'Europe/Warsaw' AS update_ts")
                    .SelectRaw("null AS last_measure")
                    .Join("iot.device_port", "device.device_id", "device_port.device_id")
                    .Join("iot.measure_device_setup", "device_port.device_port_id", "measure_device_setup.device_port_id")
                    .Join("iot.measure_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
                    .GroupBy("device.device_id")
                    .GroupBy("measure_setup.meter_id")
                    .WhereRaw("device.device_model LIKE 'Stratus%' ")
                    .As("sub"))
                        .Where(q =>           
                                      q.WhereLike("sub.device_id", @searchLike)
                                     .OrWhereLike("sub.device_model", @searchLike)
                                     .OrWhereLike("sub.meter_id", @searchLike)
                                     .OrWhereRaw(" CAST(sub.gsm_signal_power AS TEXT) LIKE ?", @searchLike)
                                     .OrWhereRaw(" CAST(sub.update_ts AS TEXT) LIKE ?", @searchLike)
                                     .OrWhereLike("sub.device_version", @searchLike)
                                     .OrWhereRaw(" CAST( sub.last_measure AS TEXT) LIKE ?", @searchLike)
                                )
            
              .Limit(rowsPerPage)
              .Offset(offset); ;

           if (sortDesc == false) { sortedDevicesQuery.OrderBy(sortBy); }
           else { sortedDevicesQuery.OrderByDesc(sortBy); }


            //Console.WriteLine(db.Compiler.Compile(sortedPpeQuery).Sql);

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


  private IEnumerable<Object> SelectMaxMeter_ts(IEnumerable<Object> sortedDevices)
         {
            SqlKata.Query selectedMaxMeter_ts;
           // List<object> selectedMaxMeter_tsList = new List<object>();


            foreach (IDictionary<string, object> row in sortedDevices)
             {
                selectedMaxMeter_ts=this.db.Query("iot.measurement_electricity")
              //.Select("meter_nr")
                .SelectRaw("max(meter_ts at time zone 'Europe/Warsaw')")
              //.GroupBy("meter_nr")
                .WhereRaw("meter_nr='" + row["meter_id"] + "'");
                //.OrderByDesc("meter_ts")
                //.Limit(1);
            
               IDictionary<string,object> max = selectedMaxMeter_ts.First();
                row["last_measure"] = ((DateTime)max["max"]);
            };
                        
            return sortedDevices;
         }
  


    }
}
