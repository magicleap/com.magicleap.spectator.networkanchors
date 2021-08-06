using System.Collections.Generic;

[System.Serializable]
public class PlayerPcfReference
{
    public string PlayerId;
    public List<GenericCoordinateReference> CoordinateReferences = new List<GenericCoordinateReference>();
}
