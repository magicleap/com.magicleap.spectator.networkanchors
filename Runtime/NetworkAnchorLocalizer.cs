using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System.Linq;

[DisallowMultipleComponent]
public class NetworkAnchorLocalizer : MonoBehaviour
{
    private string _localPlayerId;
    private IGenericCoordinateProvider _genericCoordinateProvider;

    public NetworkAnchorService NetworkAnchorService;

    private bool _didReceiveNetworkAnchor;
    private bool _isLocalized;
    private bool _isBusy;

    public UnityEvent OnAnchorPlaced;

    void OnValidate()
    {
        if (NetworkAnchorService == null)
        {
            NetworkAnchorService = FindObjectOfType<NetworkAnchorService>();
        }
    }
    void Awake()
    {
        NetworkAnchorService.OnServiceStarted += StartLocalizer;
    }
    // Start is called before the first frame update
    private void StartLocalizer(string playerId, IGenericCoordinateProvider coordinateProvider)
    {

        _localPlayerId = playerId;
        _genericCoordinateProvider = coordinateProvider;

        _genericCoordinateProvider.InitializeGenericCoordinates();
        LocateExistingAnchor();

        //Listen to the network to see if an Anchor was created while we were waiting.
        NetworkAnchorService.OnNetworkAnchorCreated += NetworkAnchorCreated;
    }

    public void LocateExistingAnchor()
    {
        if (_isBusy)
        {
            Debug.LogWarning("Another process is loading.");
            return;
        }
        StartCoroutine(DoLocateExistingAnchor());
    }


    //Logic for actions that need to be done after joining a network room.
    IEnumerator DoLocateExistingAnchor()
    {
        _isBusy = true;

        //Wait a frame in-case the other players want to tell us something before we find the anchor
        yield return new WaitForEndOfFrame();

        //Create a dummy anchor and send it to see if a network anchor exists.
        var dummyAnchor = new NetworkAnchor() { OwnerId = _localPlayerId };
        var createAnchorRequest = NetworkAnchorService.SendCreateNetworkAnchorRequest(dummyAnchor);
        while (!createAnchorRequest.IsCompleted)
        {
            yield return null;
        }
        //If a network anchor exists then align the player to the anchor.
        if (createAnchorRequest.Result.ResponseCode == NetworkAnchorService.ResultCode.EXISTS)
        {
            //If we are a magic leap then request a network anchor relative to our Pcfs
            GetSharedAnchor();
        }

        _isBusy = false;
    }

    private void NetworkAnchorCreated(NetworkAnchorService.CreateAnchorResponse obj)
    {
        if(obj.ResponseCode != NetworkAnchorService.ResultCode.SUCCESS)
            return;

        if (obj.NetworkAnchor.OwnerId != _localPlayerId)
            GetSharedAnchor(true);
    }

    private void HandleOnLocalized(bool localized)
    {
        _isLocalized = localized;
    }

    void OnDestroy()
    {
        _genericCoordinateProvider.DisableGenericCoordinates();
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
            var dummyAnchor = new NetworkAnchor() { OwnerId = _localPlayerId };
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
        var genericCoordinateRequest = _genericCoordinateProvider.RequestCoordinateReferences(true);

        while (!genericCoordinateRequest.IsCompleted)
        {
            yield return null;
        }

        if (genericCoordinateRequest.Result == null || genericCoordinateRequest.Result.Count == 0)
        {
            Debug.LogError("Anchor could not be created. Local Player's coordinate request did not contain any values.");
            _isBusy = false;
            yield break;
        }

        var uploadCoordinatesRequest = NetworkAnchorService.SendUploadCoordinatesRequest(_localPlayerId, genericCoordinateRequest.Result);

        while (!uploadCoordinatesRequest.IsCompleted)
        {
            yield return null;
        }

        NetworkAnchor anchor;

        if (uploadCoordinatesRequest.Result.ResponseCode == NetworkAnchorService.ResultCode.SUCCESS)
        {
            anchor = new NetworkAnchor("origin", genericCoordinateRequest.Result[0], transform.position,
                transform.rotation){OwnerId = _localPlayerId};
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

        Debug.Log("CreateAnchorRequest ResponseCode ="+ NetworkAnchorService.ResultCode.SUCCESS);
        if (createAnchorRequest.Result.ResponseCode == NetworkAnchorService.ResultCode.SUCCESS)
        {
            var newAchnor = createAnchorRequest.Result.NetworkAnchor;
            Debug.Log("GOT CREATION EVENT " + JsonUtility.ToJson(createAnchorRequest.Result.NetworkAnchor));
            MoveToNetworkAnchor(newAchnor, genericCoordinateRequest.Result[0]);
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
        var genericCoordinateRequest = _genericCoordinateProvider.RequestCoordinateReferences(true);

        while (!genericCoordinateRequest.IsCompleted)
        {
            yield return null;
        }

        if (genericCoordinateRequest.Result == null || genericCoordinateRequest.Result.Count == 0)
        {
            Debug.LogError("Anchor could not be located. Local Player's coordinate request did not contain any values.");
            _isBusy = false;
            yield break;
        }

        var getNetworkAnchorRequest = NetworkAnchorService.SendGetSharedNetworkAnchorRequest(_localPlayerId, genericCoordinateRequest.Result);

        while (!getNetworkAnchorRequest.IsCompleted)
        {
            yield return null;
        }


        if (getNetworkAnchorRequest.Result.ResponseCode == NetworkAnchorService.ResultCode.SUCCESS)
        {
            var networkAnchor = getNetworkAnchorRequest.Result.NetworkAnchor;
            var myPcf = genericCoordinateRequest.Result.FirstOrDefault(pcf => pcf.CoordinateId == networkAnchor.LinkedCoordinate.CoordinateId);

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
