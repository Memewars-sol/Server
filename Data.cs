using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.IO;
using System.Security.Cryptography;
using System.IO.Compression;
using Models;

namespace Memewars.RealtimeNetworking.Server
{
    public static class Data
    {

        public const int maxTownHallLevel = 10;
        public const int minGoldCollect = 10;
        public const int minElixirCollect = 10;
        public const int minDarkElixirCollect = 10;
        public static readonly int battleDuration = 120;
        public static readonly int battlePrepDuration = 30;
        public static readonly int gridSize = 45;
        public static readonly float gridCellSize = 1;

        public static readonly float battleFrameRate = 0.05f;
        public static readonly int battleTilesWorthOfOneWall = 15;
        public static readonly int battleGroupWallAttackRadius = 5;
        public static readonly int battleGridOffset = 2;
        public static readonly int shieldMinutesAmountToBattleLost = 180;

        public static readonly int globalChatArchiveMaxMessages = 30;
        public static readonly int chatSyncPeriod = 2;

        public static readonly string mysqlDateTimeFormat = "%Y-%m-%d %H:%i:%s";

        public static readonly int recoveryCodeExpiration = 300;
        public static readonly int confirmationCodeExpiration = 300;
        public static readonly int recoveryCodeLength = 6;

        public static string EncodeString(string input)
        {
            try
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
            }
            catch (Exception)
            {
                return input;
            }
        }

