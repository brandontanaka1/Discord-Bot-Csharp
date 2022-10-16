using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;

namespace Discord_Bot_Csharp.src.Data_Access
{
    public class RunType
    { 
        public ObjectId Id { get; set; }

        public string Name { get; set; }

        public string Value { get; set; }

        public ulong Channel { get; set; }

        public List<RunTypeChannel> Channels { get; set; }
    }

    public class RunTypeChannel
    {
        public ulong Guild { get; set; }

        public ulong Channel { get; set; }
    }
}
