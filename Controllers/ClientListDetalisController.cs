﻿using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Npgsql;
using SqlKata.Compilers;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HMS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientListDetalisController : ControllerBase
    {
         static string connectionToReplika = Security.GetSecondDatabaseCredentials();
         static string connectionToGeneral = Security.GetDatabaseCredentials();
        
        private readonly QueryFactory dbReplika = new QueryFactory
        {
            Connection = new NpgsqlConnection(connectionToReplika.Trim()),
            Compiler = new PostgresCompiler()
        };
        
        
        private readonly QueryFactory dbToGeneral = new QueryFactory
        {
            Connection = new NpgsqlConnection(connectionToGeneral.Trim()),
            Compiler = new PostgresCompiler()
        };


        private readonly ILog log = LogManager.GetLogger("mylog");
        

        public ClientListDetalisController(QueryFactory dbReplika, QueryFactory dbToGeneral)
        {
            this.dbReplika = dbReplika;
            this.dbToGeneral = dbToGeneral;
        }


        [HttpPost]
        public IActionResult ClientListDetalis(JsonElement parameters)
        {

            dynamic json = JsonConvert.DeserializeObject(parameters.ToString());

            int clientID = json.clientID;
    
            int pageNumber = (json.pageNumber != null) ? json.pageNumber : 1;
            int rowsPerPage = (json.rowsPerPage != null) ? json.rowsPerPage : 10;
            string sortBy = (json.sortBy != null) ? json.sortBy : "device_id";
            bool sortDesc = (json.sortDesc != null) ? json.sortDesc : false;

            string searchFromJson = json.search;
            int offset = pageNumber * rowsPerPage - rowsPerPage;
            string searchLike = "%" + searchFromJson + "%";

            searchLike = searchLike.Replace(";", "");
            searchLike = searchLike.ToLower();

            string clientMetersList = ClientMetersList(clientID);
            int totalDevices = this.FetchNumberOfClientDevices(clientMetersList,searchLike);

            DateTime dateTimeNow = DateTime.Now;
            DateTime dateTimeSubstract30Minutes = dateTimeNow.AddMinutes(-30);
            string dateTimeSubstract30MinutesWithFormatToQuery = dateTimeSubstract30Minutes.ToString("dd.MM.yyyy hh:mm:ss");

            IEnumerable<Object> sortedClientDevices = this.FetchSortedClientDevicesList(clientMetersList, searchLike, sortBy, sortDesc, rowsPerPage, offset);
            IEnumerable<Object> sortedDevicesMaxMeter = SelectMaxMeter_ts(sortedClientDevices, dateTimeSubstract30MinutesWithFormatToQuery);

            object returnedList = new { sortedClientDevices = sortedDevicesMaxMeter, totalDevices = totalDevices };
            return Ok(returnedList);
        }
        private int FetchNumberOfClientDevices(string clientMeters, string searchLike)
        {
            SqlKata.Query queryCount = this.dbToGeneral.Query("iot.device")
                       .SelectRaw("DISTINCT ON(device.device_id, measure_setup.meter_id) 1 as count")
                       //.Select("device.device_model")
                       .Join("iot.device_port", "device.device_id", "device_port.device_id")
                       .Join("iot.measure_device_setup", "device_port.device_port_id", "measure_device_setup.device_port_id")
                       .Join("iot.measure_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
                       .WhereRaw("(measure_setup.meter_id in("+ @clientMeters + "))")
                       .Where(q =>
                                   q.WhereLike("device.device_id", @searchLike)
                                    .OrWhereLike("device.device_model", @searchLike)
                                    .OrWhereLike("measure_setup.meter_id", @searchLike)
                                    // .OrWhereRaw(" CAST(measure_setup.measure_setup_id AS TEXT) LIKE ?", @searchLike)
                                    .OrWhereRaw(" CAST(device.gsm_signal_power AS TEXT) LIKE ?", @searchLike)
                                    .OrWhereRaw(" CAST(device.update_ts AS TEXT) LIKE ?", @searchLike)
                                    .OrWhereLike("device.device_version", @searchLike)
                               // .OrWhereRaw(" CAST(sub.last_measure AS TEXT) LIKE ?", @searchLike)
                               )
                       .AsCount(); ;

            //Console.WriteLine(dbToGeneral.Compiler.Compile(queryCount).Sql);
            int totalClient = Convert.ToInt32(queryCount.First().count);

            return totalClient;
        }

        private IEnumerable<Object> FetchSortedClientDevicesList(string clientMeters, string searchLike, string sortBy, bool sortDesc, int rowsPerPage, int offset)
        {
            SqlKata.Query sortedDevicesQuery = this.dbToGeneral.Query("iot.device")
                       //.SelectRaw("DISTINCT ON(device.device_id, measure_setup.meter_id) device.device_id,  device.device_model, device.gsm_state, device.gsm_signal_power, device.device_version, measure_setup.measure_setup_id, measure_setup.meter_id")
                       .SelectRaw("DISTINCT ON(device.device_id, measure_setup.meter_id,  device.gsm_signal_power, device.device_version, device.device_model, update_ts) device.device_id,  device.device_model, device.gsm_signal_power, device.device_version, measure_setup.meter_id")
                       .SelectRaw("device.update_ts at time zone 'Europe/Warsaw'AS update_ts ")
                       .SelectRaw("null AS last_measure")
                       .SelectRaw("false AS state")
                       .Join("iot.device_port", "device.device_id", "device_port.device_id")
                       .Join("iot.measure_device_setup", "device_port.device_port_id", "measure_device_setup.device_port_id")
                       .Join("iot.measure_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
                      //.WhereRaw("(device.device_model Like 'Stratus%' or device.device_model Like 'Cirrus%')")
                       .WhereRaw("(measure_setup.meter_id in(" + @clientMeters + "))")
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
            Console.WriteLine(dbToGeneral.Compiler.Compile(sortedDevicesQuery).Sql);


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

        private string  ClientMetersList(int clientID)
        {
            string meters="";
            SqlKata.Query clientMetersListQuery = this.dbReplika.Query("public.locations")
           .Select("device_serial")
           .Where("client_id", @clientID);

            IEnumerable<Object> clientMetersList = new List<object>();
            try
            {
                clientMetersList = clientMetersListQuery.Get();
            }
            catch (Exception e)
            {
                log.Error(e);
            }

            foreach (IDictionary<string, object> row in clientMetersList)
            {
                meters += "'";
                meters += row["device_serial"].ToString()+"', ";
            }


            meters = meters.Remove(meters.Length - 2);
            Console.WriteLine(meters);

            return meters;
        }



        private IEnumerable<Object> SelectMaxMeter_ts(IEnumerable<Object> sortedDevices, string dataTimeMinus30)
        {
            SqlKata.Query selectedMaxMeter_ts;

            foreach (IDictionary<string, object> row in sortedDevices)
            {
                //W przypadku przypisywania wartosci po meter_id dla Stratus i Cumulusa gdy na danym liczniku jest podpięte kolejne urządzenie to dla starych urzadzeniu
                //które były na nym liczniku zaczytywana jest data najnowszego pomiaru jaki został wykonany na urządzeniu które jest obecnie podłączone  
                //Druga opcja łaczenie po measure_setup_id 
                selectedMaxMeter_ts = this.dbToGeneral.Query("iot.measurement_electricity")
                .SelectRaw("meter_ts  at time zone 'Europe/Warsaw'")
                .WhereRaw("meter_nr= '" + row["meter_id"] + "'")
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
                    if (row["update_ts"] != null) { row["update_ts"] = row["update_ts"].ToString().Replace("T", ""); }
                    
                    //Console.WriteLine(row["last_measure"].GetType());
                    if (row["last_measure"] != "Brak odczytu")
                    {

                        DateTime dataMeasureMinus30 = DateTime.ParseExact(dataTimeMinus30, "dd.MM.yyyy hh:mm:ss",
                                       System.Globalization.CultureInfo.InvariantCulture);
                        DateTime lastMeasure = DateTime.ParseExact(row["last_measure"].ToString(), "dd.MM.yyyy hh:mm:ss",
                                       System.Globalization.CultureInfo.InvariantCulture);

                        int result = DateTime.Compare(lastMeasure, dataMeasureMinus30);
                        row["state"] = checkDeviceStatus(result);
                        row["last_measure"] = row["last_measure"].ToString().Replace("T", "");
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
