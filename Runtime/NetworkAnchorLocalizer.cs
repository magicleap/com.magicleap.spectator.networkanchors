using System.Collections;
using UnityEngine;
using UnityEngine.Events;
#if PLATFORM_LUMIN
using UnityEngine.XR.MagicLeap;
using System.Linq;
using System.Collections.Generic;
#endif

public class NetworkAnchorLocalizer : MonoBehaviour
{
    public string PlayerId;

    public NetworkAnchorService NetworkAnchorService;

    private bool _didReceiveNetworkAnchor;
    private bool _isLocalized;
    private bool _isBusy;

    public UnityEvent OnAnchorPlaced;

    private bool IsStandalone
    {
        get
        {
#if UNITY_STANDALONE
            return true;
#else
            return false;
#endif
        }
    }


    // Start is called before the first frame update
    void Start()
    {
#if PLATFORM_LUMIN
        //system start-ups:
        if (!MLPersistentCoordinateFrames.IsStarted)
        {
            MLResult result = MLPersistentCoordinateFrames.Start();
            if (!result.IsOk)
            {
                Debug.LogErrorFormat("Error: PCFExample failed starting MLPersistentCoordinateFrames, disabling script. Reason: {0}", result);
            }

            MLPersistentCoordinateFrames.OnLocalized += HandleOnLocalized;
        }
#endif

        //Listen to the network to see if an Anchor was created while we were waiting.
        NetworkAnchorService.OnNetworkAnchorCreated += NetworkAnchorCreated;
    }

    public void SetPlayerId(string playerId)
    {
        //Get the Player's ID that will be referenced in the network events
        PlayerId = playerId;
    }

    public void LocateExistingAnchor()
    {
        if (_isBusy)
        {
            Debug.LogWarning("Another process is loading.");
            return;
        }
        StartCoroutine(DoJoinRoom());
    }


    //Logic for actions that need to be done after joining a network room.
    IEnumerator DoJoinRoom()
    {
        _isBusy = true;

        //Wait a frame in-case the other players want to tell us something before we find the anchor
        yield return new WaitForEndOfFrame();

        //Create a dummy anchor and send it to see if a network anchor exists.
        var dummyAnchor = new NetworkAnchor() { OwnerId = PlayerId };
        var createAnchorRequest = NetworkAnchorService.SendCreateNetworkAnchorRequest(dummyAnchor);
        while (!createAnchorRequest.IsCompleted)
        {
            yield return null;
        }
        //If a network anchor exists then align the player to the anchor.
        if (createAnchorRequest.Result.ResponseCode == NetworkAnchorService.ResultCode.EXISTS)
        {
            //If we are on standalone we use the uploaded network anchors world position
            if (IsStandalone)
            {
                MoveToNetworkAnchor(createAnchorRequest.Result.NetworkAnchor, createAnchorRequest.Result.NetworkAnchor.LinkedCoordinate);
            }
            else
            {
                //If we are a magic leap then request a network anchor relative to our Pcfs
                GetSharedAnchor();
            }
        }

        _isBusy = false;
    }

    private void NetworkAnchorCreated(NetworkAnchorService.CreateAnchorResponse obj)
    {
        if(obj.ResponseCode != NetworkAnchorService.ResultCode.SUCCESS)
            return;

        if (!IsStandalone && obj.NetworkAnchor.OwnerId != PlayerId)
            GetSharedAnchor(true);

        if (IsStandalone)
            MoveToNetworkAnchor(obj.NetworkAnchor, obj.NetworkAnchor.LinkedCoordinate);
    }

    private void HandleOnLocalized(bool localized)
    {
        _isLocalized = localized;
    }

    void OnDestroy()
    {
#if PLATFORM_LUMIN
        MLPersistentCoordinateFrames.OnLocalized -= HandleOnLocalized;
        MLPersistentCoordinateFrames.Stop();
#endif
    }

    public void CreateOrGetAnchor()
    {
        if (_isBusy)
        {
            Debug.LogWarning("Another process is loading.");
            return;
        }
        StopAllCoroutines();
        StartCoroutine(DoCreateOrGetAnchor());
    }

