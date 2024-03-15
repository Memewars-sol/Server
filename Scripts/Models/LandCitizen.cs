using System.Collections.Generic;
using Memewars.RealtimeNetworking.Server;

namespace Models {
    public class LandCitizen {
        public long Id { get; set; }
        public long AccountId { get; set; }
        public long LandId { get; set; }
        public LandCitizen() {}

        public static List<LandCitizen> All(long land_id) {
            var citizens = new List<LandCitizen>();

            string query = string.Format("select * from land_citizens where land_id = {0}", land_id);
            var ret = Database.ExecuteForResults(query);
            if(ret.Count == 0) {
                return citizens;
            }

            foreach(var res in ret) {
                var citizen = new LandCitizen() {
                    Id = long.Parse(res["id"]),
                    AccountId = long.Parse(res["account_id"]),
                    LandId = long.Parse(res["land_id"]),
                };
                
                citizens.Add(citizen);
            }

            return citizens;
        }
    }
}