using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace ReplaysOrigin.Services {
    class DetectionService {
        JArray gameDetectionsJson;
        JArray nonGameDetectionsJson;

        public void DownloadGameDetections() {
            var result = string.Empty;
            using (var webClient = new System.Net.WebClient()) {
                result = webClient.DownloadString("https://raw.githubusercontent.com/lulzsun/RePlaysTV/master/detections/game_detections.json");
            }
            gameDetectionsJson = JsonConvert.DeserializeObject<JArray>(result);
        }

        public void DownloadNonGameDetections() {
            var result = string.Empty;
            using (var webClient = new System.Net.WebClient()) {
                result = webClient.DownloadString("https://raw.githubusercontent.com/lulzsun/RePlaysTV/master/detections/nongame_detections.json");
            }
            nonGameDetectionsJson = JsonConvert.DeserializeObject<JArray>(result);
        }

        public bool IsMatchedGame(string exeFile) {
            for (int x = 0; x < gameDetectionsJson.Count; x++) {
                JArray gameDetections = JArray.FromObject(gameDetectionsJson[x]["mapped"]["game_detection"]);

                for (int y = 0; y < gameDetections.Count; y++) {
                    var detection = gameDetections[y]["gameexe"];

                    if (detection != null) {
                        string[] jsonExeStr = detection.ToString().ToLower().Split('|');

                        for (int z = 0; z < jsonExeStr.Length; z++) {
                            if (exeFile.ToLower().Contains(jsonExeStr[z]) && jsonExeStr[z].Length > 0) {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public string GetGameTitle(string exeFile) {
            for (int x = 0; x < gameDetectionsJson.Count; x++) {
                JArray gameDetections = JArray.FromObject(gameDetectionsJson[x]["mapped"]["game_detection"]);

                for (int y = 0; y < gameDetections.Count; y++) {
                    var detection = gameDetections[y]["gameexe"];

                    if (detection != null) {
                        string[] jsonExeStr = detection.ToString().ToLower().Split('|');

                        for (int z = 0; z < jsonExeStr.Length; z++) {
                            if (exeFile.ToLower().Contains(jsonExeStr[z]) && jsonExeStr[z].Length > 0) {
                                return gameDetectionsJson[x]["title"].ToString();
                            }
                        }
                    }
                }
            }
            return "Game Unknown";
        }

        public bool IsMatchedNonGame(string exeFile) {
            for (int x = 0; x < nonGameDetectionsJson.Count; x++) {
                JArray gameDetections = JArray.FromObject(nonGameDetectionsJson[x]["detections"]);

                for (int y = 0; y < gameDetections.Count; y++) {
                    var detection = gameDetections[y]["detect_exe"];

                    if (detection != null) {
                        string[] jsonExeStr = detection.ToString().ToLower().Split('|');

                        for (int z = 0; z < jsonExeStr.Length; z++) {
                            if (exeFile.ToLower().Contains(jsonExeStr[z]) && jsonExeStr[z].Length > 0)
                                return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
