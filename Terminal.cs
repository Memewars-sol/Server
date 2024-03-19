using System;
using System.Numerics;
using System.IO;
using Newtonsoft.Json;
using Chaos.NaCl;
using System.Text;
using SimpleBase;
using Models;
using System.Diagnostics;

namespace Memewars.RealtimeNetworking.Server
{
    class Terminal
    {

        private class TerminalConfig 
        {
            public string[] clientVersions { get; set; }
            public int maxPlayers { get; set; }
            public int portNumber { get; set; }
        }

        private class FolderConfig 
        {
            public string dataFolderPath { get; set; }
            public string logFolderPath { get; set; }
        }

        #region Update
        public const int updatesPerSecond = 30;
        public static void Update()
        {
            Database.Update();
        }
        #endregion

        #region Initialization
        public static string[] clientVersions;
        public static int maxPlayers;
        public static int portNumber;
        public static int onlinePlayers;
        public static string dataFolderPath;
        public static string logFolderPath;
        private static void GetTerminalConfig()
        {
            using (StreamReader r = new StreamReader("configs/terminal.json"))
            {
                string json = r.ReadToEnd();
                TerminalConfig config = JsonConvert.DeserializeObject<TerminalConfig>(json);
                clientVersions = config.clientVersions;
                maxPlayers = config.maxPlayers;
                portNumber = config.portNumber;
            }
        }
        private static void GetFolderConfig()
        {
            using (StreamReader r = new StreamReader("configs/terminal.json"))
            {
                string json = r.ReadToEnd();
                FolderConfig config = JsonConvert.DeserializeObject<FolderConfig>(json);
                dataFolderPath = config.dataFolderPath;
                logFolderPath = config.logFolderPath;
            }
        }

        static Terminal()
        {
            GetTerminalConfig();
            GetFolderConfig();
        }

        public static void OnClientConnected(int id, string ip)
        {
            if (!Server.clients[id].connected)
            {
                Server.clients[id].connected = true;
                UpdateLog(1);
            }
        }

        public static void OnClientDisconnected(int id, string ip)
        {
            Database.PlayerDisconnected(id);
            if (Server.clients[id].connected)
            {
                Server.clients[id].connected = false;
                UpdateLog(-1);
            }
        }

        public static void UpdateLog(int amount)
        {
            onlinePlayers += amount;
            // Console.Clear();
            Console.WriteLine("Online Players: " + onlinePlayers.ToString());
        }

        public static void OnServerStarted()
        {
            Database.Initialize();
        }
        #endregion

        #region Data
        public enum RequestsID
        {
            AUTH = 1, 
            SYNC = 2, 
            BUILD = 3, 
            REPLACE = 4, 
            COLLECT = 5, 
            PREUPGRADE = 6, 
            UPGRADE = 7,
            INSTANTBUILD = 8, 
            TRAIN = 9, 
            CANCELTRAIN = 10, 
            BATTLEFIND = 11, 
            BATTLESTART = 12, 
            BATTLEFRAME = 13, 
            BATTLEEND = 14, 
            OPENCLAN = 15, 
            GETCLANS = 16, 
            JOINCLAN = 17, 
            LEAVECLAN = 18, 
            EDITCLAN = 19, 
            CREATECLAN = 20, 
            OPENWAR = 21, 
            STARTWAR = 22, 
            CANCELWAR = 23, 
            WARSTARTED = 24, 
            WARATTACK = 25, 
            WARREPORTLIST = 26, 
            WARREPORT = 27, 
            JOINREQUESTS = 28, 
            JOINRESPONSE = 29, 
            GETCHATS = 30, 
            SENDCHAT = 31, 
            SENDCODE = 32, 
            CONFIRMCODE = 33, 
            EMAILCODE = 34, 
            EMAILCONFIRM = 35, 
            LOGOUT = 36, 
            KICKMEMBER = 37, 
            BREW = 38, 
            CANCELBREW = 39, 
            RESEARCH = 40, 
            PROMOTEMEMBER = 41, 
            DEMOTEMEMBER = 42, 
            SCOUT = 43, 
            BUYSHIELD = 44, 
            BUYGEM = 45, 
            BUYGOLD = 46, 
            REPORTCHAT = 47, 
            PLAYERSRANK = 48, 
            BOOST = 49, 
            BUYRESOURCE = 50, 
            
