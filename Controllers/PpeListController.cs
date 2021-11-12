using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace analyticsTools.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PpeListController : ControllerBase
    {
        private readonly QueryFactory queryFactory;
        private readonly ILog log = LogManager.GetLogger("mylog");

        public PpeListController(QueryFactory queryFactory)
        {
            this.queryFactory = queryFactory;
        }

        [HttpPost]
        public IActionResult SortedPpe(JsonElement parameters)
        {
            dynamic json = JsonConvert.DeserializeObject(parameters.ToString());
            /*   int pageNumber;
               int rowsPerPage;
               string sortBy;
               bool sortDesc;

               pageNumber = (json.pageNumber != null) ? json.pageNumber : 1;
               rowsPerPage = (json.rowsPerPage != null) ? json.rowsPerPage : 10;
               sortBy = (json.sortBy != null) ? json.sortBy : "name";
               sortDesc = (json.sortDesc != null) ? json.sortDesc : false;

               string searchFromJson = json.search;

               int offset = pageNumber * rowsPerPage - rowsPerPage;
               string searchLike = "%" + searchFromJson + "%";
               //searchLike = searchLike.Replace(";", "");

               int totalPpe = this.FetchNumberOfPpe(searchLike);
               IEnumerable<Object> sortedPpe = this.FetchSortedPpeList(searchLike, sortBy, sortDesc, rowsPerPage, offset);

               DateTime dateTimeNow = DateTime.Now;
               DateTime dateTimeSubstract30Minutes = dateTimeNow.AddMinutes(-30);
               string dateTimeNowWithFormatToQuery = dateTimeNow.ToString("yyyy-MM-dd hh:mm:ss");
               string dateTimeSubstract30MinutesWithFormatToQuery = dateTimeSubstract30Minutes.ToString("yyyy-MM-dd hh:mm:ss");

               IEnumerable<Object> measurmentsForTheLast30Minutes = this.FetchMeasurmentsForTheLast30Minutes(dateTimeSubstract30MinutesWithFormatToQuery, dateTimeNowWithFormatToQuery);

               List<string> listOfMeterNrWithMeasurmentsForTheLast30Minute = new List<string>();
               foreach (IDictionary<string, object> row in measurmentsForTheLast30Minutes)
               {
                   listOfMeterNrWithMeasurmentsForTheLast30Minute.Add(row["meter_nr"].ToString());
               }

               this.UpdateSortedPpesWithStatus(sortedPpe, listOfMeterNrWithMeasurmentsForTheLast30Minute);
            */
            //  object returnedPpe = new { sortedPpe = sortedPpe, totalPpe = totalPpe };
            string d = "asd";
            return Ok(d);
        }

        private int FetchNumberOfPpe(string searchLike)
        {
            int totalPpe = Convert.ToInt32(this.queryFactory.Query("public.locations")
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
            SqlKata.Query sortedPpeQuery = this.queryFactory.Query("public.locations")
              .Select("locations.id AS id", "locations.name AS name", "locations.device_serial AS device_serial", "locations.custom_label AS custom_label")
              .SelectRaw("null AS status")
              .Where(q =>
                  q.WhereLike("locations.name", @searchLike)
                  .OrWhereRaw(" CAST(locations.id AS TEXT) LIKE ?", @searchLike)
                  .OrWhereLike("locations.device_serial", @searchLike)
                  .OrWhereLike("locations.custom_label", @searchLike)
              )
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

        private IEnumerable<Object> FetchMeasurmentsForTheLast30Minutes(string dateTimeFrom, string dateTimeTo)
        {
            SqlKata.Query measurmentsForTheLast30MinutesQuery = this.queryFactory.Query("iot.measurement_electricity")
                .SelectRaw("DISTINCT ON (meter_nr) meter_nr")
                .Select("meter_ts")
                .WhereRaw("meter_ts > '" + dateTimeFrom + "' and meter_ts <= '" + dateTimeTo + "'");

            IEnumerable<Object> measurmentsForTheLast30Minutes = new List<object>();
            try
            {
                measurmentsForTheLast30Minutes = measurmentsForTheLast30MinutesQuery.Get();
            }
            catch (Exception e)
            {
                log.Error(e);
            }

            return measurmentsForTheLast30Minutes;
        }
        private void UpdateSortedPpesWithStatus(IEnumerable<Object> sortedPpe, List<string> meterNr)
        {
            foreach (IDictionary<string, object> row in sortedPpe)
            {
                if (row["device_serial"] is null) { row["status"] = false; continue; }
                string deviceSerial = row["device_serial"].ToString();
                if (meterNr.Contains(deviceSerial))
                {
                    row["status"] = true;
                    continue;
                }
                row["status"] = false;
            }
        }
    }
}