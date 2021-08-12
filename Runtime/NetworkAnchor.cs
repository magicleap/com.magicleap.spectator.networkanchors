
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

    public NetworkAnchor(string id, GenericCoordinateReference localCoordinate, GenericCoordinateReference remoteCoordinate, Vector3 remoteWorldPosition, Quaternion remoteWorldRotation)
    {
        AnchorId = id;
        LinkedCoordinate = localCoordinate;
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
}