            BATTLEREPORTS = 51, 
            BATTLEREPORT = 52, 
            RENAME = 53, 
            PREAUTH = 54,
            GET_LAND = 55,
            GET_MAP = 56,
            GET_GUILD = 57,
            GET_ALL_GUILDS = 58,
            GET_FORUM_POSTS = 59,
            GET_FORUM_POST = 60,
            JOIN_GUILD = 61,
            CHANGE_GUILD = 62,
            EXIT_GUILD = 63,
            CREATE_FORUM_POST = 64,
            CREATE_FORUM_COMMENT = 65,
            PUSH_FORUM_POST_TO_GOVERNANCE = 66,
            BUY_LAND = 67,
            BUY_LAND_CALLBACK = 68,
            DELETE_FORUM_COMMENT = 69,
            DELETE_FORUM_POST = 70,
        }

        public static void ReceivedPacket(int clientID, Packet packet)
        {
            try
            {
                int id = packet.ReadInt();
                long guild_id;
                long forum_post_id;
                Guild guild;
                Player account;
                ForumPost forumPost;
                ForumComment forumComment;

                // authentication
                string address = packet.ReadString();
                string signature = packet.ReadString();
                string authMessage = packet.ReadString();

                byte[] addressBytes = Base58.Bitcoin.Decode(address);
                byte[] signatureBytes = Convert.FromBase64String(signature);
                byte[] authMessageBytes = Encoding.UTF8.GetBytes(authMessage);

                bool IsVerified = Ed25519.Verify(signatureBytes, authMessageBytes, addressBytes);
                
                // Console.WriteLine("===== AUTH =====");
                // Console.WriteLine(address);
                // Console.WriteLine(signature);
                // Console.WriteLine(authMessage);
                // Console.WriteLine(IsVerified);
                // Console.WriteLine("===== END AUTH =====");

                int retCode = IsVerified? 1 : 0;
                Packet retPacket = new Packet();

                long databaseID = 0;

                // filter all methods to be authenticated
                if(!IsVerified) {
                    Console.WriteLine("Not verified");
                    retPacket.Write(retCode);
                    Sender.TCP_Send(clientID, retPacket);
                    return;
                }

                switch ((RequestsID)id)
                {
                    case RequestsID.PREAUTH:
                        retPacket.Write((int)RequestsID.PREAUTH);
                        retPacket.Write(retCode);
                        Sender.TCP_Send(clientID, retPacket);
                        break;
                    case RequestsID.AUTH:
                        Database.AuthenticatePlayer(clientID, address);
                        break;
                    case RequestsID.SYNC:
                        Database.SyncPlayerData(clientID);
                        break;
                    case RequestsID.BUILD:
                        string building = packet.ReadString();
                        int x = packet.ReadInt();
                        int y = packet.ReadInt();
                        int layoutBuild = packet.ReadInt();
                        databaseID = packet.ReadLong();
                        Database.PlaceBuilding(clientID, building, x, y, layoutBuild, databaseID);
                        break;
                    case RequestsID.REPLACE:
                        databaseID = packet.ReadLong();
                        int replaceX = packet.ReadInt();
                        int replaceY = packet.ReadInt();
                        int layoutReplace = packet.ReadInt();
                        Database.ReplaceBuilding(clientID, databaseID, replaceX, replaceY, layoutReplace);
                        break;
                    case RequestsID.COLLECT:
                        long dbid = packet.ReadLong();
                        Database.Collect(clientID, dbid);
                        break;
                    case RequestsID.PREUPGRADE:
                        //databaseID = packet.ReadLong();
                        //Database.GetNextLevelRequirements(clientID, databaseID);
                        break;
                    case RequestsID.UPGRADE:
                        databaseID = packet.ReadLong();
                        Database.UpgradeBuilding(clientID, databaseID);
                        break;
                    case RequestsID.INSTANTBUILD:
                        databaseID = packet.ReadLong();
                        Database.InstantBuild(clientID, databaseID);
                        break;
                    case RequestsID.TRAIN:
                        string globalID = packet.ReadString();
                        Database.TrainUnit(clientID, globalID);
                        break;
                    case RequestsID.CANCELTRAIN:
                        databaseID = packet.ReadLong();
                        Database.CancelTrainUnit(clientID, databaseID);
                        break;
                    case RequestsID.BATTLEFIND:
                        Database.FindBattleTarget(clientID);
                        break;
                    case RequestsID.BATTLESTART:
                        int bytesLength = packet.ReadInt();
                        byte[] bytes = packet.ReadBytes(bytesLength);
                        int battleType = packet.ReadInt();
                        Database.StartBattle(clientID, bytes, (BattleType)battleType);
                        break;
                    case RequestsID.BATTLEFRAME:
                        int bfl = packet.ReadInt();
                        byte[] bf = packet.ReadBytes(bfl);
                        Database.AddBattleFrame(clientID, bf);
                        break;
                    case RequestsID.BATTLEEND:
                        databaseID = Server.clients[clientID].account;
                        bool surrender = packet.ReadBool();
                        int frame = packet.ReadInt();
                        Database.EndBattle(databaseID, surrender, frame);
                        break;
                    case RequestsID.SENDCHAT:
                        string message = packet.ReadString();
                        int sendType = packet.ReadInt();
                        databaseID = packet.ReadLong();
                        Database.SendChatMessage(clientID, message, (Data.ChatType)sendType, databaseID);
                        break;
                    case RequestsID.GETCHATS:
                        int msgType = packet.ReadInt();
                        databaseID = packet.ReadLong();
                        Database.SyncMessages(clientID, (Data.ChatType)msgType, databaseID);
                        break;

                    // email logins
                    // unused
                    case RequestsID.SENDCODE:
                        string targetEmail = packet.ReadString();
                        Database.SendRecoveryCode(clientID, address, targetEmail);
                        break;
                    case RequestsID.CONFIRMCODE:
                        string confirmEmail = packet.ReadString();
                        string code = packet.ReadString();
                        Database.ConfirmRecoveryCode(clientID, address, confirmEmail, code);
                        break;
                    case RequestsID.EMAILCODE:
                        string sendEmail = packet.ReadString();
                        Database.SendEmailCode(clientID, address, sendEmail);
                        break;
                    case RequestsID.EMAILCONFIRM:
                        string coEmail = packet.ReadString();
                        string codeEmail = packet.ReadString();
                        Database.ConfirmEmailCode(clientID, address, coEmail, codeEmail);
                        break;
                    // end email logins
                    
                    case RequestsID.LOGOUT:
                        Account.LogOut(address);
                        break;
                    case RequestsID.BREW:
                        string spellID = packet.ReadString();
                        Database.BrewSpell(clientID, spellID);
                        break;
                    case RequestsID.CANCELBREW:
                        databaseID = packet.ReadLong();
                        Database.CancelBrewSpell(clientID, databaseID);
                        break;
                    case RequestsID.RESEARCH:
                        int type = packet.ReadInt();
                        string global_id = packet.ReadString();
                        Database.DoResearch(clientID, (ResearchType)type, global_id);
                        break;
                    case RequestsID.SCOUT:
                        databaseID = packet.ReadLong();
                        int scoutType = packet.ReadInt();
                        Database.Scout(clientID, databaseID, scoutType);
                        break;
                    // todo
                    case RequestsID.BUYGEM:
                        break;
                    case RequestsID.BUYSHIELD:
                        break;
                    case RequestsID.BUYGOLD:
                        break;
                    case RequestsID.PLAYERSRANK:
                        int p = packet.ReadInt();
                        Account.GetPlayersRanking(clientID, p);
                        break;
                    case RequestsID.BOOST:
                        databaseID = packet.ReadLong();
                        Database.BoostResources(clientID, databaseID);
                        break;
                    case RequestsID.BUYRESOURCE:
                        int resPack = packet.ReadInt();
                        Database.BuyResources(clientID, resPack);
                        break;
                    case RequestsID.BATTLEREPORTS:
                        Database.GetBattlesList(clientID);
                        break;
                    case RequestsID.BATTLEREPORT:
                        long repId = packet.ReadLong();
                        Database.GetBattleReport(clientID, repId);
                        break;
                    case RequestsID.RENAME:
                        string nm = packet.ReadString();
                        Account.UpdateName(clientID, nm);
                        break;

                    // land
                    case RequestsID.GET_LAND:
                        retPacket.Write((int)RequestsID.GET_LAND);
                        var land_id = packet.ReadLong();
                        var land = new Land(land_id);
                        string landData = Data.Serialize(land);
                        byte[] landBytes = Data.Compress(landData);
                        retPacket.Write(1);
                        retPacket.Write(landBytes.Length);
                        retPacket.Write(landBytes);
                        Sender.TCP_Send(clientID, retPacket);
                        break;

                    case RequestsID.GET_MAP:
                        retPacket.Write((int)RequestsID.GET_MAP);
                        var lands = Land.All();
                        string landsData = Data.Serialize(lands);
                        byte[] landsBytes = Data.Compress(landsData);
                        retPacket.Write(1);
                        retPacket.Write(landsBytes.Length);
                        retPacket.Write(landsBytes);
                        Sender.TCP_Send(clientID, retPacket);
                        break;

                    case RequestsID.BUY_LAND:
                        retPacket.Write((int)RequestsID.BUY_LAND);
                        account = Account.GetByAddress(address);
                        land_id = packet.ReadLong();
                        land = new Land(land_id);

                        if(land.IsBooked) {
                            // already booked
                            retPacket.Write(0);
                            Sender.TCP_Send(clientID, retPacket);
                            break;
                        }

                        land.Book(account.id);
                        retPacket.Write(1);
                        Sender.TCP_Send(clientID, retPacket);
                        break;

                    // guild
                    case RequestsID.GET_GUILD:
                        retPacket.Write((int)RequestsID.GET_GUILD);
                        guild_id = packet.ReadLong();
                        guild = new Guild(guild_id);
                        string guildData = Data.Serialize(guild);
                        byte[] guildBytes = Data.Compress(guildData);
                        retPacket.Write(1);
                        retPacket.Write(guildBytes.Length);
                        retPacket.Write(guildBytes);
                        Sender.TCP_Send(clientID, retPacket);
                        break;
                    case RequestsID.GET_ALL_GUILDS:
                        retPacket.Write((int)RequestsID.GET_ALL_GUILDS);
                        var guilds = Guild.All();
                        string guildsData = Data.Serialize(guilds);
                        byte[] guildsBytes = Data.Compress(guildsData);
                        retPacket.Write(1);
                        retPacket.Write(guildsBytes.Length);
                        retPacket.Write(guildsBytes);
                        Sender.TCP_Send(clientID, retPacket);
                        break;
                        
                    case RequestsID.JOIN_GUILD:
                        retPacket.Write((int)RequestsID.JOIN_GUILD);
                        guild_id = packet.ReadLong();
                        account = Account.GetByAddress(address);
                        Guild.Join(guild_id, account.id);
                        retPacket.Write(1);
                        Sender.TCP_Send(clientID, retPacket);
                        break;

                    case RequestsID.CHANGE_GUILD:
                        retPacket.Write((int)RequestsID.CHANGE_GUILD);
                        guild_id = packet.ReadLong();
                        account = Account.GetByAddress(address);
                        Guild.Join(guild_id, account.id);
                        retPacket.Write(1);
                        Sender.TCP_Send(clientID, retPacket);
                        break;

                    case RequestsID.EXIT_GUILD:
                        retPacket.Write((int)RequestsID.JOIN_GUILD);
                        account = Account.GetByAddress(address);
                        Guild.Exit(account.id);
                        retPacket.Write(1);
                        Sender.TCP_Send(clientID, retPacket);
                        break;

                    // forums
                    case RequestsID.GET_FORUM_POSTS:
                        retPacket.Write((int)RequestsID.GET_FORUM_POSTS);
                        account = Account.GetByAddress(address);

                        if(account.guild_id == 0) {
                            retPacket.Write(0);
                            Sender.TCP_Send(clientID, retPacket);
                            break;
                        }

                        var forumPosts = ForumPost.All(account.guild_id);
                        string forumPostData = Data.Serialize(forumPosts);
                        byte[] forumPostBytes = Data.Compress(forumPostData);
                        retPacket.Write(1);
                        retPacket.Write(forumPostBytes.Length);
                        retPacket.Write(forumPostBytes);
                        Sender.TCP_Send(clientID, retPacket);
                        break;

                    case RequestsID.GET_FORUM_POST:
                        retPacket.Write((int)RequestsID.GET_FORUM_POST);
                        forum_post_id = packet.ReadLong();
                        account = Account.GetByAddress(address);
                        var post = new ForumPost(forum_post_id);

                        if(post.GuildId != account.guild_id) {
                            retPacket.Write(0);
                            Sender.TCP_Send(clientID, retPacket);
                            break;
                        }

                        string postData = Data.Serialize(post);
                        byte[] postBytes = Data.Compress(postData);
                        retPacket.Write(1);
                        retPacket.Write(postBytes.Length);
                        retPacket.Write(postBytes);
                        Sender.TCP_Send(clientID, retPacket);
                        break;

                    case RequestsID.CREATE_FORUM_POST:
                        retPacket.Write((int)RequestsID.CREATE_FORUM_POST);
                        account = Account.GetByAddress(address);
                        string title = packet.ReadString();
                        string description = packet.ReadString();
                        string content = packet.ReadString();
                        post = new ForumPost() {
                            Title = title,
                            Description = description,
                            Content = content,
                            GuildId = account.guild_id,
                            CreatedBy = account.address,
                        };
                        post.Create();
                        retPacket.Write(1);
                        Sender.TCP_Send(clientID, retPacket);
                        break;

                    case RequestsID.CREATE_FORUM_COMMENT:
                        retPacket.Write((int)RequestsID.CREATE_FORUM_COMMENT);
                        account = Account.GetByAddress(address);
                        forum_post_id = packet.ReadLong();
                        forumPost = new ForumPost(forum_post_id);

                        if(forumPost.CreatedBy == "" || forumPost.GuildId != account.guild_id) {
                            // not created yet or not accessible
                            retPacket.Write(0);
                            Sender.TCP_Send(clientID, retPacket);
                            break;
                        }

                        string comment = packet.ReadString();
                        forumComment = new ForumComment() {
                            Comment = comment,
                            CreatedBy = account.address,
                        };
                        forumComment.Create();
                        retPacket.Write(1);
                        Sender.TCP_Send(clientID, retPacket);
                        break;

                    case RequestsID.DELETE_FORUM_COMMENT:
                        retPacket.Write((int)RequestsID.DELETE_FORUM_COMMENT);
                        account = Account.GetByAddress(address);
                        var forum_comment_id = packet.ReadLong();
                        forumComment = new ForumComment(forum_comment_id);

                        if(forumComment.CreatedBy != account.address) {
                            // unauthorized
                            retPacket.Write(0);
                            Sender.TCP_Send(clientID, retPacket);
                            break;
                        }

                        forumComment.Delete();
                        retPacket.Write(1);
                        Sender.TCP_Send(clientID, retPacket);
                        break;

                    case RequestsID.DELETE_FORUM_POST:
                        retPacket.Write((int)RequestsID.DELETE_FORUM_POST);
                        account = Account.GetByAddress(address);
                        forum_post_id = packet.ReadLong();
                        forumPost = new ForumPost(forum_post_id);

                        if(forumPost.CreatedBy != account.address) {
                            // unauthorized
                            retPacket.Write(0);
                            Sender.TCP_Send(clientID, retPacket);
                            break;
                        }

                        forumPost.Delete();
                        retPacket.Write(1);
                        Sender.TCP_Send(clientID, retPacket);
                        break;

                    case RequestsID.PUSH_FORUM_POST_TO_GOVERNANCE:
                        retPacket.Write((int)RequestsID.PUSH_FORUM_POST_TO_GOVERNANCE);
                        account = Account.GetByAddress(address);
                        forum_post_id = packet.ReadLong();
                        forumPost = new ForumPost(forum_post_id);

                        if(forumPost.CreatedBy == "" || forumPost.GuildId != account.guild_id) {
                            // not created yet or not accessible
                            retPacket.Write(0);
                            Sender.TCP_Send(clientID, retPacket);
                            break;
                        }

                        forumPost.PushToGovernance();
                        retPacket.Write(1);
                        Sender.TCP_Send(clientID, retPacket);
                        break;
                }
            }
            catch (Exception ex)
            {
                Tools.LogError(ex.Message, ex.StackTrace);
            }
        }

        public static void ReceivedBytes(int clientID, int packetID, byte[] data)
        {

        }

        public static void ReceivedString(int clientID, int packetID, string data)
        {

        }

        public static void ReceivedInteger(int clientID, int packetID, int data)
        {

        }

        public static void ReceivedFloat(int clientID, int packetID, float data)
        {

        }

        public static void ReceivedBoolean(int clientID, int packetID, bool data)
        {

        }

        public static void ReceivedVector3(int clientID, int packetID, Vector3 data)
        {

        }

        public static void ReceivedQuaternion(int clientID, int packetID, Quaternion data)
        {

        }

        public static void ReceivedLong(int clientID, int packetID, long data)
        {

        }

        public static void ReceivedShort(int clientID, int packetID, short data)
        {

        }

        public static void ReceivedByte(int clientID, int packetID, byte data)
        {

        }

        public static void ReceivedEvent(int clientID, int packetID)
        {

        }
        #endregion

    }
}