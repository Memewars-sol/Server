using System.Threading.Tasks;
using System;
using Npgsql;
using Memewars.RealtimeNetworking.Server;
using System.Data;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using WebUtils;

namespace Models {
    public class Account {
        public long Id { get; set; }
        public string Address { get; set; }

        private (int, int, int, int) AddResources(NpgsqlConnection connection, int gold, int elixir, int darkElixir, int gems)
        {
            int addedGold = 0;
            int addedElixir = 0;
            int addedDark = 0;

            if (gold > 0 || elixir > 0 || darkElixir > 0)
            {
                List<Data.Building> storages = new List<Data.Building>();
                string query = String.Format("SELECT buildings.id, buildings.global_id, buildings.gold_storage, buildings.elixir_storage, buildings.dark_elixir_storage, server_buildings.gold_capacity, server_buildings.elixir_capacity, server_buildings.dark_elixir_capacity FROM buildings LEFT JOIN server_buildings ON buildings.global_id = server_buildings.global_id AND buildings.level = server_buildings.level WHERE buildings.account_id = {0} AND buildings.global_id IN('{1}', '{2}', '{3}', '{4}') AND buildings.level > 0;", Id, Data.BuildingID.townhall.ToString(), Data.BuildingID.goldstorage.ToString(), Data.BuildingID.elixirstorage.ToString(), Data.BuildingID.darkelixirstorage.ToString());
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                Data.Building building = new Data.Building();
                                building.databaseID = long.Parse(reader["id"].ToString());
                                building.id = (Data.BuildingID)Enum.Parse(typeof(Data.BuildingID), reader["global_id"].ToString());
                                building.goldStorage = (int)Math.Floor(float.Parse(reader["gold_storage"].ToString()));
                                building.elixirStorage = (int)Math.Floor(float.Parse(reader["elixir_storage"].ToString()));
                                building.darkStorage = (int)Math.Floor(float.Parse(reader["dark_elixir_storage"].ToString()));
                                building.goldCapacity = int.Parse(reader["gold_capacity"].ToString());
                                building.elixirCapacity = int.Parse(reader["elixir_capacity"].ToString());
                                building.darkCapacity = int.Parse(reader["dark_elixir_capacity"].ToString());
                                storages.Add(building);
                            }
                        }
                    }
                }

                if (storages.Count > 0)
                {
                    int remainedGold = gold;
                    int remainedElixir = elixir;
                    int remainedDatk = darkElixir;
                    for (int i = 0; i < storages.Count; i++)
                    {
                        if (remainedGold <= 0 && remainedElixir <= 0 && remainedDatk <= 0)
                        {
                            break;
                        }

                        int goldSpace = storages[i].goldCapacity - storages[i].goldStorage;
                        int elixirSpace = storages[i].elixirCapacity - storages[i].elixirStorage;
                        int darkSpace = storages[i].darkCapacity - storages[i].darkStorage;

                        int addGold = 0;
                        int addElixir = 0;
                        int addDark = 0;

                        switch (storages[i].id)
                        {
                            case Data.BuildingID.townhall:
                                addGold = (goldSpace >= remainedGold) ? remainedGold : goldSpace;
                                addElixir = (elixirSpace >= remainedElixir) ? remainedElixir : elixirSpace;
                                addDark = (darkSpace >= remainedDatk) ? remainedDatk : darkSpace;
                                break;
                            case Data.BuildingID.goldstorage:
                                addGold = (goldSpace >= remainedGold) ? remainedGold : goldSpace;
                                break;
                            case Data.BuildingID.elixirstorage:
                                addElixir = (elixirSpace >= remainedElixir) ? remainedElixir : elixirSpace;
                                break;
                            case Data.BuildingID.darkelixirstorage:
                                addDark = (darkSpace >= remainedDatk) ? remainedDatk : darkSpace;
                                break;
                        }

                        query = String.Format("UPDATE buildings SET gold_storage = gold_storage + {0}, elixir_storage = elixir_storage + {1}, dark_elixir_storage = dark_elixir_storage + {2} WHERE id = {3};", addGold, addElixir, addDark, storages[i].databaseID);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            command.ExecuteNonQuery();
                        }

                        remainedGold -= addGold;
                        remainedElixir -= addElixir;
                        remainedDatk -= addDark;

                        addedGold += addGold;
                        addedElixir += addElixir;
                        addedDark += addDark;
                    }
                }
            }

            if (gems > 0)
            {
                string query = String.Format("UPDATE accounts SET gems = gems + {0} WHERE id = {1};", gems, Id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
            }

            return (addedGold, addedElixir, addedDark, gems);
        }


        public async Task<long> Create() {
            using var connection = Database.GetDbConnection();
            string query = String.Format("INSERT INTO accounts (device_id, password, name, address) VALUES('{0}', '{1}', '{2}', '{3}') RETURNING id;", "", "", "", Address);
            
            // get Id
            using NpgsqlCommand command = new NpgsqlCommand(query, connection);
            Id = (long)command.ExecuteScalar();

            // mints the cNFT
            // causes 1.7s delay
            await HttpSender.PostJson("http://localhost:8081/api/mintAccount", new Dictionary<string, string>(){
                ["address"] = Address,
            });

            await new Building {
                id = BuildingID.townhall,
                account_id = Id,
                x = 25,
                y = 25,
                warX = 25,
                warY = 25,
                level = 1,
            }.Create();

            await new Building {
                id = BuildingID.goldmine,
                account_id = Id,
                x = 27,
                y = 21,
                warX = 27,
                warY = 21,
                level = 1,
            }.Create();

            await new Building {
                id = BuildingID.goldstorage,
                account_id = Id,
                x = 27,
                y = 21,
                warX = 27,
                warY = 21,
                level = 1,
            }.Create();

            await new Building {
                id = BuildingID.elixirmine,
                account_id = Id,
                x = 21,
                y = 27,
                warX = 21,
                warY = 27,
                level = 1,
            }.Create();

            await new Building {
                id = BuildingID.elixirstorage,
                account_id = Id,
                x = 25,
                y = 30,
                warX = 25,
                warY = 30,
                level = 1,
            }.Create();

            await new Building {
                id = BuildingID.buildershut,
                account_id = Id,
                x = 22,
                y = 24,
                warX = 22,
                warY = 24,
                level = 1,
            }.Create();

            List<int> xl = new List<int> { 19, 24, 32, 32, 34, 30, 26, 17, 8, 3, 2, 5, 16, 26, 35, 40 };
            List<int> yl = new List<int> { 20, 15, 16, 24, 30, 33, 35, 37, 32, 39, 10, 4, 1, 3, 1, 5 };
            Random rnd = new Random();
            for (int i = 1; i <= 5; i++)
            {
                int index = rnd.Next(0, xl.Count);
                int level = rnd.Next(1, 6);

                // add random obstacles
                await new Building {
                    id = BuildingID.obstacle,
                    account_id = Id,
                    x = xl[index],
                    y = yl[index],
                    warX = xl[index],
                    warY = yl[index],
                    level = level,
                }.Create();

                xl.RemoveAt(index);
                yl.RemoveAt(index);
            }

            AddResources(connection, 10000, 10000, 0, 250);
            connection.Close();
            return Id;
        }
    }
}