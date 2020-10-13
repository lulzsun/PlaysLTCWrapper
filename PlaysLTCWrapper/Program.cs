using System;
using ReplaysOrigin.Services;

namespace PlaysLTCWrapper.TestProgram {
    class Program {
        static void Main(string[] args) {
            RecordingService recordingService = new RecordingService();
            DetectionService detectionService = new DetectionService();
            detectionService.DownloadGameDetections();
            detectionService.DownloadNonGameDetections();

            LTCProcess ltc = new LTCProcess();

            ltc.ConnectionHandshake += (sender, msg) => {
                Console.WriteLine("Connection Handshake: {0}, {1}", msg.Version, msg.IntegrityCheck);
                ltc.SetCaptureMode(49152); //ORB_GAMEDVR_SET_CAPTURE_MODE ?????
                ltc.SetGameDVRCaptureEngine(1); //1 = nvidia ?????
            };

            ltc.ProcessCreated += (sender, msg) => {
                Console.WriteLine("Process Created: {0}, {1}", msg.Pid, msg.ExeFile, msg.CmdLine);

                if (!recordingService.IsRecording) { // If we aren't already recording something, lets look for a process to record
                    bool isGame = detectionService.IsMatchedGame(msg.ExeFile);
                    bool isNonGame = detectionService.IsMatchedNonGame(msg.ExeFile);

                    if (isGame && !isNonGame) {
                        Console.WriteLine("This is a recordable game, preparing to LoadGameModule");

                        string gameTitle = detectionService.GetGameTitle(msg.ExeFile);
                        recordingService.SetCurrentSession(msg.Pid, gameTitle);
                        ltc.SetGameName(gameTitle);
                        ltc.LoadGameModule(msg.Pid);
                    } else if (!isGame && !isNonGame) {
                        Console.WriteLine("This is an unknown application, lets try to ScanForGraphLib");

                        recordingService.SetCurrentSession(msg.Pid, "Game Unknown");
                        ltc.ScanForGraphLib(msg.Pid); // the response will be sent to GraphicsLibLoaded if successful
                    } else {
                        Console.WriteLine("This is a non-game");
                    }
                } else {
                    Console.WriteLine("Current recording a game right now, ignoring detection checks.");
                }
            };

            ltc.GraphicsLibLoaded += (sender, msg) => {
                Console.WriteLine("Graphics Lib Loaded: {0}, {1}", msg.Pid, msg.ModuleName);
                ltc.SetGameName("Game Unknown");
                ltc.LoadGameModule(msg.Pid);
            };

            ltc.ModuleLoaded += (sender, msg) => {
                Console.WriteLine("Plays-ltc Recording Module Loaded: {0}, {1}", msg.Pid, msg.ModuleName);
            };

            ltc.GameLoaded += (sender, msg) => {
                Console.WriteLine("Game finished loading: {0}, {1}x{2}", msg.Pid, msg.Width, msg.Height);
            };

            ltc.VideoCaptureReady += (sender, msg) => {
                Console.WriteLine("Video capture ready, can start recording: {0}", msg.Pid);

                //if (AutomaticRecording == true)
                if (!recordingService.IsRecording) {
                    ltc.SetKeyBinds();
                    ltc.StartRecording();
                    recordingService.StartRecording();
                }
            };

            ltc.ProcessTerminated += (sender, msg) => {
                Console.WriteLine("Process Terminated: {0}", msg.Pid);

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
