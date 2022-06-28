using System;
using System.Collections.Generic;
using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Discord_Bot_Csharp.src.Data_Access
{
    public class Diablo2Runner
    {
        public ObjectId Id { get; set; }

        public string Name { get; set; }

        public ObjectId? CurrentGame { get; set; }     
    }
}
