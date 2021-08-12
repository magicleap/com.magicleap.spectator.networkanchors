using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Used to provide generic coordinates on the desktop client. Currently, this class simply downloads the hosts coordinates
/// and uses them as it's own. We plan to extend this to image targets in the future.
/// </summary>
public class StandaloneCoordinateProvider : MonoBehaviour, IGenericCoordinateProvider
{
    public NetworkAnchorService NetworkAnchorService;
    private const int RequestTimeoutMs = 2000;

    public void OnValidate()
    {
        if (NetworkAnchorService == null)
        {
            NetworkAnchorService = FindObjectOfType<NetworkAnchorService>();
        }
    }

    public async Task<List<GenericCoordinateReference>> RequestCoordinateReferences(bool refresh)
    {

      //  Request to download them
        var downloadHostCoordinatesRequest =
            NetworkAnchorService.RequestRemoteCoordinates();

        await Task.Delay(100);

        if (await Task.WhenAny(downloadHostCoordinatesRequest,
            Task.Delay(RequestTimeoutMs)) != downloadHostCoordinatesRequest)
        {
            Debug.LogError("Could not get coordinates");
            return new List<GenericCoordinateReference>();
        }

        //Return them as our own
        if (downloadHostCoordinatesRequest.IsCompleted && downloadHostCoordinatesRequest.Result != null && downloadHostCoordinatesRequest.Result.GenericCoordinates.Count>0)
        {
            return downloadHostCoordinatesRequest.Result.GenericCoordinates;
        }
        else
        {
            Debug.LogError("Could not download anchors " + downloadHostCoordinatesRequest.Status);
            return null;
        }
    }

    public void InitializeGenericCoordinates()
    {
       //We do not have to start the service on the desktop
    }

    public void DisableGenericCoordinates()
    {
        //We do not have to disable the service on the desktop
    }
}
