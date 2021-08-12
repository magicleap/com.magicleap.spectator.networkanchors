using System.Collections.Generic;

[System.Serializable]
public class PlayerPcfReference
{
    public string PlayerId;
    public NetworkAnchor NetworkAnchor;
    public List<GenericCoordinateReference> CoordinateReferences = new List<GenericCoordinateReference>();
    public bool NetworkAnchorIsValid
    {
        get { return NetworkAnchor != null && !string.IsNullOrEmpty(NetworkAnchor.AnchorId); }
    }
}
