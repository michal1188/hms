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
            int totalDevices = this.FetchNumberOfDevices(searchLike);
            
            IEnumerable<Object> sortedDevices1 = this.FetchSortedDevicesList(searchLike, sortBy, sortDesc, rowsPerPage, offset);
            IEnumerable<Object> sortedDevices = SelectMaxMeter_ts(sortedDevices1);

           object returnedList = new { sortedDevices = sortedDevices, totalDevices = totalDevices };

            return Ok(returnedList);
        }




        private int FetchNumberOfDevices(string searchLike)
        {    /*
            SqlKata.Query queryCount = this.queryFactory.Query()
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
                                      .OrWhereRaw(" CAST(sub.gsm_state AS TEXT) LIKE ?", @searchLike)
                                     .OrWhereRaw(" CAST(sub.update_ts AS TEXT) LIKE ?", @searchLike)
                                     .OrWhereLike("sub.device_version", @searchLike)
                                     .OrWhereRaw(" CAST(sub.last_measure AS TEXT) LIKE ?", @searchLike)
                                )

                .AsCount();
            */
            SqlKata.Query queryCount = this.queryFactory.Query("iot.device")
                        .Select("device.device_id", "measure_setup.meter_id", "device.device_model", "device.gsm_state", "device.gsm_signal_power", "device.device_version")
                                .SelectRaw("device.update_ts at time zone 'Europe/Warsaw'AS update_ts ")
                                .SelectRaw("null AS last_measure")
                                .Join("iot.device_port", "device.device_id", "device_port.device_id")
                                .Join("iot.measure_device_setup", "device_port.device_port_id", "measure_device_setup.device_port_id")
                                .Join("iot.measure_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
                                .GroupBy("device.device_id")
                                .GroupBy("measure_setup.meter_id")
                               .WhereRaw("device.device_model LIKE 'Stratus%' ")
                                .Where(q =>
                                            q.WhereLike("device.device_id", @searchLike)
                                             .OrWhereLike("device.device_model", @searchLike)
                                             .OrWhereLike("measure_setup.meter_id", @searchLike)
                                             .OrWhereRaw(" CAST(device.gsm_signal_power AS TEXT) LIKE ?", @searchLike)
                                              .OrWhereRaw(" CAST(device.gsm_state AS TEXT) LIKE ?", @searchLike)
                                             .OrWhereRaw(" CAST(device.update_ts AS TEXT) LIKE ?", @searchLike)
                                             .OrWhereLike("device.device_version", @searchLike)
                                             //.OrWhereRaw(" CAST(last_measure AS TEXT) LIKE ?", @searchLike)
                                        ).AsCount();    
            //Console.WriteLine(db.Compiler.Compile(zapytanie).Sql);
            int totalPpe = Convert.ToInt32(queryCount.First().count);

            return totalPpe;
        }

        private IEnumerable<Object> FetchSortedDevicesList(string searchLike, string sortBy, bool sortDesc, int rowsPerPage, int offset)
        {
            /*  SqlKata.Query sortedDevicesQuery = this.queryFactory.Query().
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
                                       .OrWhereRaw(" CAST(sub.gsm_state AS TEXT) LIKE ?", @searchLike)
                                       .OrWhereRaw(" CAST(sub.update_ts AS TEXT) LIKE ?", @searchLike)
                                       .OrWhereLike("sub.device_version", @searchLike)
                                       .OrWhereRaw(" CAST( sub.last_measure AS TEXT) LIKE ?", @searchLike)
                                  )

                .Limit(rowsPerPage)
                .Offset(offset); 
            */

            SqlKata.Query sortedDevicesQuery = this.queryFactory.Query("iot.device")            
                    .Select("device.device_id", "measure_setup.meter_id", "device.device_model", "device.gsm_state", "device.gsm_signal_power", "device.device_version")
                    .SelectRaw("device.update_ts at time zone 'Europe/Warsaw' AS update_ts")
                    .SelectRaw("null AS last_measure")
                    .Join("iot.device_port", "device.device_id", "device_port.device_id")
                    .Join("iot.measure_device_setup", "device_port.device_port_id", "measure_device_setup.device_port_id")
                    .Join("iot.measure_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
                    .GroupBy("device.device_id")
                    .GroupBy("measure_setup.meter_id")
                    .WhereRaw("device.device_model LIKE 'Stratus%' ")
                        .Where(q =>
                                      q.WhereLike("device.device_id", @searchLike)
                                     .OrWhereLike("device.device_model", @searchLike)
                                     .OrWhereLike("measure_setup.meter_id", @searchLike)
                                     .OrWhereRaw(" CAST(device.gsm_signal_power AS TEXT) LIKE ?", @searchLike)
                                     .OrWhereRaw(" CAST(device.gsm_state AS TEXT) LIKE ?", @searchLike)
                                     .OrWhereRaw(" CAST(device.update_ts AS TEXT) LIKE ?", @searchLike)
                                     .OrWhereLike("device.device_version", @searchLike)
                                     //Czy na pewno ma nie wyszukiwac po ostatnim pomiarze? 
                                     //.OrWhereRaw(" CAST( sub.last_measure AS TEXT) LIKE ?", @searchLike)
                                )

              .Limit(rowsPerPage)
              .Offset(offset);
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

            // sprobuj zrobić z where in
            foreach (IDictionary<string, object> row in sortedDevices)
            {
                //Console.WriteLine("obrot");
                // Console.WriteLine(DateTime.Now);
                selectedMaxMeter_ts = this.queryFactory.Query("iot.measurement_electricity")
                .Select("meter_nr")
                //.SelectRaw("max(meter_ts at time zone 'Europe/Warsaw')")
                //.GroupBy("meter_nr")
                .WhereRaw("meter_nr='" + row["meter_id"] + "'")
                .OrderByDesc("meter_ts")
                .Limit(1);

               try
                {
                    string max = selectedMaxMeter_ts.First().ToString();
                                if (row["last_measure"] is null)
                {
                    row["last_measure"] = "Brak odczytu";
                    continue;
                    }
                row["last_measure"] =max;

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
