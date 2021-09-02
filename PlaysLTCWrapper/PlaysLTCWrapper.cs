using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace PlaysLTCWrapper {
    public class LTCProcess {
        TcpListener server;
        NetworkStream ns;
        Process ltcProcess;
        public void Connect() {
            Process currentProcess = Process.GetCurrentProcess();
            string pid = currentProcess.Id.ToString();
            int port = 9500;
            server = new TcpListener(IPAddress.Any, port);
            server.Start();

            ltcProcess = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = Environment.GetEnvironmentVariable("LocalAppData") + @"\Plays-ltc\0.54.7\PlaysTVComm.exe",
                    Arguments = port + " " + pid + "",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = false
                }
            };

            ltcProcess.OutputDataReceived += new DataReceivedEventHandler((s, e) => {
                PrettyConsole.Writeline(ConsoleColor.DarkCyan, "LTCPROCESS: ", e.Data);
            });

            ltcProcess.Start();
            while (true) {
                TcpClient client = server.AcceptTcpClient();
                ns = client.GetStream();

                while (client.Connected) {
                    int streamByte = ns.ReadByte();
                    StringBuilder stringBuilder = new StringBuilder();

                    while (streamByte != 12)
                    {
                        stringBuilder.Append((char)streamByte);
                        streamByte = ns.ReadByte();
                    }

                    string msg = stringBuilder.ToString().Replace("\n", "").Replace("\r", "").Trim();
                    PrettyConsole.Writeline(ConsoleColor.Cyan, "RECEIVED: ", msg);

                    JsonElement jsonElement = GetDataType(msg);
                    string type = jsonElement.GetProperty("type").GetString();
                    var data = jsonElement.GetProperty("data");

                    switch (type)
                    {
                        case "LTC:handshake":
                            GetEncoderSupportLevel();
                            SetSavePaths("G:/Videos/Plays/", "G:/Videos/Plays/.temp/");
                            SetGameDVRQuality(10, 30, 720);

                            ConnectionHandshakeArgs connectionHandshakeArgs = new ConnectionHandshakeArgs
                            {
                                Version = data.GetProperty("version").ToString(),
                                IntegrityCheck = data.GetProperty("integrityCheck").ToString(),
                            };
                            OnConnectionHandshake(connectionHandshakeArgs);
                            break;
                        case "LTC:processCreated":
                            ProcessCreatedArgs processCreatedArgs = new ProcessCreatedArgs
                            {
                                Pid = data.GetProperty("pid").GetInt32(),
                                ExeFile = data.GetProperty("exeFile").GetString(),
                                CmdLine = data.GetProperty("cmdLine").GetString()
                            };
                            OnProcessCreated(processCreatedArgs);
                            break;
                        case "LTC:processTerminated":
                            ProcessTerminatedArgs processTerminatedArgs = new ProcessTerminatedArgs
                            {
                                Pid = data.GetProperty("pid").GetInt32(),
                            };
                            OnProcessTerminated(processTerminatedArgs);
                            break;
                        case "LTC:graphicsLibLoaded":
                            GraphicsLibLoadedArgs graphicsLibLoadedArgs = new GraphicsLibLoadedArgs
                            {
                                Pid = data.GetProperty("pid").GetInt32(),
                                ModuleName = data.GetProperty("moduleName").GetString()
                            };
                            OnGraphicsLibLoaded(graphicsLibLoadedArgs);
                            break;
                        case "LTC:moduleLoaded":
                            ModuleLoadedArgs moduleLoadedArgs = new ModuleLoadedArgs
                            {
                                Pid = data.GetProperty("pid").GetInt32(),
                                ModuleName = data.GetProperty("moduleName").GetString()
                            };
                            OnModuleLoaded(moduleLoadedArgs);
                            break;
                        case "LTC:gameLoaded":
                            GameLoadedArgs gameLoadedArgs = new GameLoadedArgs
                            {
                                Pid = data.GetProperty("pid").GetInt32(),
                                Width = data.GetProperty("size").GetProperty("width").GetInt32(),
                                Height = data.GetProperty("size").GetProperty("height").GetInt32(),
                            };
                            OnGameLoaded(gameLoadedArgs);
                            break;
                        case "LTC:videoCaptureReady":
                            VideoCaptureReadyArgs videoCaptureReadyArgs = new VideoCaptureReadyArgs
                            {
                                Pid = data.GetProperty("pid").GetInt32()
                            };
                            OnVideoCaptureReady(videoCaptureReadyArgs);
                            break;
                        case "LTC:recordingError":
                            int errorCode = data.GetProperty("code").GetInt32();
                            PrettyConsole.Writeline(ConsoleColor.Red, "ERROR: ", string.Format("Recording Error code: {0} ", errorCode));
                            switch (errorCode)
                            {
                                case 11:
                                    Console.WriteLine("- Issue with video directory");
                                    break;
                                case 12:
                                    Console.WriteLine("- Issue with temp directory");
                                    break;
                                case 16:
                                    Console.WriteLine("- Issue with disk space");
                                    break;
                                default:
                                    Console.WriteLine();
                                    break;
                            }
                            break;
                        case "LTC:gameScreenSizeChanged":
                            PrettyConsole.Writeline(ConsoleColor.Magenta, "INFO: ", string.Format("Game screen size changed, {0}x{1}", data.GetProperty("width").GetInt32(), data.GetProperty("height").GetInt32()));
                            break;
                        case "LTC:saveStarted":
                            PrettyConsole.Writeline(ConsoleColor.Magenta, "INFO: ", string.Format("Started saving recording to file, {0}", data.GetProperty("filename").GetString()));
                            break;
                        case "LTC:saveFinished":
                            PrettyConsole.Writeline(ConsoleColor.Magenta, "INFO: ", string.Format("Finished saving recording to file, {0}, {1}x{2}, {3}, {4}",
                                                data.GetProperty("fileName"),
                                                data.GetProperty("width"),
                                                data.GetProperty("height"),
                                                data.GetProperty("duration"),
                                                data.GetProperty("recMode")));
                            break;
                        default:
                            PrettyConsole.Writeline(ConsoleColor.Yellow, "WARNING: ", string.Format("WAS SENT AN EVENT THAT DOES NOT MATCH CASE: {0}", msg));
                            break;
                    }
                }

                client.Close();
                ltcProcess.Close();
                server.Stop();
            }
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

            if(ns != null && server != null) {
                byte[] jsonBytes = Encoding.Default.GetBytes(json);
                ns.Write(jsonBytes, 0, jsonBytes.Length);     //sending the message
                PrettyConsole.Writeline(ConsoleColor.Green, "SENT: ", json);
            }
        }

        public JsonElement GetDataType(string jsonString) {
            JsonElement jsonElement = JsonDocument.Parse(jsonString).RootElement;

            return jsonElement;
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

    public class PrettyConsole
    {
        public static void Writeline(ConsoleColor titleColor, string title, string message)
        {
            Console.ForegroundColor = titleColor;
            Console.Write(title);
            Console.ResetColor();
            Console.WriteLine(message);
        }
    }
}
