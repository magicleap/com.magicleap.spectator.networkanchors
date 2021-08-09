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
    public NetworkAnchorLocalizer NetworkAnchorLocalizer;

    public async Task<List<GenericCoordinateReference>> RequestCoordinateReferences(bool refresh)
    {
        //On the desktop client, we use the hosts coordinates

        //Request to download them
        var downloadHostCoordinatesRequest =
            NetworkAnchorService.SendDownloadHostCoordinatesRequest(NetworkAnchorLocalizer.PlayerId);
        while (downloadHostCoordinatesRequest.IsCompleted)
        {
            await Task.Delay(100);
        }
        //Return them as our own
        return downloadHostCoordinatesRequest.Result.PlayerPcfReference.CoordinateReferences;
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
