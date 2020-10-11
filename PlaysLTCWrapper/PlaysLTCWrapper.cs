using System;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimpleTCP;

namespace PlaysLTCWrapper {
    class LTCProcess {
        public void connect() {
            Process currentProcess = Process.GetCurrentProcess();
            string pid = currentProcess.Id.ToString();
            int port = 9500;

            var server = new SimpleTcpServer().Start(9500);

            server.Delimiter = Convert.ToByte('\f');
            server.DelimiterDataReceived += (sender, msg) => {
                string jsonData = msg.MessageString;
                JObject jsonObject = GetDataType(jsonData);
                string type = jsonObject["type"].ToString();

                if (type == "LTC:handshake") {
                    Emit(server, "LTC:getEncoderSupportLevel");
                    Emit(server, "LTC:setSavePaths",
                        @"{ 
                            'saveFolder': 'F:\\Videos\\Plays\\',
                            'tempFolder': 'F:\\Videos\\Plays\\.temp\\',
                        }");
                    Emit(server, "LTC:setGameDVRQuality",
                        @"{ 
                            'bitRate': 10,
                            'frameRate': 30,
                            'videoResolution': 720,
                        }");
                } else if (type == "LTC:processCreated") {
                    string data =
                        "{" +
                            "'pid': " + jsonObject["data"]["pid"] +
                        "}";
                    //Emit(server, "LTC:scanForGraphLib", data);
                }
                Console.WriteLine("===================================");
            };

            var proc = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = Environment.GetEnvironmentVariable("LocalAppData") + @"\Plays-ltc\0.54.7\PlaysTVComm.exe",
                    Arguments = port + " " + pid + "",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = false
                }
            };

            proc.Start();
            while (!proc.StandardOutput.EndOfStream) {
                string line = proc.StandardOutput.ReadLine();
                Console.WriteLine(line);
            }

            Console.WriteLine("PlaysTVComm.exe has ended!!!");
            Console.ReadKey();

            proc.Close();
        }

        static JObject GetDataType(string jsonData) {
            JObject jsonObject = JsonConvert.DeserializeObject<JObject>(jsonData);
            Console.WriteLine(jsonObject);

            return jsonObject;
        }

        static void Emit(SimpleTcpServer server, string type, string data = "{}") {
            data = data.Replace("'", "\"");
            string json = "{ \"type\": \"" + type + "\", \"data\": " + data + " }\f";
            Console.WriteLine(json);
            server.Broadcast(json);
        }
    }
}