        public static string DecodeString(string input)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(input));
            }
            catch (Exception)
            {
                return input;
            }
        }

        public static bool IsEmailValid(string email)
        {
            email = email.Trim();
            if (email.EndsWith(".")) { return false; }
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch { return false; }
        }

        public static string RandomCode(int length)
        {
            if (length <= 0)
            {
                return "";
            }
            Random random = new Random();
            const string chars = "0123456789";
            string value = "";
            while (value.Length < length)
            {
                value += chars[random.Next(0, chars.Length)].ToString();
            }
            return value;
        }

        public static string EncrypteToMD5(string data)
        {
            using (MD5 md5Hash = MD5.Create())
            {
                data = GetMd5Hash(md5Hash, data);
            }
            return data;
            /*
            UTF8Encoding ue = new UTF8Encoding();
            byte[] bytes = ue.GetBytes(data);
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            byte[] hashBytes = md5.ComputeHash(bytes);
            string hashString = "";
            for (int i = 0; i < hashBytes.Length; i++)
            {
                hashString = hashString + Convert.ToString(hashBytes[i], 16).PadLeft(2, '0');
            }
            return hashString.PadLeft(32, '0');
            */
        }

        private static string GetMd5Hash(MD5 md5Hash, string input)
        {
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            StringBuilder sBuilder = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }
            return sBuilder.ToString();
        }

        public enum ChatType
        {
            global = 1,
        }

        public static bool IsMessageGoodToSend(string message)
        {

            if (message.Contains("fork"))
            {
                return false;
            }

            return true;
        }

        public static int GetBoostResourcesCost(BuildingID id, int level)
        {
            return 20;
        }

        public class CharMessage
        {
            public long id = 0;
            public long accountID = 0;
            public string name = "";
            public Data.ChatType type = 0;
            public long globalID = 0;
            public long clanID = 0;
            public string message = "";
            public string color = "";
            public string time = "";
        }

        public class JoinRequest
        {
            public long id = 0;
            public long accountID = 0;
            public string name = "";
            public int level = 1;
            public int trophies = 0;
            public DateTime time;
        }
        public static bool IsUnitUnlocked(UnitID id, int barracksLevel, int darkBarracksLevel)
        {
            // todo
            switch (id)
            {
                case UnitID.barbarian: return barracksLevel >= 1;
                case UnitID.archer: return barracksLevel >= 2;
                case UnitID.giant: return barracksLevel >= 3;
                case UnitID.goblin: return barracksLevel >= 4;
                case UnitID.wallbreaker: return barracksLevel >= 5;
                case UnitID.balloon: return barracksLevel >= 6;
                case UnitID.wizard: return barracksLevel >= 7;
                case UnitID.healer: return barracksLevel >= 8;
                case UnitID.dragon: return barracksLevel >= 9;
                case UnitID.pekka: return barracksLevel >= 10;
                case UnitID.babydragon: return barracksLevel >= 11;
                case UnitID.miner: return barracksLevel >= 12;
                case UnitID.electrodragon: return barracksLevel >= 13;
                case UnitID.yeti: return barracksLevel >= 14;
                case UnitID.dragonrider: return barracksLevel >= 15;
                case UnitID.electrotitan: return barracksLevel >= 16;
                case UnitID.minion: return darkBarracksLevel >= 1;
                case UnitID.hogrider: return darkBarracksLevel >= 2;
                case UnitID.valkyrie: return darkBarracksLevel >= 3;
                case UnitID.golem: return darkBarracksLevel >= 4;
                case UnitID.witch: return darkBarracksLevel >= 5;
                case UnitID.lavahound: return darkBarracksLevel >= 6;
                case UnitID.bowler: return darkBarracksLevel >= 7;
                case UnitID.icegolem: return darkBarracksLevel >= 8;
                case UnitID.headhunter: return darkBarracksLevel >= 9;
                default: return false;
            }
        }

        public static bool IsSpellUnlocked(SpellID id, int spellFactoryLevel, int darkSpellFactoryLevel)
        {
            // todo
            switch (id)
            {
                case SpellID.lightning: return spellFactoryLevel >= 1;
                case SpellID.healing: return spellFactoryLevel >= 2;
                case SpellID.rage: return spellFactoryLevel >= 3;
                //case SpellID.jump: return spellFactoryLevel >= 4;
                case SpellID.freeze: return spellFactoryLevel >= 4;
                case SpellID.invisibility: return spellFactoryLevel >= 5;
                case SpellID.earthquake: return darkSpellFactoryLevel >= 1;
                case SpellID.haste: return darkSpellFactoryLevel >= 2;
                case SpellID.skeleton: return darkSpellFactoryLevel >= 3;
                case SpellID.bat: return darkSpellFactoryLevel >= 4;
                default: return false;
            }
        }

        public static int GetNexLevelRequiredXp(int currentLevel)
        {
            if (currentLevel == 1) { return 30; }
            else if (currentLevel <= 200) { return (currentLevel - 1) * 50; }
            else if (currentLevel <= 299) { return ((currentLevel - 200) * 500) + 9500; }
            else { return ((currentLevel - 300) * 1000) + 60000; }
        }

        public static int GetBattleSearchCost(int townHallLevel)
        {
            // todo
            switch (townHallLevel)
            {
                case 1: return 10;
                case 2: return 25;
                case 3: return 50;
                case 4: return 100;
                case 5: return 200;
                case 6: return 380;
                case 7: return 420;
                case 8: return 580;
                case 9: return 850;
                case 10: return 1000;
                case 11: return 1500;
                case 12: return 2000;
                case 13: return 4000;
                case 14: return 6000;
                case 15: return 10000;
                default: return 999999;
            }
        }

        public static int GetTotalXpEarned(int currentLevel)
        {
            if (currentLevel == 1) { return 0; }
            else if (currentLevel <= 201) { return ((currentLevel - 1) * (currentLevel - 2) * 25) + 30; }
            else if (currentLevel <= 299) { return ((currentLevel - 200) * (currentLevel - 200) * 250) + (9250 * (currentLevel - 200)) + 985530; }
            else { return ((currentLevel - 300) * (currentLevel - 300) * 500) + (59500 * (currentLevel - 300)) + 4410530; }
        }

        public static int GetStorageGoldAndElixirLoot(int townhallLevel, float storage)
        {
            // todo
            double p = 0;
            switch (townhallLevel)
            {
                case 1: case 2: case 3: case 4: case 5: case 6: p = 0.2d; break;
                case 7: p = 0.18d; break;
                case 8: p = 0.16d; break;
                case 9: p = 0.14d; break;
                case 10: p = 0.12d; break;
                default: p = 0.1d; break;
            }
            return (int)Math.Floor(storage * p);
        }

        public static int GetStorageDarkElixirLoot(int townhallLevel, float storage)
        {
            // todo
            double p = 0;
            switch (townhallLevel)
            {
                case 1: case 2: case 3: case 4: case 5: case 6: case 7: case 8: p = 0.06d; break;
                case 9: p = 0.05d; break;
                default: p = 0.04d; break;
            }
            return (int)Math.Floor(storage * p);
        }

        public static int GetMinesGoldAndElixirLoot(int townhallLevel, float storage)
        {
            return (int)Math.Floor(storage * 0.5d);
        }

        public static int GetMinesDarkElixirLoot(int townhallLevel, float storage)
        {
            return (int)Math.Floor(storage * 0.75d);
        }

        public static (int, int) GetBattleTrophies(int attackerTrophies, int defendderTrophies)
        {
            int win = 0;
            int lose = 0;
            if (attackerTrophies == defendderTrophies)
            {
                win = 30;
                lose = 20;
            }
            else
            {
                double delta = Math.Abs(attackerTrophies - defendderTrophies);
                if (attackerTrophies > defendderTrophies)
                {
                    win = 30 - (int)Math.Floor(delta * (28d / 600d));
                    lose = 20 + (int)Math.Floor(delta * (19d / 600d));
                    if (win < 2)
                    {
                        win = 2;
                    }
                }
                else
                {
                    win = 30 + (int)Math.Floor(delta * (28d / 600d));
                    lose = 20 - (int)Math.Floor(delta * (19d / 600d));
                    if (lose < 1)
                    {
                        lose = 1;
                    }
                }
            }
            return (win, lose);
        }

        public class OpponentData
        {
            public long id = 0;
            public Player data = null;
            public List<Building> buildings = null;
        }

        public class BattleStartBuildingData
        {
            public BuildingID id = BuildingID.townhall;
            public long databaseID = 0;
            public int lootGoldStorage = 0;
            public int lootElixirStorage = 0;
            public int lootDarkStorage = 0;
        }
        public static T CloneClass<T>(this T target)
        {
            return Desrialize<T>(Serialize<T>(target));
        }

        public static string Serialize<T>(this T target)
        {
            XmlSerializer xml = new XmlSerializer(typeof(T));
            StringWriter writer = new StringWriter();
            xml.Serialize(writer, target);
            return writer.ToString();
        }

        public static T Desrialize<T>(this string target)
        {
            XmlSerializer xml = new XmlSerializer(typeof(T));
            StringReader reader = new StringReader(target);
            return (T)xml.Deserialize(reader);
        }

        public async static Task<string> SerializeAsync<T>(this T target)
        {
            Task<string> task = Task.Run(() =>
            {
                XmlSerializer xml = new XmlSerializer(typeof(T));
                StringWriter writer = new StringWriter();
                xml.Serialize(writer, target);
                return writer.ToString();
            });
            return await task;
        }

        public async static Task<T> DesrializeAsync<T>(this string target)
        {
            Task<T> task = Task.Run(() =>
            {
                XmlSerializer xml = new XmlSerializer(typeof(T));
                StringReader reader = new StringReader(target);
                return (T)xml.Deserialize(reader);
            });
            return await task;
        }

        public static void CopyTo(Stream source, Stream target)
        {
            byte[] bytes = new byte[4096]; int count;
            while ((count = source.Read(bytes, 0, bytes.Length)) != 0)
            {
                target.Write(bytes, 0, count);
            }
        }

        public async static Task<byte[]> CompressAsync(string target)
        {
            Task<byte[]> task = Task.Run(() =>
            {
                return Compress(target);
            });
            return await task;
        }

        public static byte[] Compress(string target)
        {
            var bytes = Encoding.UTF8.GetBytes(target);
            using (var msi = new MemoryStream(bytes))
            {
                using (var mso = new MemoryStream())
                {
                    using (var gs = new GZipStream(mso, CompressionMode.Compress))
                    {
                        CopyTo(msi, gs);
                    }
                    return mso.ToArray();
                }
            }
        }

        public async static Task<string> DecompressAsync(byte[] bytes)
        {
            Task<string> task = Task.Run(() =>
            {
                return Decompress(bytes);
            });
            return await task;
        }

        public static string Decompress(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            {
                using (var mso = new MemoryStream())
                {
                    using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                    {
                        CopyTo(gs, mso);
                    }
                    return Encoding.UTF8.GetString(mso.ToArray());
                }
            }
        }

        public static BuildingCount GetBuildingLimits(int townHallLevel, string globalID)
        {
            if (townHallLevel > 0 && townHallLevel < buildingAvailability.Length)
            {
                for (int i = 0; i < buildingAvailability[townHallLevel].buildings.Length; i++)
                {
                    if (buildingAvailability[townHallLevel].buildings[i].id == globalID)
                    {
                        return CloneClass(buildingAvailability[townHallLevel].buildings[i]);
                    }
                }
            }
            return null;
        }

        public static BuildingAvailability GetTownHallLimits(int targetTownHallLevel)
        {
            for (int i = 0; i < buildingAvailability.Length; i++)
            {
                if (buildingAvailability[i].level == targetTownHallLevel)
                {
                    return CloneClass(buildingAvailability[i]);
                }
            }
            return null;
        }

        public static int GetInstantBuildRequiredGems(int remainedSeconds)
        {
            int gems = 0;
            if (remainedSeconds > 0)
            {
                if (remainedSeconds <= 60)
                {
                    gems = 1;
                }
                else if (remainedSeconds <= 3600)
                {
                    gems = (int)(0.00537f * ((float)remainedSeconds - 60f)) + 1;
                }
                else if (remainedSeconds <= 86400)
                {
                    gems = (int)(0.00266f * ((float)remainedSeconds - 3600f)) + 20;
                }
                else
                {
                    gems = (int)(0.00143f * ((float)remainedSeconds - 86400f)) + 260;
                }
            }
            return gems;
        }

        public static int GetResourceGemCost(int gold, int elixir, int dark)
        {
            if (gold < 0) { gold = 0; }
            if (elixir < 0) { elixir = 0; }
            if (dark < 0) { dark = 0; }
            if (gold <= 0 && elixir <= 0 && dark <= 0)
            {
                return 0;
            }
            else
            {
                return (int)Math.Ceiling(((double)(gold + elixir) * 0.001d + (double)dark * 0.1d));
            }
        }

        public enum BuyResourcePack
        {
            gold_10 = 0, gold_50 = 1, gold_100 = 2, elixir_10 = 3, elixir_50 = 4, elixir_100 = 5, dark_10 = 6, dark_50 = 7, dark_100 = 8
        }

        public static BuildingAvailability[] buildingAvailability =
        {
            new BuildingAvailability
            {
                level = 0,
                buildings = {}
            },
            new BuildingAvailability
            {
                level = 1,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 1, maxLevel = 2},
                    new BuildingCount { id = "elixirmine", count = 1, maxLevel = 2},
                    new BuildingCount { id = "goldstorage", count = 1, maxLevel = 1},
                    new BuildingCount { id = "elixirstorage", count = 1, maxLevel = 1},
                    new BuildingCount { id = "armycamp", count = 1, maxLevel = 1},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 3},
                    new BuildingCount { id = "cannon", count = 2, maxLevel = 2},
                }
            },
            new BuildingAvailability
            {
                level = 2,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 2, maxLevel = 4},
                    new BuildingCount { id = "elixirmine", count = 2, maxLevel = 4},
                    new BuildingCount { id = "goldstorage", count = 1, maxLevel = 3},
                    new BuildingCount { id = "elixirstorage", count = 1, maxLevel = 3},
                    new BuildingCount { id = "armycamp", count = 1, maxLevel = 2},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 4},
                    new BuildingCount { id = "cannon", count = 2, maxLevel = 3},
                    new BuildingCount { id = "archertower", count = 1, maxLevel = 2},
                    new BuildingCount { id = "wall", count = 25, maxLevel = 2},
                }
            },
            new BuildingAvailability
            {
                level = 3,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 3, maxLevel = 6},
                    new BuildingCount { id = "elixirmine", count = 3, maxLevel = 6},
                    new BuildingCount { id = "goldstorage", count = 2, maxLevel = 6},
                    new BuildingCount { id = "elixirstorage", count = 2, maxLevel = 6},
                    new BuildingCount { id = "clancastle", count = 1, maxLevel = 1},
                    new BuildingCount { id = "armycamp", count = 2, maxLevel = 3},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 5},
                    new BuildingCount { id = "laboratory", count = 1, maxLevel = 1},
                    new BuildingCount { id = "cannon", count = 2, maxLevel = 4},
                    new BuildingCount { id = "archertower", count = 1, maxLevel = 3},
                    new BuildingCount { id = "mortor", count = 1, maxLevel = 1},
                    new BuildingCount { id = "wall", count = 50, maxLevel = 3},
                    new BuildingCount { id = "boomb", count = 2, maxLevel = 2},
                }
            },
            new BuildingAvailability
            {
                level = 4,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 4, maxLevel = 8},
                    new BuildingCount { id = "elixirmine", count = 4, maxLevel = 8},
                    new BuildingCount { id = "goldstorage", count = 2, maxLevel = 8},
                    new BuildingCount { id = "elixirstorage", count = 2, maxLevel = 8},
                    new BuildingCount { id = "clancastle", count = 1, maxLevel = 2},
                    new BuildingCount { id = "armycamp", count = 2, maxLevel = 4},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 6},
                    new BuildingCount { id = "laboratory", count = 1, maxLevel = 2},
                    new BuildingCount { id = "cannon", count = 2, maxLevel = 5},
                    new BuildingCount { id = "archertower", count = 2, maxLevel = 4},
                    new BuildingCount { id = "mortor", count = 1, maxLevel = 2},
                    new BuildingCount { id = "airdefense", count = 1, maxLevel = 2},
                    new BuildingCount { id = "wall", count = 75, maxLevel = 4},
                    new BuildingCount { id = "boomb", count = 2, maxLevel = 2},
                    new BuildingCount { id = "springtrap", count = 2, maxLevel = 1},
                }
            },
            new BuildingAvailability
            {
                level = 5,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 5, maxLevel = 10},
                    new BuildingCount { id = "elixirmine", count = 5, maxLevel = 10},
                    new BuildingCount { id = "goldstorage", count = 2, maxLevel = 9},
                    new BuildingCount { id = "elixirstorage", count = 2, maxLevel = 9},
                    new BuildingCount { id = "clancastle", count = 1, maxLevel = 2},
                    new BuildingCount { id = "armycamp", count = 3, maxLevel = 5},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 7},
                    new BuildingCount { id = "laboratory", count = 1, maxLevel = 3},
                    new BuildingCount { id = "spellfactory", count = 1, maxLevel = 1},
                    new BuildingCount { id = "cannon", count = 3, maxLevel = 6},
                    new BuildingCount { id = "archertower", count = 3, maxLevel = 6},
                    new BuildingCount { id = "mortor", count = 1, maxLevel = 3},
                    new BuildingCount { id = "airdefense", count = 1, maxLevel = 3},
                    new BuildingCount { id = "wizardtower", count = 1, maxLevel = 2},
                    new BuildingCount { id = "wall", count = 100, maxLevel = 5},
                    new BuildingCount { id = "boomb", count = 4, maxLevel = 3},
                    new BuildingCount { id = "springtrap", count = 2, maxLevel = 1},
                    new BuildingCount { id = "airbomb", count = 2, maxLevel = 2},
                }
            },
            new BuildingAvailability
            {
                level = 6,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 6, maxLevel = 10},
                    new BuildingCount { id = "elixirmine", count = 6, maxLevel = 10},
                    new BuildingCount { id = "goldstorage", count = 2, maxLevel = 10},
                    new BuildingCount { id = "elixirstorage", count = 2, maxLevel = 10},
                    new BuildingCount { id = "clancastle", count = 1, maxLevel = 3},
                    new BuildingCount { id = "armycamp", count = 3, maxLevel = 6},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 8},
                    new BuildingCount { id = "laboratory", count = 1, maxLevel = 4},
                    new BuildingCount { id = "spellfactory", count = 1, maxLevel = 2},
                    new BuildingCount { id = "cannon", count = 3, maxLevel = 7},
                    new BuildingCount { id = "archertower", count = 3, maxLevel = 7},
                    new BuildingCount { id = "mortor", count = 2, maxLevel = 4},
                    new BuildingCount { id = "airdefense", count = 2, maxLevel = 4},
                    new BuildingCount { id = "wizardtower", count = 2, maxLevel = 3},
                    new BuildingCount { id = "airsweeper", count = 1, maxLevel = 2},
                    new BuildingCount { id = "wall", count = 125, maxLevel = 6},
                    new BuildingCount { id = "boomb", count = 4, maxLevel = 3},
                    new BuildingCount { id = "springtrap", count = 4, maxLevel = 1},
                    new BuildingCount { id = "airbomb", count = 2, maxLevel = 2},
                    new BuildingCount { id = "giantbomb", count = 1, maxLevel = 2},
                }
            },
            new BuildingAvailability
            {
                level = 7,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 6, maxLevel = 11},
                    new BuildingCount { id = "elixirmine", count = 6, maxLevel = 11},
                    new BuildingCount { id = "darkelixirmine", count = 1, maxLevel = 3},
                    new BuildingCount { id = "goldstorage", count = 2, maxLevel = 11},
                    new BuildingCount { id = "elixirstorage", count = 2, maxLevel = 11},
                    new BuildingCount { id = "darkelixirstorage", count = 1, maxLevel = 2},
                    new BuildingCount { id = "clancastle", count = 1, maxLevel = 3},
                    new BuildingCount { id = "armycamp", count = 4, maxLevel = 6},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 9},
                    new BuildingCount { id = "darkbarracks", count = 1, maxLevel = 2},
                    new BuildingCount { id = "laboratory", count = 1, maxLevel = 5},
                    new BuildingCount { id = "spellfactory", count = 1, maxLevel = 3},
                    new BuildingCount { id = "kingaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "cannon", count = 5, maxLevel = 8},
                    new BuildingCount { id = "archertower", count = 4, maxLevel = 8},
                    new BuildingCount { id = "mortor", count = 3, maxLevel = 5},
                    new BuildingCount { id = "airdefense", count = 3, maxLevel = 5},
                    new BuildingCount { id = "wizardtower", count = 2, maxLevel = 4},
                    new BuildingCount { id = "airsweeper", count = 1, maxLevel = 3},
                    new BuildingCount { id = "hiddentesla", count = 2, maxLevel = 3},
                    new BuildingCount { id = "wall", count = 175, maxLevel = 6},
                    new BuildingCount { id = "boomb", count = 6, maxLevel = 4},
                    new BuildingCount { id = "springtrap", count = 4, maxLevel = 2},
                    new BuildingCount { id = "airbomb", count = 2, maxLevel = 3},
                    new BuildingCount { id = "giantbomb", count = 2, maxLevel = 2},
                    new BuildingCount { id = "seekingairmine", count = 1, maxLevel = 1},
                }
            },
            new BuildingAvailability
            {
                level = 8,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 6, maxLevel = 12},
                    new BuildingCount { id = "elixirmine", count = 6, maxLevel = 12},
                    new BuildingCount { id = "darkelixirmine", count = 2, maxLevel = 3},
                    new BuildingCount { id = "goldstorage", count = 3, maxLevel = 11},
                    new BuildingCount { id = "elixirstorage", count = 3, maxLevel = 11},
                    new BuildingCount { id = "darkelixirstorage", count = 1, maxLevel = 4},
                    new BuildingCount { id = "clancastle", count = 1, maxLevel = 4},
                    new BuildingCount { id = "armycamp", count = 4, maxLevel = 6},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 10},
                    new BuildingCount { id = "darkbarracks", count = 1, maxLevel = 4},
                    new BuildingCount { id = "laboratory", count = 1, maxLevel = 6},
                    new BuildingCount { id = "spellfactory", count = 1, maxLevel = 3},
                    new BuildingCount { id = "darkspellfactory", count = 1, maxLevel = 2},
                    new BuildingCount { id = "kingaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "cannon", count = 5, maxLevel = 10},
                    new BuildingCount { id = "archertower", count = 5, maxLevel = 10},
                    new BuildingCount { id = "mortor", count = 4, maxLevel = 6},
                    new BuildingCount { id = "airdefense", count = 3, maxLevel = 6},
                    new BuildingCount { id = "wizardtower", count = 3, maxLevel = 6},
                    new BuildingCount { id = "airsweeper", count = 1, maxLevel = 4},
                    new BuildingCount { id = "hiddentesla", count = 3, maxLevel = 6},
                    new BuildingCount { id = "bombtower", count = 1, maxLevel = 2},
                    new BuildingCount { id = "wall", count = 225, maxLevel = 8},
                    new BuildingCount { id = "boomb", count = 6, maxLevel = 5},
                    new BuildingCount { id = "springtrap", count = 6, maxLevel = 3},
                    new BuildingCount { id = "airbomb", count = 4, maxLevel = 3},
                    new BuildingCount { id = "giantbomb", count = 3, maxLevel = 3},
                    new BuildingCount { id = "seekingairmine", count = 2, maxLevel = 1},
                    new BuildingCount { id = "skeletontrap", count = 2, maxLevel = 2},
                }
            },
            new BuildingAvailability
            {
                level = 9,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 7, maxLevel = 12},
                    new BuildingCount { id = "elixirmine", count = 7, maxLevel = 12},
                    new BuildingCount { id = "darkelixirmine", count = 3, maxLevel = 6},
                    new BuildingCount { id = "goldstorage", count = 4, maxLevel = 11},
                    new BuildingCount { id = "elixirstorage", count = 4, maxLevel = 11},
                    new BuildingCount { id = "darkelixirstorage", count = 1, maxLevel = 6},
                    new BuildingCount { id = "clancastle", count = 1, maxLevel = 5},
                    new BuildingCount { id = "armycamp", count = 4, maxLevel = 7},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 11},
                    new BuildingCount { id = "darkbarracks", count = 1, maxLevel = 6},
                    new BuildingCount { id = "laboratory", count = 1, maxLevel = 7},
                    new BuildingCount { id = "spellfactory", count = 1, maxLevel = 4},
                    new BuildingCount { id = "darkspellfactory", count = 1, maxLevel = 4},
                    new BuildingCount { id = "kingaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "qeenaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "cannon", count = 5, maxLevel = 11},
                    new BuildingCount { id = "archertower", count = 6, maxLevel = 11},
                    new BuildingCount { id = "mortor", count = 4, maxLevel = 7},
                    new BuildingCount { id = "airdefense", count = 4, maxLevel = 7},
                    new BuildingCount { id = "wizardtower", count = 4, maxLevel = 7},
                    new BuildingCount { id = "airsweeper", count = 2, maxLevel = 5},
                    new BuildingCount { id = "hiddentesla", count = 4, maxLevel = 7},
                    new BuildingCount { id = "bombtower", count = 1, maxLevel = 3},
                    new BuildingCount { id = "xbow", count = 2, maxLevel = 3},
                    new BuildingCount { id = "wall", count = 250, maxLevel = 10},
                    new BuildingCount { id = "boomb", count = 6, maxLevel = 6},
                    new BuildingCount { id = "springtrap", count = 6, maxLevel = 4},
                    new BuildingCount { id = "airbomb", count = 4, maxLevel = 4},
                    new BuildingCount { id = "giantbomb", count = 4, maxLevel = 3},
                    new BuildingCount { id = "seekingairmine", count = 4, maxLevel = 2},
                    new BuildingCount { id = "skeletontrap", count = 2, maxLevel = 3},
                }
            },
            new BuildingAvailability
            {
                level = 10,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 7, maxLevel = 13},
                    new BuildingCount { id = "elixirmine", count = 7, maxLevel = 13},
                    new BuildingCount { id = "darkelixirmine", count = 3, maxLevel = 7},
                    new BuildingCount { id = "goldstorage", count = 4, maxLevel = 11},
                    new BuildingCount { id = "elixirstorage", count = 4, maxLevel = 11},
                    new BuildingCount { id = "darkelixirstorage", count = 1, maxLevel = 6},
                    new BuildingCount { id = "clancastle", count = 1, maxLevel = 6},
                    new BuildingCount { id = "armycamp", count = 4, maxLevel = 8},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 12},
                    new BuildingCount { id = "darkbarracks", count = 1, maxLevel = 7},
                    new BuildingCount { id = "laboratory", count = 1, maxLevel = 8},
                    new BuildingCount { id = "spellfactory", count = 1, maxLevel = 5},
                    new BuildingCount { id = "darkspellfactory", count = 1, maxLevel = 5},
                    new BuildingCount { id = "kingaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "qeenaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "cannon", count = 6, maxLevel = 13},
                    new BuildingCount { id = "archertower", count = 7, maxLevel = 13},
                    new BuildingCount { id = "mortor", count = 4, maxLevel = 8},
                    new BuildingCount { id = "airdefense", count = 4, maxLevel = 8},
                    new BuildingCount { id = "wizardtower", count = 4, maxLevel = 9},
                    new BuildingCount { id = "airsweeper", count = 2, maxLevel = 6},
                    new BuildingCount { id = "hiddentesla", count = 4, maxLevel = 8},
                    new BuildingCount { id = "bombtower", count = 2, maxLevel = 4},
                    new BuildingCount { id = "xbow", count = 3, maxLevel = 4},
                    new BuildingCount { id = "infernotower", count = 2, maxLevel = 3},
                    new BuildingCount { id = "wall", count = 275, maxLevel = 11},
                    new BuildingCount { id = "boomb", count = 6, maxLevel = 7},
                    new BuildingCount { id = "springtrap", count = 6, maxLevel = 5},
                    new BuildingCount { id = "airbomb", count = 5, maxLevel = 4},
                    new BuildingCount { id = "giantbomb", count = 5, maxLevel = 4},
                    new BuildingCount { id = "seekingairmine", count = 5, maxLevel = 3},
                    new BuildingCount { id = "skeletontrap", count = 3, maxLevel = 4},
                }
            },
            new BuildingAvailability
            {
                level = 11,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 7, maxLevel = 14},
                    new BuildingCount { id = "elixirmine", count = 7, maxLevel = 14},
                    new BuildingCount { id = "darkelixirmine", count = 3, maxLevel = 8},
                    new BuildingCount { id = "goldstorage", count = 4, maxLevel = 12},
                    new BuildingCount { id = "elixirstorage", count = 4, maxLevel = 12},
                    new BuildingCount { id = "darkelixirstorage", count = 1, maxLevel = 6},
                    new BuildingCount { id = "clancastle", count = 1, maxLevel = 7},
                    new BuildingCount { id = "armycamp", count = 4, maxLevel = 9},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 13},
                    new BuildingCount { id = "darkbarracks", count = 1, maxLevel = 8},
                    new BuildingCount { id = "laboratory", count = 1, maxLevel = 9},
                    new BuildingCount { id = "spellfactory", count = 1, maxLevel = 6},
                    new BuildingCount { id = "darkspellfactory", count = 1, maxLevel = 5},
                    new BuildingCount { id = "kingaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "qeenaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "wardenaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "cannon", count = 7, maxLevel = 15},
                    new BuildingCount { id = "archertower", count = 8, maxLevel = 15},
                    new BuildingCount { id = "mortor", count = 4, maxLevel = 10},
                    new BuildingCount { id = "airdefense", count = 4, maxLevel = 9},
                    new BuildingCount { id = "wizardtower", count = 5, maxLevel = 10},
                    new BuildingCount { id = "airsweeper", count = 2, maxLevel = 7},
                    new BuildingCount { id = "hiddentesla", count = 4, maxLevel = 9},
                    new BuildingCount { id = "bombtower", count = 2, maxLevel = 6},
                    new BuildingCount { id = "xbow", count = 4, maxLevel = 5},
                    new BuildingCount { id = "infernotower", count = 2, maxLevel = 5},
                    new BuildingCount { id = "eagleartillery", count = 1, maxLevel = 2},
                    new BuildingCount { id = "wall", count = 300, maxLevel = 12},
                    new BuildingCount { id = "boomb", count = 6, maxLevel = 8},
                    new BuildingCount { id = "springtrap", count = 6, maxLevel = 5},
                    new BuildingCount { id = "airbomb", count = 5, maxLevel = 5},
                    new BuildingCount { id = "giantbomb", count = 5, maxLevel = 5},
                    new BuildingCount { id = "seekingairmine", count = 5, maxLevel = 3},
                    new BuildingCount { id = "skeletontrap", count = 3, maxLevel = 4},
                    new BuildingCount { id = "tornadotrap", count = 1, maxLevel = 2},
                }
            },
            new BuildingAvailability
            {
                level = 12,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 7, maxLevel = 15},
                    new BuildingCount { id = "elixirmine", count = 7, maxLevel = 15},
                    new BuildingCount { id = "darkelixirmine", count = 3, maxLevel = 9},
                    new BuildingCount { id = "goldstorage", count = 4, maxLevel = 13},
                    new BuildingCount { id = "elixirstorage", count = 4, maxLevel = 13},
                    new BuildingCount { id = "darkelixirstorage", count = 1, maxLevel = 7},
                    new BuildingCount { id = "clancastle", count = 1, maxLevel = 8},
                    new BuildingCount { id = "armycamp", count = 4, maxLevel = 10},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 14},
                    new BuildingCount { id = "darkbarracks", count = 1, maxLevel = 9},
                    new BuildingCount { id = "laboratory", count = 1, maxLevel = 10},
                    new BuildingCount { id = "spellfactory", count = 1, maxLevel = 6},
                    new BuildingCount { id = "darkspellfactory", count = 1, maxLevel = 5},
                    new BuildingCount { id = "workshop", count = 1, maxLevel = 3},
                    new BuildingCount { id = "kingaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "qeenaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "wardenaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "cannon", count = 7, maxLevel = 17},
                    new BuildingCount { id = "archertower", count = 8, maxLevel = 17},
                    new BuildingCount { id = "mortor", count = 4, maxLevel = 12},
                    new BuildingCount { id = "airdefense", count = 4, maxLevel = 10},
                    new BuildingCount { id = "wizardtower", count = 5, maxLevel = 11},
                    new BuildingCount { id = "airsweeper", count = 2, maxLevel = 7},
                    new BuildingCount { id = "hiddentesla", count = 5, maxLevel = 10},
                    new BuildingCount { id = "bombtower", count = 2, maxLevel = 7},
                    new BuildingCount { id = "xbow", count = 4, maxLevel = 6},
                    new BuildingCount { id = "infernotower", count = 3, maxLevel = 6},
                    new BuildingCount { id = "eagleartillery", count = 1, maxLevel = 3},
                    new BuildingCount { id = "wall", count = 300, maxLevel = 13},
                    new BuildingCount { id = "boomb", count = 6, maxLevel = 8},
                    new BuildingCount { id = "springtrap", count = 8, maxLevel = 5},
                    new BuildingCount { id = "airbomb", count = 6, maxLevel = 6},
                    new BuildingCount { id = "giantbomb", count = 6, maxLevel = 5},
                    new BuildingCount { id = "seekingairmine", count = 6, maxLevel = 3},
                    new BuildingCount { id = "skeletontrap", count = 3, maxLevel = 4},
                    new BuildingCount { id = "tornadotrap", count = 1, maxLevel = 3},
                }
            },
            new BuildingAvailability
            {
                level = 13,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 7, maxLevel = 15},
                    new BuildingCount { id = "elixirmine", count = 7, maxLevel = 15},
                    new BuildingCount { id = "darkelixirmine", count = 3, maxLevel = 9},
                    new BuildingCount { id = "goldstorage", count = 4, maxLevel = 14},
                    new BuildingCount { id = "elixirstorage", count = 4, maxLevel = 14},
                    new BuildingCount { id = "darkelixirstorage", count = 1, maxLevel = 8},
                    new BuildingCount { id = "clancastle", count = 1, maxLevel = 9},
                    new BuildingCount { id = "armycamp", count = 4, maxLevel = 11},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 15},
                    new BuildingCount { id = "darkbarracks", count = 1, maxLevel = 9},
                    new BuildingCount { id = "laboratory", count = 1, maxLevel = 11},
                    new BuildingCount { id = "spellfactory", count = 1, maxLevel = 7},
                    new BuildingCount { id = "darkspellfactory", count = 1, maxLevel = 5},
                    new BuildingCount { id = "workshop", count = 1, maxLevel = 5},
                    new BuildingCount { id = "kingaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "qeenaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "wardenaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "championaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "cannon", count = 7, maxLevel = 19},
                    new BuildingCount { id = "archertower", count = 8, maxLevel = 19},
                    new BuildingCount { id = "mortor", count = 4, maxLevel = 13},
                    new BuildingCount { id = "airdefense", count = 4, maxLevel = 11},
                    new BuildingCount { id = "wizardtower", count = 5, maxLevel = 13},
                    new BuildingCount { id = "airsweeper", count = 2, maxLevel = 7},
                    new BuildingCount { id = "hiddentesla", count = 5, maxLevel = 12},
                    new BuildingCount { id = "bombtower", count = 2, maxLevel = 8},
                    new BuildingCount { id = "xbow", count = 4, maxLevel = 8},
                    new BuildingCount { id = "infernotower", count = 3, maxLevel = 7},
                    new BuildingCount { id = "eagleartillery", count = 1, maxLevel = 4},
                    new BuildingCount { id = "scattershot", count = 2, maxLevel = 2},
                    new BuildingCount { id = "wall", count = 300, maxLevel = 14},
                    new BuildingCount { id = "boomb", count = 7, maxLevel = 9},
                    new BuildingCount { id = "springtrap", count = 9, maxLevel = 5},
                    new BuildingCount { id = "airbomb", count = 6, maxLevel = 8},
                    new BuildingCount { id = "giantbomb", count = 6, maxLevel = 7},
                    new BuildingCount { id = "seekingairmine", count = 7, maxLevel = 4},
                    new BuildingCount { id = "skeletontrap", count = 3, maxLevel = 4},
                    new BuildingCount { id = "tornadotrap", count = 1, maxLevel = 3},
                }
            },
            new BuildingAvailability
            {
                level = 14,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 7, maxLevel = 15},
                    new BuildingCount { id = "elixirmine", count = 7, maxLevel = 15},
                    new BuildingCount { id = "darkelixirmine", count = 3, maxLevel = 9},
                    new BuildingCount { id = "goldstorage", count = 4, maxLevel = 15},
                    new BuildingCount { id = "elixirstorage", count = 4, maxLevel = 15},
                    new BuildingCount { id = "darkelixirstorage", count = 1, maxLevel = 9},
                    new BuildingCount { id = "clancastle", count = 1, maxLevel = 10},
                    new BuildingCount { id = "armycamp", count = 4, maxLevel = 11},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 16},
                    new BuildingCount { id = "darkbarracks", count = 1, maxLevel = 9},
                    new BuildingCount { id = "laboratory", count = 1, maxLevel = 12},
                    new BuildingCount { id = "spellfactory", count = 1, maxLevel = 7},
                    new BuildingCount { id = "darkspellfactory", count = 1, maxLevel = 5},
                    new BuildingCount { id = "workshop", count = 1, maxLevel = 6},
                    new BuildingCount { id = "pethouse", count = 1, maxLevel = 4},
                    new BuildingCount { id = "kingaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "qeenaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "wardenaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "championaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "cannon", count = 7, maxLevel = 20},
                    new BuildingCount { id = "archertower", count = 8, maxLevel = 20},
                    new BuildingCount { id = "mortor", count = 4, maxLevel = 14},
                    new BuildingCount { id = "airdefense", count = 4, maxLevel = 12},
                    new BuildingCount { id = "wizardtower", count = 5, maxLevel = 14},
                    new BuildingCount { id = "airsweeper", count = 2, maxLevel = 7},
                    new BuildingCount { id = "hiddentesla", count = 5, maxLevel = 13},
                    new BuildingCount { id = "bombtower", count = 2, maxLevel = 9},
                    new BuildingCount { id = "xbow", count = 4, maxLevel = 9},
                    new BuildingCount { id = "infernotower", count = 3, maxLevel = 8},
                    new BuildingCount { id = "eagleartillery", count = 1, maxLevel = 5},
                    new BuildingCount { id = "scattershot", count = 2, maxLevel = 3},
                    new BuildingCount { id = "wall", count = 325, maxLevel = 15},
                    new BuildingCount { id = "boomb", count = 8, maxLevel = 10},
                    new BuildingCount { id = "springtrap", count = 9, maxLevel = 5},
                    new BuildingCount { id = "airbomb", count = 7, maxLevel = 9},
                    new BuildingCount { id = "giantbomb", count = 7, maxLevel = 8},
                    new BuildingCount { id = "seekingairmine", count = 8, maxLevel = 4},
                    new BuildingCount { id = "skeletontrap", count = 4, maxLevel = 4},
                    new BuildingCount { id = "tornadotrap", count = 1, maxLevel = 3},
                }
            },
            new BuildingAvailability
            {
                level = 15,
                buildings = new BuildingCount[]
                {
                    new BuildingCount { id = "townhall", count = 1, maxLevel = 15},
                    new BuildingCount { id = "buildershut", count = 5, maxLevel = 1},
                    new BuildingCount { id = "goldmine", count = 7, maxLevel = 15},
                    new BuildingCount { id = "elixirmine", count = 7, maxLevel = 15},
                    new BuildingCount { id = "darkelixirmine", count = 3, maxLevel = 9},
                    new BuildingCount { id = "goldstorage", count = 4, maxLevel = 16},
                    new BuildingCount { id = "elixirstorage", count = 4, maxLevel = 16},
                    new BuildingCount { id = "darkelixirstorage", count = 1, maxLevel = 10},
                    new BuildingCount { id = "clancastle", count = 1, maxLevel = 11},
                    new BuildingCount { id = "armycamp", count = 4, maxLevel = 12},
                    new BuildingCount { id = "barracks", count = 1, maxLevel = 16},
                    new BuildingCount { id = "darkbarracks", count = 1, maxLevel = 9},
                    new BuildingCount { id = "laboratory", count = 1, maxLevel = 13},
                    new BuildingCount { id = "spellfactory", count = 1, maxLevel = 7},
                    new BuildingCount { id = "darkspellfactory", count = 1, maxLevel = 5},
                    new BuildingCount { id = "workshop", count = 1, maxLevel = 7},
                    new BuildingCount { id = "pethouse", count = 1, maxLevel = 8},
                    new BuildingCount { id = "kingaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "qeenaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "wardenaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "championaltar", count = 1, maxLevel = 1},
                    new BuildingCount { id = "cannon", count = 7, maxLevel = 21},
                    new BuildingCount { id = "archertower", count = 8, maxLevel = 21},
                    new BuildingCount { id = "mortor", count = 4, maxLevel = 15},
                    new BuildingCount { id = "airdefense", count = 4, maxLevel = 13},
                    new BuildingCount { id = "wizardtower", count = 5, maxLevel = 15},
                    new BuildingCount { id = "airsweeper", count = 2, maxLevel = 7},
                    new BuildingCount { id = "hiddentesla", count = 5, maxLevel = 13},
                    new BuildingCount { id = "bombtower", count = 2, maxLevel = 10},
                    new BuildingCount { id = "xbow", count = 4, maxLevel = 10},
                    new BuildingCount { id = "infernotower", count = 3, maxLevel = 9},
                    new BuildingCount { id = "eagleartillery", count = 1, maxLevel = 5},
                    new BuildingCount { id = "scattershot", count = 2, maxLevel = 3},
                    new BuildingCount { id = "spelltower", count = 2, maxLevel = 3},
                    new BuildingCount { id = "monolith", count = 1, maxLevel = 2},
                    new BuildingCount { id = "wall", count = 325, maxLevel = 16},
                    new BuildingCount { id = "boomb", count = 8, maxLevel = 11},
                    new BuildingCount { id = "springtrap", count = 9, maxLevel = 5},
                    new BuildingCount { id = "airbomb", count = 7, maxLevel = 10},
                    new BuildingCount { id = "giantbomb", count = 7, maxLevel = 8},
                    new BuildingCount { id = "seekingairmine", count = 8, maxLevel = 4},
                    new BuildingCount { id = "skeletontrap", count = 4, maxLevel = 4},
                    new BuildingCount { id = "tornadotrap", count = 1, maxLevel = 3},
                }
            },
        };

    }
}