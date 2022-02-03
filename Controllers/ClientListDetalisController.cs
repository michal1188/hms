using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Npgsql;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Linq;
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

            IEnumerable<Object> clientMetersList = ClientMetersList(clientID);
            string clientMetersString = ClientMeteresString(clientID);
            DateTime dateTimeNow = DateTime.Now;
            DateTime dateTimeSubstract30Minutes = dateTimeNow.AddMinutes(-30);
            string dateTimeSubstract30MinutesWithFormatToQuery = dateTimeSubstract30Minutes.ToString("dd.MM.yyyy hh:mm:ss");
            IEnumerable<Object> sortedDevicesMaxMeter = SelectMaxMeter_ts(clientMetersList, dateTimeSubstract30MinutesWithFormatToQuery);

             IEnumerable<Object> sortedClientDevices = this.FetchSortedClientDevicesList(sortedDevicesMaxMeter, clientMetersString, searchLike, sortBy, sortDesc,  rowsPerPage,  offset);
               int totalDevices = sortedClientDevices.Count();
            //   IEnumerable<Object> listDevicesWithSingleMeter = GetLatestMeter_id(sortedDevicesMaxMeter);

            object returnedList = new { sortedClientDevices = sortedClientDevices, totalDevices = totalDevices };
            return Ok(returnedList);
        }

        private string ClientMeteresString(int clientID)
        {
            string meters = "";
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
                if (row["device_serial"] is null)
                {
                    continue;
                }
                else
                {
                    meters += "'";
                    meters += row["device_serial"].ToString() + "', ";
                }
            }


            meters = meters.Remove(meters.Length - 2);
            Console.WriteLine(meters);

            return meters;
        }
        private IEnumerable<Object> ClientMetersList(int clientID) {

            SqlKata.Query clientMetersListQuery = this.dbReplika.Query("public.locations")
              .Select("device_serial as meter_id")
              .SelectRaw("null AS last_measure")
              .SelectRaw("null AS measure_setup_id")
              .SelectRaw("false AS state")
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
            return clientMetersList;

        }

        private IEnumerable<Object> FetchSortedClientDevicesList(IEnumerable<Object> maxMeter_tsList, string clientMeters, string searchLike, string sortBy, bool sortDesc, int rowsPerPage, int offset)
        {
            SqlKata.Query sortedDevicesQuery = this.dbToGeneral.Query("iot.device")
                       .SelectRaw("device.device_id,  device.device_model, device.gsm_signal_power, device.device_version, measure_setup.meter_id, measure_setup.measure_setup_id")
                       .SelectRaw("device.update_ts at time zone 'Europe/Warsaw'AS update_ts ")
                       .SelectRaw("null AS last_measure")
                       .SelectRaw("false AS state")
                       .Join("iot.device_port", "device.device_id", "device_port.device_id")
                       .Join("iot.measure_device_setup", "device_port.device_port_id", "measure_device_setup.device_port_id")
                       .Join("iot.measure_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
                       //.WhereRaw("(device.device_model Like 'Stratus%' or device.device_model Like 'Cirrus%')")
                       .WhereRaw("(measure_setup.meter_id in(" + @clientMeters + ") and measure_setup.measure_device_setup_id not like 'Santa%' and measure_setup.measure_device_setup_id not like 't%'and measure_setup.measure_device_setup_id not like 'sy%')")
                       .Where(q =>
                                   q.WhereLike("device.device_id", @searchLike)
                                    .OrWhereLike("device.device_model", @searchLike)
                                    .OrWhereLike("measure_setup.meter_id", @searchLike)
                                    //.OrWhereRaw(" CAST(measure_setup.measure_setup_id AS TEXT) LIKE ?", @searchLike)
                                    .OrWhereRaw(" CAST(device.gsm_signal_power AS TEXT) LIKE ?", @searchLike)
                                    .OrWhereRaw(" CAST(device.update_ts AS TEXT) LIKE ?", @searchLike)
                                    .OrWhereLike("device.device_version", @searchLike)
                               // .OrWhereRaw(" CAST(sub.last_measure AS TEXT) LIKE ?", @searchLike)
                               );
  
          //  Console.WriteLine(dbToGeneral.Compiler.Compile(sortedDevicesQuery).Sql);
            if (sortDesc == false) { sortedDevicesQuery.OrderBy(sortBy); }
            else { sortedDevicesQuery.OrderByDesc(sortBy); }
            IList<Object> clientDevicesList = new List<object>();
            IEnumerable<Object> sortedDevices = new List<object>();
                try
            {
                sortedDevices = sortedDevicesQuery.Get();

                foreach (IDictionary<string, object> rowDevice in sortedDevices)
                {
                    foreach (IDictionary<string, object> rowMeasure in maxMeter_tsList)
                    {
                       // Console.WriteLine("1" + rowDevice["meter_id"]);
                        //Console.WriteLine("2" + rowDevice[ "measure_setup_id"]);
                        //Console.WriteLine("3" + rowMeasure[ "meter_id"]);
                        //Console.WriteLine("4" + rowMeasure[ "measure_setup_id"]);
                        if (rowDevice["meter_id"].ToString() == rowMeasure["meter_id"].ToString() && rowDevice["measure_setup_id"].ToString() == rowMeasure["measure_setup_id"].ToString())
                        {
                            rowDevice["state"] = rowMeasure["state"];
                            rowDevice["last_measure"] = rowMeasure["last_measure"];
                            rowDevice["last_measure"] = rowDevice["last_measure"].ToString().Replace("-", ".");
                            if (rowDevice["update_ts"] != null) { rowDevice["update_ts"] = rowDevice["update_ts"].ToString().Replace("T", ""); }
                            clientDevicesList.Add(rowDevice);
                        }
                        else { continue; }
                    }
                }

            }
            catch (Exception e)
            {
                log.Error(e);
            }
      
            var clientDevices = clientDevicesList.Skip(offset).Take(rowsPerPage).ToList();
            return clientDevices;
        }



        private bool CheckDeviceStatus(int status)
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

      

        private IEnumerable<Object> SelectMaxMeter_ts(IEnumerable<Object> clientMetersList, string dataTimeMinus30)
        {
            SqlKata.Query selectedMaxMeter_ts;
           // IList<Object> latestMeasureSetupId = new List<object>();
            foreach (IDictionary<string, object> row in clientMetersList)
            {
                selectedMaxMeter_ts = this.dbToGeneral.Query("iot.measurement_electricity")
                .SelectRaw("meter_ts  at time zone 'Europe/Warsaw'")
                .Select("measure_setup_id")
                .WhereRaw("meter_nr= '" + row["meter_id"] + "'")
                .OrderByDesc("meter_ts")
                .Limit(1);
                // Console.WriteLine(queryFactory.Compiler.Compile(selectedMaxMeter_ts).Sql);
                IDictionary<string, object> max = selectedMaxMeter_ts.FirstOrDefault();
                
                try
                {
                    if (row["meter_id"] is null) { row["meter_id"] = "Brak danych"; }
                    if (max is null)
                        {
                            row["last_measure"] = "Brak odczytu";
                            row["measure_setup_id"] = "Brak danych";
                         continue;

                        }
                        row["last_measure"] = max["timezone"];
                        row["measure_setup_id"] = max["measure_setup_id"];
                        row["last_measure"] = row["last_measure"].ToString().Replace("-", ".");
                        if (row["last_measure"] != "Brak odczytu")
                        {
                       // Console.WriteLine(row["last_measure"]);
                        DateTime dataMeasureMinus30 = DateTime.ParseExact(dataTimeMinus30, "dd.MM.yyyy hh:mm:ss",
                                           System.Globalization.CultureInfo.InvariantCulture);
                            DateTime lastMeasure = DateTime.ParseExact(row["last_measure"].ToString(), "dd.MM.yyyy hh:mm:ss",
                                           System.Globalization.CultureInfo.InvariantCulture);
                            int result = DateTime.Compare(lastMeasure, dataMeasureMinus30);
                            row["state"] = CheckDeviceStatus(result);
                            row["last_measure"] = row["last_measure"].ToString().Replace("T", "");
                        }  
                }
                catch (Exception e)
                {
                    log.Error(e);
                }

            };
           return clientMetersList;
        }

        
    }
}
