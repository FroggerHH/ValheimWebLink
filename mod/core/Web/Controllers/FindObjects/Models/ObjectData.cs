﻿namespace ValheimWebLink.Web.Controllers.FindObjects.Models;

[Serializable]
public class ObjectData
{
    public string objectType = RecordPrefabs.ObjectType.Other.ToString();
    public int prefabHash;
    public string prefabName;
    public SimpleVector3 position;

    public virtual async Task<ObjectData> Init(ZDO zdo)
    {
        prefabHash = zdo.GetPrefab();
        prefabName = RecordPrefabs.prefabNames.TryGetValue(zdo.GetPrefab(), out var name) ? name : "Unknown";
        position = zdo.GetPosition();

        return this;
    }
}