﻿using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlKata;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
            
            IEnumerable<Object> sortedDevices = this.FetchSortedDevicesList(searchLike, sortBy, sortDesc, rowsPerPage, offset);
            IEnumerable<Object> sortedDevicesMaxMeter = SelectMaxMeter_ts(sortedDevices);

           object returnedList = new { sortedDevicesMaxMeter = sortedDevicesMaxMeter, totalDevices = totalDevices };

            return Ok(returnedList);
        }
        
        private int FetchNumberOfDevices(string searchLike)
        {
   
            SqlKata.Query queryCount = this.queryFactory.Query("iot.device")
                       .SelectRaw("DISTINCT ON(device.device_id, measure_setup.meter_id) 1 as count")
                       .Join("iot.device_port", "device.device_id", "device_port.device_id")
                       .Join("iot.measure_device_setup", "device_port.device_port_id", "measure_device_setup.device_port_id")
                       .Join("iot.measure_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
                       .WhereRaw("device.device_model LIKE 'Stratus%' ")
                       .Where(q =>
                                   q.WhereLike("device.device_id", @searchLike)
                                    .OrWhereLike("device.device_model", @searchLike)
                                    .OrWhereLike("measure_setup.meter_id", @searchLike)
                                   // .OrWhereRaw(" CAST(measure_setup.measure_setup_id AS TEXT) LIKE ?", @searchLike)
                                    .OrWhereRaw(" CAST(device.gsm_signal_power AS TEXT) LIKE ?", @searchLike)
                                    .OrWhereRaw(" CAST(device.gsm_state AS TEXT) LIKE ?", @searchLike)
                                    .OrWhereRaw(" CAST(device.update_ts AS TEXT) LIKE ?", @searchLike)
                                    .OrWhereLike("device.device_version", @searchLike)
                               // .OrWhereRaw(" CAST(sub.last_measure AS TEXT) LIKE ?", @searchLike)
                               );

            //Console.WriteLine(queryFactory.Compiler.Compile(queryCount).Sql);
            IEnumerable<object> totalPpe = queryCount.Get();

            return totalPpe.Count();
        }

        private IEnumerable<Object> FetchSortedDevicesList(string searchLike, string sortBy, bool sortDesc, int rowsPerPage, int offset)
        { 
            SqlKata.Query sortedDevicesQuery = this.queryFactory.Query("iot.device")
                       //.SelectRaw("DISTINCT ON(device.device_id, measure_setup.meter_id) device.device_id,  device.device_model, device.gsm_state, device.gsm_signal_power, device.device_version, measure_setup.measure_setup_id, measure_setup.meter_id")
                       .SelectRaw("DISTINCT ON(device.device_id, measure_setup.meter_id) device.device_id,  device.device_model, device.gsm_state, device.gsm_signal_power, device.device_version, measure_setup.meter_id")
                       .SelectRaw("device.update_ts at time zone 'Europe/Warsaw'AS update_ts ")
                       .SelectRaw("null AS last_measure")
                       .Join("iot.device_port", "device.device_id", "device_port.device_id")
                       .Join("iot.measure_device_setup", "device_port.device_port_id", "measure_device_setup.device_port_id")
                       .Join("iot.measure_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
                       .WhereRaw("device.device_model LIKE 'Stratus%' ")
                       .Where(q =>
                                   q.WhereLike("device.device_id", @searchLike)
                                    .OrWhereLike("device.device_model", @searchLike)
                                    .OrWhereLike("measure_setup.meter_id", @searchLike)
                                    //.OrWhereRaw(" CAST(measure_setup.measure_setup_id AS TEXT) LIKE ?", @searchLike)
                                    .OrWhereRaw(" CAST(device.gsm_signal_power AS TEXT) LIKE ?", @searchLike)
                                    .OrWhereRaw(" CAST(device.gsm_state AS TEXT) LIKE ?", @searchLike)
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

        private IEnumerable<Object> SelectMaxMeter_ts(IEnumerable<Object> sortedDevices)
        {
            SqlKata.Query selectedMaxMeter_ts;

                 foreach (IDictionary<string, object> row in sortedDevices)
                 {
                     //W przypadku przypisywania wartosci po meter_id dla Stratus i Cumulusa gdy na danym liczniku jest podpięte kolejne urządzenie to dla starych urzadzeniu
                     //które były na nym liczniku zaczytywana jest data najnowszego pomiaru jaki został wykonany na urządzeniu które jest obecnie podłączone  
                     //Druga opcja łaczenie po measure_setup_id 
                     selectedMaxMeter_ts = this.queryFactory.Query("iot.measurement_electricity")
                     .SelectRaw("meter_ts  at time zone 'Europe/Warsaw'")
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
                             continue;
                         }
                         row["last_measure"] = max["timezone"];

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
