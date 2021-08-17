
using UnityEngine;

[System.Serializable]
public class NetworkAnchor
{
    public string OwnerId;
    public string AnchorId;
    public GenericCoordinateReference LinkedCoordinate;
    public Vector3 RelativePosition;
    public Quaternion RelativeRotation;

    public NetworkAnchor()
    {
    }

    public Quaternion GetWorldRotation(GenericCoordinateReference referenceCoordinate)
    {
        return referenceCoordinate.Rotation * RelativeRotation;
    }

    public Vector3 GetWorldPosition(GenericCoordinateReference referenceCoordinate)
    {
        Matrix4x4 pcfCoordinateSpace = new Matrix4x4();
        pcfCoordinateSpace.SetTRS(referenceCoordinate.Position, referenceCoordinate.Rotation, Vector3.one);

        return pcfCoordinateSpace.MultiplyPoint3x4(RelativePosition);
    }

    /// Gets the anchors last cached world rotation. This is unreliable as it assumes the player has not lost tracking since the Pcf was created.
    public Quaternion GetWorldRotation()
    {
        return LinkedCoordinate.Rotation * RelativeRotation;
    }

    /// <summary>
    /// Gets the anchors last cached world position. This is unreliable as it assumes the player has not lost tracking since the Pcf was created.
    /// </summary>
    /// <returns></returns>
    public Vector3 GetWorldPosition()
    {
        Matrix4x4 pcfCoordinateSpace = new Matrix4x4();
        pcfCoordinateSpace.SetTRS(LinkedCoordinate.Position, LinkedCoordinate.Rotation, Vector3.one);

        return pcfCoordinateSpace.MultiplyPoint3x4(RelativePosition);
    }

    public NetworkAnchor(string id, GenericCoordinateReference referenceCoordinate, Vector3 worldPosition, Quaternion worldRotation)
    {
        AnchorId = id;
        LinkedCoordinate = referenceCoordinate;
        SetRelativeRotationPosition(referenceCoordinate, worldRotation, worldPosition);
    }

    /// <summary>
    /// Create a new network anchor based on another player's data by finding the anchor's position relative to a shared anchor.
    /// </summary>
    /// <param name="id">The ID of the anchor.</param>
    /// <param name="localCoordinate">The coordinate that this anchor will be linked to.</param>
    /// <param name="remoteCoordinate">The same coordinate as the localCoordinate, except from the remote player - This is because the world positions of the coordinates are different.</param>
    /// <param name="remoteWorldPosition">The position of the anchor based on the remote player.</param>
    /// <param name="remoteWorldRotation">The rotation of the anchor based on the remote player.</param>
    public NetworkAnchor(string id, GenericCoordinateReference localCoordinate, GenericCoordinateReference remoteCoordinate, Vector3 remoteWorldPosition, Quaternion remoteWorldRotation)
    {
        AnchorId = id;
        LinkedCoordinate = localCoordinate;
        //Set the relative position based on the remote player's coordinate and world positions. This is because the relative positing is the same for all players.
        SetRelativeRotationPosition(remoteCoordinate, remoteWorldRotation, remoteWorldPosition);
    }

    private void SetRelativeRotationPosition(GenericCoordinateReference referenceCoordinate, Quaternion worldRotation, Vector3 worldPosition)
    {
        // Relative Orientation can be computed independent of Position
        // A = PCF.Orientation
        // B = transform.rotation
        // C = OrientationOffset

        Quaternion relOrientation = Quaternion.Inverse(referenceCoordinate.Rotation) * worldRotation;
        RelativeRotation = relOrientation;


        // Relative Position is dependent on Relative Orientation
        // A = pcfCoordinateSpace (Transform of PCF)
        // B = transform.position
        // C = Offset (Position Offset)
        Matrix4x4 pcfCoordinateSpace = Matrix4x4.TRS(referenceCoordinate.Position, referenceCoordinate.Rotation, Vector3.one);
        Vector3 relPosition = Matrix4x4.Inverse(pcfCoordinateSpace).MultiplyPoint3x4(worldPosition);
        RelativePosition = relPosition;
    }

    /// <summary>
    /// Checks if the Network Anchor is valid.
    /// </summary>
    /// <param name="networkAnchor">The target network anchor</param>
    /// <returns>Returns true if the network anchor is not null, has an ID, has a linked coordinate, and the linked coordinate id is not null.</returns>
    public static bool IsValid(NetworkAnchor networkAnchor)
    {
        return networkAnchor !=null && !string.IsNullOrEmpty(networkAnchor.AnchorId) && networkAnchor.LinkedCoordinate != null && 
               !string.IsNullOrEmpty(networkAnchor.LinkedCoordinate.CoordinateId) ;
    }
}
