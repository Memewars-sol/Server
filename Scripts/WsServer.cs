using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using System.IO;
using System.Net;

namespace Memewars.RealtimeNetworking.Server
{
    public class Echo: WebSocketBehavior
    {

        // Deserialize the serialized data back into a Packet object
        public static Packet Deserialize(byte[] serializedData)
        {
            Packet packet = new Packet();

            // Extract the length of the packet's content
            int contentLength = BitConverter.ToInt32(serializedData, 0);

            // Remove the length bytes from the beginning
            serializedData = serializedData.Skip(sizeof(int)).ToArray();

            // Set the bytes of the packet's content
            packet.SetBytes(serializedData);

            return packet;
        }

        // Deserialize the serialized data back into a Packet object
        private static Packet DeserializeBase64(string base64SerializedData)
        {
            // Convert the base64 string back to byte array
            byte[] serializedData = Convert.FromBase64String(base64SerializedData);

            // Deserialize the byte array back into a Packet object
            Packet packet = Deserialize(serializedData);

            return packet;
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            // base.OnMessage(e);
            Console.WriteLine("Received message from client: " + e.Data);
            // Send(e.Data);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Console.WriteLine("Disconnected: " + ID);
            base.OnClose(e);
        }
        protected override void OnOpen()
        {
            Console.WriteLine("Connected: " + ID);
            base.OnOpen();
        }
    }

    class WsServer
    {
        private class Credential
        {
            public string domain { get; set; }
            public string port { get; set; }
        }

        private static Credential wsConfig;

        private static Credential GetWsCredentials()
        {
            using (StreamReader r = new StreamReader("configs/websocket.json"))
            {
                string json = r.ReadToEnd();
                Credential credential = JsonConvert.DeserializeObject<Credential>(json);
                return credential;
            }
        }

        public static void Start()
        {
            wsConfig = GetWsCredentials();
            WebSocketServer wssv = new WebSocketServer(String.Format("{0}:{1}", wsConfig.domain, wsConfig.port));

            wssv.AddWebSocketService<Echo>("/Echo");

            wssv.Start();
            // Console.WriteLine($"Wsserver started on {wsConfig.domain}:{wsConfig.port}/Echo");
            Console.WriteLine(String.Format("Wsserver started on  {0}:{1}/Echo", wsConfig.domain, wsConfig.port));

            // Console.Read();
            // wssv.Stop();
        }
    }
}