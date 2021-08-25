using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System.Linq;

[DisallowMultipleComponent]
public class NetworkAnchorLocalizer : MonoBehaviour
{
    public NetworkAnchorService NetworkAnchorService;

    private bool _isBusy;
    private bool _isInitialized;

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
        NetworkAnchorService.OnConnectionChanged += OnNetworkServiceConnected;
    }

    // Check for an existing anchors and listen to anchor anchor changes when the network anchor service connects.
    private void OnNetworkServiceConnected(bool isConnected)
    {
        if (isConnected)
        {
            LocateExistingAnchor();
            NetworkAnchorService.OnNetworkAnchorChanged += MoveToNetworkAnchor;
            _isInitialized = true;

        }
        else if(_isInitialized)
        {
            NetworkAnchorService.OnNetworkAnchorChanged -= MoveToNetworkAnchor;
            _isInitialized = false;
        }
    }

    public void LocateExistingAnchor()
    {
        if (_isBusy)
        {
            Debug.LogWarning("Another process is loading.");
            return;
        }
        StartCoroutine(DoGetExistingAnchor());
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
    
    /// <summary>
    /// Locate and move to an existing anchor or create a new one using the NetworkAnchorService.
    /// </summary>
    /// <param name="force">If true, will stop all coroutines to perform this call. Otherwise, will not proceed
    /// if a request is already loading.</param>
    public void GetSharedAnchor(bool force = false)
    {
        if (_isBusy && force == false)
        {
            Debug.LogWarning("Another process is loading.");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(DoGetExistingAnchor());
    }

    /// <summary>
    /// Create a new network anchor using the NetworkAnchorService.
    /// </summary>
    /// <param name="force">If true, will stop all coroutines to perform this call. Otherwise, will not proceed
    /// if a request is already loading.</param>
    public void CreateNetworkAnchor(bool force = false)
    {

        if (_isBusy && force == false)
        {
            Debug.LogWarning("Another process is loading.");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(DoCreateAnchor());
    }

    private IEnumerator DoCreateOrGetAnchor()
    {
        //Request an anchor
        var getAnchorRequest = NetworkAnchorService.RequestNetworkAnchor();
        while (!getAnchorRequest.IsCompleted)
        {
            yield return null;
        }

        //If a network anchor exists then align move this object to the anchor position
        if (getAnchorRequest.Result.ResultCode == NetworkAnchorService.ResultCode.SUCCESS)
        {
            MoveToNetworkAnchor(getAnchorRequest.Result.NetworkAnchor);
        }
        else
        {
            //If the request failed, assume that it's because no anchor existed. Create an anchor
            yield return DoCreateAnchor();
        }

    }

    private IEnumerator DoGetExistingAnchor()
    {
        _isBusy = true;

        //Wait a frame in-case the other players want to tell us something before we find the anchor
        yield return new WaitForEndOfFrame();

        var getAnchorRequest = NetworkAnchorService.RequestNetworkAnchor();
        while (!getAnchorRequest.IsCompleted)
        {
            yield return null;
        }
        //If a network anchor exists then align the player to the anchor.
        if (getAnchorRequest.Result.ResultCode == NetworkAnchorService.ResultCode.SUCCESS)
        {
            //If we are a magic leap then request a network anchor relative to our Pcfs
            MoveToNetworkAnchor(getAnchorRequest.Result.NetworkAnchor);
        }

        _isBusy = false;
    }

    private IEnumerator DoCreateAnchor()
    {
        _isBusy = true;
        //Wait a frame in-case the other players want to tell us something before we create the anchor
        yield return new WaitForEndOfFrame();

        var createAnchorRequest = NetworkAnchorService.RequestCreateNetworkAnchor("origin", transform.position, transform.rotation);
        while (!createAnchorRequest.IsCompleted)
        {
            yield return null;
        }

        if (createAnchorRequest.Result.ResultCode == NetworkAnchorService.ResultCode.SUCCESS)
        {
            MoveToNetworkAnchor(createAnchorRequest.Result.NetworkAnchor);
        }
        else
        {
            string info = createAnchorRequest.Result != null ? createAnchorRequest.Result.ResultCode.ToString() : "UNKNOWN";
            Debug.LogError("Could not create anchor " + info);
        }

        _isBusy = false;
    }

    /// <summary>
    /// Moves this object to the the position of the network anchor
    /// </summary>
    /// <param name="anchor"></param>
    private void MoveToNetworkAnchor(NetworkAnchor anchor)
    {
        Debug.Log("Moved to anchor to anchor id" + anchor.AnchorId);
        this.transform.rotation = anchor.GetWorldRotation();
        this.transform.position = anchor.GetWorldPosition();
        OnAnchorPlaced.Invoke();
    }
}
