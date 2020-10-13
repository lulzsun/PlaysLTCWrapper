using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimpleTCP;

namespace PlaysLTCWrapper {
    class LTCProcess {
        SimpleTcpServer server;
        Process ltcProcess;
        public void Connect() {
            Process currentProcess = Process.GetCurrentProcess();
            string pid = currentProcess.Id.ToString();
            int port = 9500;

            server = new SimpleTcpServer().Start(port);

            server.Delimiter = Convert.ToByte('\f');
            server.DelimiterDataReceived += (sender, msg) => {
                string jsonData = msg.MessageString;
                JObject jsonObject = GetDataType(jsonData);
                string type = jsonObject["type"].ToString();

                switch (type) {
                    case "LTC:handshake":
                        GetEncoderSupportLevel();
                        SetSavePaths("F:\\Videos\\Plays\\", "F:\\Videos\\Plays\\.temp\\");
                        SetGameDVRQuality(10, 30, 720);

                        ConnectionHandshakeArgs connectionHandshakeArgs = new ConnectionHandshakeArgs {
                            Version = jsonObject["data"]["version"].ToString(),
                            IntegrityCheck = jsonObject["data"]["integrityCheck"].ToString()
                        };
                        OnConnectionHandshake(connectionHandshakeArgs);
                        break;
                    case "LTC:processCreated":
                        ProcessCreatedArgs processCreatedArgs = new ProcessCreatedArgs {
                            Pid = Int32.Parse(jsonObject["data"]["pid"].ToString()),
                            ExeFile = jsonObject["data"]["exeFile"].ToString(),
                            CmdLine = jsonObject["data"]["cmdLine"].ToString()
                        };
                        OnProcessCreated(processCreatedArgs);
                        break;
                    case "LTC:processTerminated":
                        ProcessTerminatedArgs processTerminatedArgs = new ProcessTerminatedArgs {
                            Pid = Int32.Parse(jsonObject["data"]["pid"].ToString())
                        };
                        OnProcessTerminated(processTerminatedArgs);
                        break;
                    case "LTC:graphicsLibLoaded":
                        GraphicsLibLoadedArgs graphicsLibLoadedArgs = new GraphicsLibLoadedArgs {
                            Pid = Int32.Parse(jsonObject["data"]["pid"].ToString()),
                            ModuleName = jsonObject["data"]["moduleName"].ToString()
                        };
                        OnGraphicsLibLoaded(graphicsLibLoadedArgs);
                        break;
                    case "LTC:moduleLoaded":
                        ModuleLoadedArgs moduleLoadedArgs = new ModuleLoadedArgs {
                            Pid = Int32.Parse(jsonObject["data"]["pid"].ToString()),
                            ModuleName = jsonObject["data"]["moduleName"].ToString()
                        };
                        OnModuleLoaded(moduleLoadedArgs);
                        break;
                    case "LTC:gameLoaded":
                        GameLoadedArgs gameLoadedArgs = new GameLoadedArgs {
                            Pid = Int32.Parse(jsonObject["data"]["pid"].ToString()),
                            Width = Int32.Parse(jsonObject["data"]["size"]["width"].ToString()),
                            Height = Int32.Parse(jsonObject["data"]["size"]["height"].ToString())
                        };
                        OnGameLoaded(gameLoadedArgs);
                        break;
                    case "LTC:videoCaptureReady":
                        VideoCaptureReadyArgs videoCaptureReadyArgs = new VideoCaptureReadyArgs {
                            Pid = Int32.Parse(jsonObject["data"]["pid"].ToString())
                        };
                        OnVideoCaptureReady(videoCaptureReadyArgs);
                        break;
                    case "LTC:recordingError":
                        Console.WriteLine("Recording Error code: {0}", Int32.Parse(jsonObject["data"]["code"].ToString()));
                        break;
                    case "LTC:gameScreenSizeChanged":
                        Console.WriteLine("Game screen size changed, {0}x{1}", jsonObject["data"]["width"], jsonObject["data"]["height"]);
                        break;
                    case "LTC:saveStarted":
                        Console.WriteLine("Started saving recording to file, {0}", jsonObject["data"]["filename"]);
                        break;
                    case "LTC:saveFinished":
                        Console.WriteLine("Finished saving recording to file, {0}, {1}x{2}, {3}, {4}", 
                                            jsonObject["data"]["fileName"], 
                                            jsonObject["data"]["width"], 
                                            jsonObject["data"]["height"], 
                                            jsonObject["data"]["duration"], 
                                            jsonObject["data"]["recMode"]);
                        break;
                    default:
                        Console.WriteLine("WARNING: WAS SENT AN EVENT THAT DOES NOT MATCH CASE: {0}", jsonObject);
                        break;
                }
            };

            ltcProcess = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = Environment.GetEnvironmentVariable("LocalAppData") + @"\Plays-ltc\0.54.7\PlaysTVComm.exe",
                    Arguments = port + " " + pid + "",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = false
                }
            };

            ltcProcess.Start();
            while (!ltcProcess.StandardOutput.EndOfStream) {
                string line = ltcProcess.StandardOutput.ReadLine();
                Console.WriteLine(line);
            }

