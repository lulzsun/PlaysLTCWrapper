using System;
using PlaysLTCWrapper.Example.Services;

namespace PlaysLTCWrapper.Example {
    class Program {
        static void Main(string[] args) {
            RecordingService recordingService = new RecordingService();
            DetectionService detectionService = new DetectionService();
            detectionService.DownloadGameDetections();
            detectionService.DownloadNonGameDetections();

            LTCProcess ltc = new LTCProcess();

            ltc.Log += (sender, msg) => {
                ConsoleColor consoleColor = ConsoleColor.White;
                switch (msg.Title) {
                    case "LTCPROCESS":
                        consoleColor = ConsoleColor.DarkCyan;
                        break;
                    case "RECEIVED":
                        consoleColor = ConsoleColor.Cyan;
                        break;
                    case "SENT":
                        consoleColor = ConsoleColor.Green;
                        break;
                    case "INFO":
                        consoleColor = ConsoleColor.Magenta;
                        break;
                    case "WARNING":
                        consoleColor = ConsoleColor.Yellow;
                        break;
                    case "ERROR":
                        consoleColor = ConsoleColor.Red;
                        break;
                    default:
                        break;
                }
                Logger.WriteLine(consoleColor, msg.Title + ": ", msg.Message);
            };

            ltc.ConnectionHandshake += (sender, msg) => {
                ltc.SetCaptureMode(49152); //ORB_GAMEDVR_SET_CAPTURE_MODE ?????
                ltc.SetGameDVRCaptureEngine(1); //1 = nvidia ?????
            };

            ltc.ProcessCreated += (sender, msg) => {
                if (!recordingService.IsRecording) { // If we aren't already recording something, lets look for a process to record
                    bool isGame = detectionService.IsMatchedGame(msg.ExeFile);
                    bool isNonGame = detectionService.IsMatchedNonGame(msg.ExeFile);

                    if (isGame && !isNonGame) {
                        Logger.WriteLine(ConsoleColor.Magenta, "INFO: ", "This is a recordable game, preparing to LoadGameModule");

                        string gameTitle = detectionService.GetGameTitle(msg.ExeFile);
                        recordingService.SetCurrentSession(msg.Pid, gameTitle);
                        ltc.SetGameName(gameTitle);
                        ltc.LoadGameModule(msg.Pid);
                    } else if (!isGame && !isNonGame) {
                        Logger.WriteLine(ConsoleColor.Magenta, "INFO: ", "This is an unknown application, lets try to ScanForGraphLib");

                        recordingService.SetCurrentSession(msg.Pid, "Game Unknown");
                        ltc.ScanForGraphLib(msg.Pid); // the response will be sent to GraphicsLibLoaded if successful
                    } else {
                        Logger.WriteLine(ConsoleColor.Magenta, "INFO: ", "This is a non-game");
                    }
                } else {
                    Logger.WriteLine(ConsoleColor.Magenta, "INFO: ", "Current recording a game right now, ignoring detection checks.");
                }
            };

            ltc.GraphicsLibLoaded += (sender, msg) => {
                ltc.SetGameName("Game Unknown");
                ltc.LoadGameModule(msg.Pid);
            };

            ltc.GameBehaviorDetected += (sender, msg) => {
                ltc.StartAutoHookedGame(msg.Pid);
            };

            ltc.VideoCaptureReady += (sender, msg) => {
                //if (AutomaticRecording == true)
                if (!recordingService.IsRecording) {
                    ltc.SetKeyBinds();
                    ltc.StartRecording();
                    recordingService.StartRecording();
                }
            };

            ltc.ProcessTerminated += (sender, msg) => {
                if(recordingService.IsRecording) {
                    if (recordingService.GetCurrentSession().Pid == msg.Pid) {
                        ltc.StopRecording();
                        recordingService.StopRecording();
                    }
                }
            };

            ltc.Connect();

            Console.ReadKey();
        }
    }
}
