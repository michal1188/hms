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
            IEnumerable<Object> selectStringsOfValuesFromDataBase = SelectStringsOfValuesFromDataBase(sortedDevices);
            IEnumerable<Object> sortedDevicesMaxMeter = SelectMaxMeter_ts(selectStringsOfValuesFromDataBase, dateTimeSubstract30MinutesWithFormatToQuery);
            int totalDevices = FetchNumberOfDevice(searchLike);

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

        private int FetchNumberOfDevice(string searchLike)
        {
            SqlKata.Query sortedDevicesQuery = this.queryFactory.Query("iot.device")
                         .SelectRaw("device.device_id,  device.device_model, device.gsm_signal_power, device.device_version")
                         .SelectRaw("device.update_ts at time zone 'Europe/Warsaw'AS update_ts ")
                         .SelectRaw("null AS last_measure")
                         .SelectRaw("null AS meter_id")
                         .SelectRaw("false AS state")
                         .SelectRaw("null AS meteridlist")
                         .SelectRaw("null AS measuresetupidlist")
                         .WhereRaw("((device.device_model Like 'Stratus%' or device.device_model Like 'Cirrus%') and  device.device_id not like 's%')")
                         .Where(q =>
                                     q.WhereLike("device.device_id", @searchLike)
                                      .OrWhereLike("device.device_model", @searchLike)
                                      //.OrWhereLike("measure_setup.meter_id", @searchLike)
                                      //.OrWhereRaw(" CAST(measure_setup.measure_setup_id AS TEXT) LIKE ?", @searchLike)
                                      .OrWhereRaw(" CAST(device.gsm_signal_power AS TEXT) LIKE ?", @searchLike)
                                      .OrWhereRaw(" CAST(device.update_ts AS TEXT) LIKE ?", @searchLike)
                                      .OrWhereLike("device.device_version", @searchLike)
                                 // .OrWhereRaw(" CAST(sub.last_measure AS TEXT) LIKE ?", @searchLike)
                                 )
                      .AsCount();

            
            int totalClient = Convert.ToInt32(sortedDevicesQuery.First().count);

            return totalClient;
        }

        private IEnumerable<Object> FetchSortedDevicesList(string searchLike, string sortBy, bool sortDesc, int rowsPerPage, int offset)
        {
            SqlKata.Query sortedDevicesQuery = this.queryFactory.Query("iot.device") 
                       .SelectRaw("device.device_id,  device.device_model, device.gsm_signal_power, device.device_version")
                       .SelectRaw("device.update_ts at time zone 'Europe/Warsaw'AS update_ts ")
                       .SelectRaw("null AS last_measure")
                       .SelectRaw("null AS meter_id")
                       .SelectRaw("false AS state")
                       .SelectRaw("null AS meteridlist")
                       .SelectRaw("null AS measuresetupidlist")
                       .WhereRaw("((device.device_model Like 'Stratus%' or device.device_model Like 'Cirrus%') and  device.device_id not like 's%')")
                       .Where(q =>
                                   q.WhereLike("device.device_id", @searchLike)
                                    .OrWhereLike("device.device_model", @searchLike)
                                    //.OrWhereLike("measure_setup.meter_id", @searchLike)
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


        private string GetstringOfValues(IEnumerable<Object> sortedDevices, string column)
        {
            string stringOfValues = "";

            foreach (IDictionary<string, object> row in sortedDevices)
            {
               // Console.WriteLine(row); 

                if (row[column] is null)
                {
                    continue;
                }
                else
                {
                    stringOfValues += "'";
                    stringOfValues += row[column].ToString() + "', ";
                }
            }
            if (stringOfValues.Length > 1)
            {
                stringOfValues = stringOfValues.Remove(stringOfValues.Length - 2);

            }
            return stringOfValues;
        }
        private IEnumerable<Object> SelectStringsOfValuesFromDataBase(IEnumerable<Object> sortedDevices )
        {

            SqlKata.Query stringOfMeter_Id;
            SqlKata.Query stringOfMeasure_Setup_Id;
            SqlKata.Query tempmeasure_setup_idlist;
            SqlKata.Query tempmeterlist;
            
            foreach (IDictionary<string, object> row in sortedDevices)
            {
                tempmeterlist = this.queryFactory.Query("iot.measure_setup")
                                                   .Select("measure_setup.meter_id")
                                                   .Join("iot.measure_device_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
                                                   .Join("iot.device_port", "device_port.device_port_id", "measure_device_setup.device_port_id")
                                                   .Join("iot.device", "device.device_id", "device_port.device_id")
                                                   .WhereRaw("device.device_id ='" + row["device_id"] + "'")
                                                   .As("tempmeterlist");
                tempmeasure_setup_idlist = this.queryFactory.Query("iot.measure_setup")
                                                 .Select("measure_setup.measure_setup_id")
                                                 .Join("iot.measure_device_setup", "measure_device_setup.measure_device_setup_id", "measure_setup.measure_device_setup_id")
                                                 .Join("iot.device_port", "device_port.device_port_id", "measure_device_setup.device_port_id")
                                                 .Join("iot.device", "device.device_id", "device_port.device_id")
                                                 .WhereRaw("device.device_id ='" + row["device_id"] + "'")
                                                 .As("tempmeasure_setup_idlist");


                stringOfMeter_Id = this.queryFactory.Query().From(tempmeterlist)
                    .SelectRaw("tempmeterlist.meter_id");
                stringOfMeasure_Setup_Id = this.queryFactory.Query().From(tempmeasure_setup_idlist)
                    .SelectRaw("tempmeasure_setup_idlist.measure_setup_id");

                string meterId=GetstringOfValues(stringOfMeter_Id.Get(), "meter_id");
                string measure_setup_id= GetstringOfValues(stringOfMeasure_Setup_Id.Get(), "measure_setup_id");
               // Console.WriteLine("meterId "+meterId);
              //  Console.WriteLine("measure_setup_id " + measure_setup_id);
               // Console.WriteLine(row["device_id"]);
                try
                {
                     row["meteridlist"] = meterId;
                     row["measuresetupidlist"] = measure_setup_id;

                }
                catch (Exception e)
                {
                    log.Error(e);
                    //    Console.WriteLine(e);
                }

            };
            return sortedDevices;
        }
        private IEnumerable<Object> SelectMaxMeter_ts(IEnumerable<Object> sortedDevices, string dataTimeMinus30)
        {
            SqlKata.Query selectedMaxMeter_ts;
            foreach (IDictionary<string, object> row in sortedDevices)
            {
                if (row["meteridlist"].ToString() != "" || row["measuresetupidlist"].ToString() != "")
                {
                    selectedMaxMeter_ts = this.queryFactory.Query("iot.measurement_electricity")
                    .SelectRaw("meter_ts  at time zone 'Europe/Warsaw'")
                    .Select("measurement_electricity.measure_setup_id", "meter_nr")
                    .WhereRaw("measurement_electricity.meter_nr in(" + row["meteridlist"] + ") and measurement_electricity.measure_setup_id in (" + row["measuresetupidlist"] + ")")
                    .OrderByDesc("meter_ts")
                    .Limit(1);
                    Console.WriteLine(queryFactory.Compiler.Compile(selectedMaxMeter_ts).Sql);

                    IDictionary<string, object> max = selectedMaxMeter_ts.FirstOrDefault();
                    try
                    {
                        if (max is null)
                        {
                            row["last_measure"] = "Brak odczytu";
                            row["update_ts"] = row["update_ts"].ToString().Replace("T", "");
                            continue;

                        }
                        row["meter_id"] = max["meter_nr"];
                        row["last_measure"] = max["timezone"];
                        row["last_measure"] = row["last_measure"].ToString().Replace("-", ".");
                        row["update_ts"] = row["update_ts"].ToString().Replace("T", "");
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

                }
                else {
                    row["meter_id"] = "Brak Licznika";
                    row["last_measure"] = "Brak Odczytu";
                    row["update_ts"] = row["update_ts"].ToString().Replace("T", "");

                }
            };
            return sortedDevices;


        }
    }
      }
    