            Console.WriteLine("PlaysTVComm.exe has ended!!!");
            ltcProcess.Close();
        }

        public void GetEncoderSupportLevel() {
            Emit("LTC:getEncoderSupportLevel");
        }

        public void SetSavePaths(string saveFolder, string tempFolder) {
            Emit("LTC:setSavePaths",
            "{" +
                "'saveFolder': '" + saveFolder + "', " +
                "'tempFolder': '" + tempFolder + "'" +
            "}");
        }

        public void ScanForGraphLib(int pid) {
            string data =
            "{" +
                "'pid': " + pid +
            "}";
            Emit("LTC:scanForGraphLib", data);
        }

        public void SetGameName(string name) {
            string data =
            "{" +
                "'gameName': '" + Regex.Replace(name, "[/:*?\"<>|]", "") + "'" +
            "}";
            Emit("LTC:setGameName", data);
        }

        public void LoadGameModule(int pid) {
            string data =
            "{" +
                "'pid': " + pid +
            "}";
            Emit("LTC:loadGameModule", data);
        }

        public void SetCaptureMode(int mode) {
            string data =
            "{" +
                "'captureMode': " + mode +
            "}";
            Emit("LTC:setCaptureMode", data);
        }

        public void SetGameDVRCaptureEngine(int engine) {
            string data =
            "{" +
                "'engine': " + engine + ", " +
                "'previewMode': false" +
            "}";
            Emit("LTC:setGameDVRCaptureEngine", data);
        }

        public void SetKeyBinds(string keyBinds = "[]") {
            string data =
            "{" +
                "'keyBinds': " + keyBinds +
            "}";
            Emit("LTC:setKeyBinds", data);
        }

        public void StartRecording() {
            Emit("LTC:startRecording");
        }

        public void StopRecording() {
            Emit("LTC:stopRecording");
        }


        public void SetGameDVRQuality(int bitRate, int frameRate, int videoResolution) {
            Emit("LTC:setGameDVRQuality",
            "{" +
                "'bitRate': " + bitRate + ", " +
                "'frameRate': " + frameRate + ", " +
                "'videoResolution': " + videoResolution +
            "}");
        }

        public void Emit(string type, string data = "{}") {
            data = data.Replace("'", "\"");
            string json = "{ \"type\": \"" + type + "\", \"data\": " + data + " }\f";
            //Console.WriteLine(json);
            if(server != null && server.IsStarted)
                server.Broadcast(json);
        }

        public JObject GetDataType(string jsonData) {
            JObject jsonObject = JsonConvert.DeserializeObject<JObject>(jsonData);
            //Console.WriteLine(jsonObject);
            //Console.WriteLine("===================================");

            return jsonObject;
        }

        #region ConnectionHandshake
        public class ConnectionHandshakeArgs : EventArgs {
            public string Version { get; internal set; }
            public string IntegrityCheck { get; internal set; }
        }
        public event EventHandler<ConnectionHandshakeArgs> ConnectionHandshake;
        protected virtual void OnConnectionHandshake(ConnectionHandshakeArgs e) {
            ConnectionHandshake?.Invoke(this, e);
        }
        #endregion

        #region ProcessCreated
        public class ProcessCreatedArgs : EventArgs { 
            public int Pid { get; internal set; }
            public string ExeFile { get; internal set; }
            public string CmdLine { get; internal set; }
        }
        public event EventHandler<ProcessCreatedArgs> ProcessCreated;
        protected virtual void OnProcessCreated(ProcessCreatedArgs e) {
            ProcessCreated?.Invoke(this, e);
        }
        #endregion

        #region ProcessTerminated
        public class ProcessTerminatedArgs : EventArgs {
            public int Pid { get; internal set; }
        }
        public event EventHandler<ProcessTerminatedArgs> ProcessTerminated;
        protected virtual void OnProcessTerminated(ProcessTerminatedArgs e) {
            ProcessTerminated?.Invoke(this, e);
        }
        #endregion

        #region GraphicsLibLoaded
        public class GraphicsLibLoadedArgs : EventArgs {
            public int Pid { get; internal set; }
            public string ModuleName { get; internal set; }
        }
        public event EventHandler<GraphicsLibLoadedArgs> GraphicsLibLoaded;
        protected virtual void OnGraphicsLibLoaded(GraphicsLibLoadedArgs e) {
            GraphicsLibLoaded?.Invoke(this, e);
        }
        #endregion

        #region ModuleLoaded
        public class ModuleLoadedArgs : EventArgs {
            public int Pid { get; internal set; }
            public string ModuleName { get; internal set; }
        }
        public event EventHandler<ModuleLoadedArgs> ModuleLoaded;
        protected virtual void OnModuleLoaded(ModuleLoadedArgs e) {
            ModuleLoaded?.Invoke(this, e);
        }
        #endregion

        #region GameLoaded
        public class GameLoadedArgs : EventArgs {
            public int Pid { get; internal set; }
            public int Width { get; internal set; }
            public int Height { get; internal set; }
        }
        public event EventHandler<GameLoadedArgs> GameLoaded;
        protected virtual void OnGameLoaded(GameLoadedArgs e) {
            GameLoaded?.Invoke(this, e);
        }
        #endregion

        #region VideoCaptureReady
        public class VideoCaptureReadyArgs : EventArgs {
            public int Pid { get; internal set; }
            public int Width { get; internal set; }
            public int Height { get; internal set; }
        }
        public event EventHandler<VideoCaptureReadyArgs> VideoCaptureReady;
        protected virtual void OnVideoCaptureReady(VideoCaptureReadyArgs e) {
            VideoCaptureReady?.Invoke(this, e);
        }
        #endregion
    }
}
