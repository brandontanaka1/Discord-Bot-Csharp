using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;

namespace Discord_Bot_Csharp.src.Data_Access
{
    public class Diablo2RunnerStats
    {
        public ObjectId Id { get; set; }

        public string Platform { get; set; }

        public string GameType { get; set; }

        public string RunType { get; set; }

        public string RunnerName { get; set; }

        public int RunCount { get; set; }
    }
}
