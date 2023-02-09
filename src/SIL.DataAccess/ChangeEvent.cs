﻿namespace SIL.DataAccess;

[MongoDB.Bson.Serialization.Attributes.BsonIgnoreExtraElements]
public class ChangeEvent
{
    public string EntityRef { get; set; } = default!;
    public EntityChangeType ChangeType { get; set; }
    public int Revision { get; set; }
}