    public void GetSharedAnchor(bool force = false)
    {
        if (_isBusy && force == false)
        {
            Debug.LogWarning("Another process is loading.");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(DoGetNetworkAnchor());
    }

    public void CreateNetworkAnchor(bool force = false)
    {

        if (_isBusy && force == false)
        {
            Debug.LogWarning("Another process is loading.");
            return;
        }
        if (_didReceiveNetworkAnchor  && force == false)
        {
            Debug.LogError("Network anchor already created.");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(DoCreateAnchor());
    }

    private IEnumerator DoCreateOrGetAnchor()
    {
        if (_didReceiveNetworkAnchor == false)
        {
            var dummyAnchor = new NetworkAnchor() { OwnerId = PlayerId };
            //Try to upload a dummy anchor and see if the server already has one
            var createAnchorRequest = NetworkAnchorService.SendCreateNetworkAnchorRequest(dummyAnchor);

            while (!createAnchorRequest.IsCompleted)
            {
                yield return null;
            }
            //If the dummy anchor is just missing information than a new anchor can be created.
            if (createAnchorRequest.Result.ResponseCode == NetworkAnchorService.ResultCode.MISSING_INFORMATION)
            {
                //Create a new anchor
                yield return DoCreateAnchor();
                //After we create a new anchor we are done
                yield break;
            }
        }

        yield return DoCreateAnchor();
    }

    private IEnumerator DoCreateAnchor()
    {
        _isBusy = true;
        //Wait a frame
        yield return new WaitForEndOfFrame();

#if PLATFORM_LUMIN
        while (!MLPersistentCoordinateFrames.IsStarted || !MLPersistentCoordinateFrames.IsLocalized)
        {
            yield return null;
        }
       
        MLResult result = MLPersistentCoordinateFrames.FindAllPCFs(out List<MLPersistentCoordinateFrames.PCF> allPcFs, typesMask: MLPersistentCoordinateFrames.PCF.Types.MultiUserMultiSession);
        if (result != MLResult.Code.Ok)
        {
            Debug.LogError("Could not find PCFS: " + result);
            yield break;
        }
        Debug.LogError("Looking for PCFs" + result);

        List<GenericCoordinateReference> genericPcfReferences = allPcFs.OrderByDescending(x => x.Confidence)
            .Select (x => new GenericCoordinateReference
            { CoordinateId = x.CFUID.ToString(), Position = x.Position, Rotation = x.Rotation }).ToList();
        Debug.Log(genericPcfReferences.Count);

        var uploadCoordinatesRequest = NetworkAnchorService.SendUploadCoordinatesRequest(PlayerId, genericPcfReferences);

        while (!uploadCoordinatesRequest.IsCompleted)
        {
            yield return null;
        }

        NetworkAnchor anchor;

        if (uploadCoordinatesRequest.Result.ResponseCode == NetworkAnchorService.ResultCode.SUCCESS)
        {
            anchor = new NetworkAnchor("origin", genericPcfReferences[0], transform.position,
                transform.rotation){OwnerId = PlayerId};
            Debug.Log("Creating new anchor " + anchor.LinkedCoordinate.CoordinateId);

        }
        else
        {
            if (uploadCoordinatesRequest.Result.ResponseCode == NetworkAnchorService.ResultCode.EXISTS)
                _didReceiveNetworkAnchor = true;

            Debug.LogError("Could not create network anchor because :  " + uploadCoordinatesRequest.Result.ResponseCode);
            yield break;
        }

        var createAnchorRequest = NetworkAnchorService.SendCreateNetworkAnchorRequest(anchor);
        
        while (!createAnchorRequest.IsCompleted)
        {
            yield return null;
        }

        Debug.Log(createAnchorRequest.Result.ResponseCode == NetworkAnchorService.ResultCode.SUCCESS);
        if (createAnchorRequest.Result.ResponseCode == NetworkAnchorService.ResultCode.SUCCESS)
        {
            var newAchnor = createAnchorRequest.Result.NetworkAnchor;
            Debug.Log("GOT CREATION EVENT " + JsonUtility.ToJson(createAnchorRequest.Result.NetworkAnchor));
            MoveToNetworkAnchor(newAchnor, genericPcfReferences[0]);
        }
        else
        {
            Debug.LogError("Could not create anchor because : " + createAnchorRequest.Result.NetworkAnchor);

        }

        _isBusy = false;

#endif

    }

    private IEnumerator DoGetNetworkAnchor()
    {
        _isBusy = true;
        //Wait a frame
        yield return new WaitForEndOfFrame();
#if PLATFORM_LUMIN
        while (!MLPersistentCoordinateFrames.IsStarted || !MLPersistentCoordinateFrames.IsLocalized)
        {
            yield return null;
        }

        MLResult result = MLPersistentCoordinateFrames.FindAllPCFs(out List<MLPersistentCoordinateFrames.PCF> allPCFs, typesMask: MLPersistentCoordinateFrames.PCF.Types.MultiUserMultiSession);
        if (result != MLResult.Code.Ok)
        {
            Debug.LogError("Could not find PCFS: " + result);
            yield break;
        }

        List<GenericCoordinateReference> genericPcfReferences = allPCFs.OrderByDescending(x => x.Confidence)
            .Select(x => new GenericCoordinateReference
                { CoordinateId = x.CFUID.ToString(), Position = x.Position, Rotation = x.Rotation }).ToList();

        var getNetworkAnchorRequest = NetworkAnchorService.SendGetSharedNetworkAnchorRequest(PlayerId, genericPcfReferences);

        while (!getNetworkAnchorRequest.IsCompleted)
        {
            yield return null;
        }


        if (getNetworkAnchorRequest.Result.ResponseCode == NetworkAnchorService.ResultCode.SUCCESS)
        {
            var networkAnchor = getNetworkAnchorRequest.Result.NetworkAnchor;
            var myPcf = genericPcfReferences.FirstOrDefault(pcf => pcf.CoordinateId == networkAnchor.LinkedCoordinate.CoordinateId);

            if (myPcf != null)
            {
                MoveToNetworkAnchor(networkAnchor, myPcf);
            }
            else
            {
                Debug.LogError("Could not find the shared PCF for the NetworkAnchor. IsLocalized = " + _isLocalized);
            }
        }
        else
        {
            Debug.Log($"Could not find network anchor {getNetworkAnchorRequest.Result.ResponseCode}");
        }
#endif
        _isBusy = false;

    }

    private void MoveToNetworkAnchor(NetworkAnchor anchor, GenericCoordinateReference coordinateReference)
    {
        Debug.Log("Moved to anchor " + anchor.AnchorId);
        _didReceiveNetworkAnchor = true;
        OnAnchorPlaced.Invoke();
        this.transform.rotation = anchor.GetWorldRotation(coordinateReference);
        this.transform.position = anchor.GetWorldPosition(coordinateReference);
    }
}
