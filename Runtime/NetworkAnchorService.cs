using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkAnchorService : MonoBehaviour
{
    //Server or Host
    private List<int> _connectedPlayers = new List<int>();

    //Player
    private int _localPlayerId;

    public NetworkAnchor LocalNetworkAnchor
    {
        get
        {
            return _localNetworkAnchor;
        }
        private set
        {
            if(_localNetworkAnchor !=value)
                OnNetworkAnchorChanged?.Invoke(value);
            _localNetworkAnchor = value;
        }
    }
    private NetworkAnchor _localNetworkAnchor;

    private List<GenericCoordinateReference> _genericCoordinateReferences;
    
    //The Coordinate Service that is being used
    private IGenericCoordinateProvider _genericCoordinateProvider;

    public bool IsConnected
    {
        get
        {
            return _isConnected;
        }
        private set
        {
            if(_isConnected !=value)
                OnConnectionChanged?.Invoke(value);
            _isConnected = value;

        }
    }

    private bool _isConnected;

    /// <summary>
    /// Is called when the local network anchor changes
    /// </summary>
    /// <param name="players">The new networ anchor value</param>
    public delegate void ConditionalEvent(bool networkAnchor);
    public ConditionalEvent OnConnectionChanged;

    /// <summary>
    /// Is called when the local network anchor changes
    /// </summary>
    /// <param name="players">The new networ anchor value</param>
    public delegate void NetworkAnchorEvent(NetworkAnchor networkAnchor);
    public NetworkAnchorEvent OnNetworkAnchorChanged;

    /// <summary>
    /// Is called when the service needs to send network events
    /// </summary>
    /// <param name="networkEventCode">The network event code that is referenced to ensure the current parser for the data</param>
    /// <param name="jsonData">The event data json file that will be parsed</param>
    /// <param name="players">The target for the Network event. int[0] = All , int[1]{-1} = MasterClient , int[i>0] = Target Player Id's </param>
    public delegate void BroadcastNetworkEvent(byte networkEventCode, string jsonData, int[] players);
    public BroadcastNetworkEvent OnBroadcastNetworkEvent;

    public enum ResultCode
    {
        UNKNOWN = 0,
        SUCCESS,
        NO_MATCHES_FOUND,
        FAILED = 100,
    }

    public enum SendCode
    {
        MASTER_CLIENT = -1,
        OTHERS = -2,
        ALL =-3
    }

    const int RequestTimeoutMs = 3000;

    [SerializeField] private bool _verboseLogging = true;

    public void ProcessNetworkEvents(byte eventCode, object jsonData)
    {
        if(_verboseLogging)
         Debug.Log("Received Message with code" + eventCode);

        if (eventCode == GetNetworkAnchorRequest.EventCode)
        {
            GetNetworkAnchorRequest result = JsonUtility.FromJson<GetNetworkAnchorRequest>((string)jsonData);
            ProcessNetworkAnchorRequest(result);
        }

        if (eventCode == GetNetworkAnchorResponse.EventCode)
        {
            GetNetworkAnchorResponse result = JsonUtility.FromJson<GetNetworkAnchorResponse>((string)jsonData);
            Debug.Log((string)jsonData);
          _getNetworkAnchorResponseCompletionSource?.TrySetResult(result);
        }


        if (eventCode == CreateNetworkAnchorRequest.EventCode)
        {
            CreateNetworkAnchorRequest result = JsonUtility.FromJson<CreateNetworkAnchorRequest>((string)jsonData);
            ProcessCreateNetworkAnchorRequest(result);
        }

        if (eventCode == CreateNetworkAnchorResponse.EventCode)
        {
            CreateNetworkAnchorResponse result = JsonUtility.FromJson<CreateNetworkAnchorResponse>((string)jsonData);
            _createNetworkAnchorResponseCompletionSource?.TrySetResult(result);
        }

        if (eventCode == ConnectToServiceRequest.EventCode)
        {
            ConnectToServiceRequest result = JsonUtility.FromJson<ConnectToServiceRequest>((string)jsonData);
            ProcessConnectToServiceRequest(result);
        }

        if (eventCode == DisconnectFromServiceRequest.EventCode)
        {
            DisconnectFromServiceRequest result = JsonUtility.FromJson<DisconnectFromServiceRequest>((string)jsonData);
            ProcessDisconnectFromServiceRequest(result);
        }

        if (eventCode == ConnectToServiceResponse.EventCode)
        {
            ConnectToServiceResponse result = JsonUtility.FromJson<ConnectToServiceResponse>((string)jsonData);
            _connectedPlayers = result.ConnectedPlayerIds;
            _connectToServiceResponseCompletionSource?.TrySetResult(result);
        }

        if (eventCode == GetRemoteCoordinatesRequest.EventCode)
        {
            GetRemoteCoordinatesRequest result = JsonUtility.FromJson<GetRemoteCoordinatesRequest>((string)jsonData);
            ProcessRemoteCoordinatesRequest(result);
        }

        if (eventCode == GetRemoteCoordinatesResponse.EventCode)
        {
            GetRemoteCoordinatesResponse result = JsonUtility.FromJson<GetRemoteCoordinatesResponse>((string)jsonData);
            _getRemoteCoordinatesResponseCompletionSource?.TrySetResult(result);
        }

    }

    /// <summary>
    /// Function used to send the network events to the client and server
    /// </summary>
    /// <param name="networkEventCode">The network event code that is referenced to ensure the current parser for the data</param>
    /// <param name="jsonData">The event data json file that will be parsed</param>
    /// <param name="players">The target for the Network event. int[0] = All , int[1]{-1} = MasterClient , int[i>0] = Target Player Id's </param>
    private void SendNetworkEvent(byte networkEventCode, string jsonData, int[] players)
    {
        OnBroadcastNetworkEvent?.Invoke(networkEventCode, jsonData, players);
    }

    #region GetNetworkAnchor
    public class GetNetworkAnchorRequest
    {
        public const byte EventCode = 101;
        public int SenderId;
    }

    public class GetNetworkAnchorResponse
    {
        public const byte EventCode = 102;
        public ResultCode ResultCode;
        public int SenderId;
        public NetworkAnchor NetworkAnchor;
        public List<GenericCoordinateReference> GenericCoordinates = new List<GenericCoordinateReference>();
    }

    public class GetNetworkAnchorResult
    {
        public ResultCode ResultCode;
        public NetworkAnchor NetworkAnchor;
    }

    TaskCompletionSource<GetNetworkAnchorResponse> _getNetworkAnchorResponseCompletionSource;

    public async Task<GetNetworkAnchorResult> RequestNetworkAnchor()
    {
        if (_verboseLogging)
            Debug.Log("Requesting the remote Network Anchor");


        var genericCoordinatesRequest = _genericCoordinateProvider.RequestCoordinateReferences(true);
         // var ct = new CancellationTokenSource(RequestTimeoutMs);
         //   ct.Token.Register(() => _getNetworkAnchorResponseCompletionSource.TrySetCanceled());


         await genericCoordinatesRequest;

        if (_verboseLogging)
            Debug.Log("Local coordinates processed");

        if (!genericCoordinatesRequest.IsCompleted || genericCoordinatesRequest.Result == null)
        {
            Debug.LogError("Generic coordinates could not be found.");

            return (new GetNetworkAnchorResult()
            { ResultCode = ResultCode.FAILED });
        }

        _genericCoordinateReferences = genericCoordinatesRequest.Result;

        for (int i = 0; i < _connectedPlayers.Count; i++)
        {
            if (_connectedPlayers[i] == _localPlayerId)
                continue;

            var clientRequest = new GetNetworkAnchorRequest()
            { SenderId = _localPlayerId };
            _getNetworkAnchorResponseCompletionSource = new TaskCompletionSource<GetNetworkAnchorResponse>();
           
            if (_verboseLogging)
                Debug.Log("Requesting network anchor from player {ID: " + _connectedPlayers[i]+"}");
            
            SendNetworkEvent(GetNetworkAnchorRequest.EventCode, JsonUtility.ToJson(clientRequest), new int[] { _connectedPlayers[i] });

            var content =
                await TaskWithTimeout(_getNetworkAnchorResponseCompletionSource.Task, TimeSpan.FromSeconds(2));

            if (content==null)
            {
                _getNetworkAnchorResponseCompletionSource.TrySetResult(null);
                if(_verboseLogging)
                 Debug.Log("Did not get a response in time, connection failed. Continuing...");
                continue;
            }


            if (_getNetworkAnchorResponseCompletionSource.Task.Result != null
                && _getNetworkAnchorResponseCompletionSource.Task.Result.ResultCode == ResultCode.SUCCESS)
            {
                var remoteCoordinates = _getNetworkAnchorResponseCompletionSource.Task.Result.GenericCoordinates;
                var remoteNetworkAnchor = _getNetworkAnchorResponseCompletionSource.Task.Result.NetworkAnchor;
                
                if(_verboseLogging)
                    Debug.Log("Received network anchor from player {ID : " + _connectedPlayers[i]);

                if (TryGetNetworkAnchor(_genericCoordinateReferences, remoteCoordinates, remoteNetworkAnchor,
                    out NetworkAnchor localNetworkAnchor))
                {
                    if (_verboseLogging)
                        Debug.Log("Remote anchor is valid and has been located locally.");

                    LocalNetworkAnchor = localNetworkAnchor;
                    return (new GetNetworkAnchorResult()
                        { ResultCode = ResultCode.SUCCESS, NetworkAnchor = LocalNetworkAnchor });
                }
                else
                {
                    if (_verboseLogging)
                        Debug.Log("Local player did not share coordinates with the remote player.");
                }
            }
        }

        if (_verboseLogging)
            Debug.Log("Network Anchor could not be found / does not exist.");

        return (new GetNetworkAnchorResult()
        { ResultCode = ResultCode.NO_MATCHES_FOUND, NetworkAnchor = LocalNetworkAnchor });

    }

    private void ProcessNetworkAnchorRequest(GetNetworkAnchorRequest request)
    {
        GetNetworkAnchorResponse response = null;
        if (LocalNetworkAnchor == null || string.IsNullOrEmpty(LocalNetworkAnchor.AnchorId)
            && _genericCoordinateReferences == null || _genericCoordinateReferences.Count == 0)
        {
            response = new GetNetworkAnchorResponse()
            { ResultCode = ResultCode.FAILED,GenericCoordinates = _genericCoordinateReferences,  SenderId = _localPlayerId };
            OnBroadcastNetworkEvent?.Invoke(GetNetworkAnchorResponse.EventCode, JsonUtility.ToJson(response), new[] { request.SenderId });
       
            if (_verboseLogging)
                Debug.Log("A player has requested your coordinates and network anchor, but the you have not localized." );

            return;
        }

        response = new GetNetworkAnchorResponse()
        { ResultCode = ResultCode.SUCCESS, GenericCoordinates = _genericCoordinateReferences, NetworkAnchor = LocalNetworkAnchor, SenderId = _localPlayerId };
        
        if (_verboseLogging)
            Debug.Log("Sending local coordinates and network anchor to remote player.");

        OnBroadcastNetworkEvent?.Invoke(GetNetworkAnchorResponse.EventCode, JsonUtility.ToJson(response), new[] { request.SenderId });

    }

    #endregion

    #region CreateNetworkAnchor

    public class CreateNetworkAnchorRequest
    {
        public const byte EventCode = 103;
        public int SenderId;
        public NetworkAnchor NetworkAnchor;
        public List<GenericCoordinateReference> GenericCoordinates = new List<GenericCoordinateReference>();
    }

    public class CreateNetworkAnchorResponse
    {
        public const byte EventCode = 104;
        public ResultCode ResultCode;
        public int SenderId;
    }

    public class CreateNetworkAnchorResult
    {
        public ResultCode ResultCode;
        public NetworkAnchor NetworkAnchor;
    }

    TaskCompletionSource<CreateNetworkAnchorResponse> _createNetworkAnchorResponseCompletionSource;

    public async Task<CreateNetworkAnchorResult> RequestCreateNetworkAnchor(string id, Vector3 position, Quaternion rotation)
    {

        if (_verboseLogging)
            Debug.Log("Requesting to create a new Network Anchor.");

        _createNetworkAnchorResponseCompletionSource = new TaskCompletionSource<CreateNetworkAnchorResponse>();

        var genericCoordinatesRequest = _genericCoordinateProvider.RequestCoordinateReferences(true);

        await genericCoordinatesRequest;

        if (!genericCoordinatesRequest.IsCompleted || genericCoordinatesRequest.Result == null)
        {
            if (_verboseLogging)
                Debug.LogError("Generic coordinates could not be found.");

            _createNetworkAnchorResponseCompletionSource.SetCanceled();
            return (new CreateNetworkAnchorResult()
            { ResultCode = ResultCode.FAILED });
        }

        _genericCoordinateReferences = genericCoordinatesRequest.Result;

        var newNetworkAnchor = new NetworkAnchor(id, _genericCoordinateReferences[0], position, rotation);

        var createAnchorRequest = new CreateNetworkAnchorRequest()
        { GenericCoordinates = _genericCoordinateReferences, NetworkAnchor = newNetworkAnchor, SenderId = _localPlayerId };

        SendNetworkEvent(CreateNetworkAnchorRequest.EventCode, JsonUtility.ToJson(createAnchorRequest), new int[] { (int)SendCode.OTHERS});
        if (_verboseLogging)
            Debug.Log("Notifying others about the new Network Anchor.");

        var content =
            await TaskWithTimeout(_createNetworkAnchorResponseCompletionSource.Task, TimeSpan.FromSeconds(2));

        if (content == null)
        {
            Debug.LogError("Could not get coordinates");
            _createNetworkAnchorResponseCompletionSource.SetCanceled();
            return (new CreateNetworkAnchorResult()
                { ResultCode = ResultCode.FAILED });
        }

        //TODO: change logic to check if the request has been sent successfully rather than making every client respond.
        var result = new CreateNetworkAnchorResult()
        {
            NetworkAnchor = newNetworkAnchor,
            ResultCode = _createNetworkAnchorResponseCompletionSource.Task.Result.ResultCode
        };

        return result;
    }

    private void ProcessCreateNetworkAnchorRequest(CreateNetworkAnchorRequest request)
    {
        CreateNetworkAnchorResponse response = null;
        if (request.NetworkAnchor == null || string.IsNullOrEmpty(request.NetworkAnchor.AnchorId)
            && request.GenericCoordinates == null || request.GenericCoordinates.Count == 0)
        {
            response = new CreateNetworkAnchorResponse()
            { ResultCode = ResultCode.FAILED, SenderId = _localPlayerId };

            OnBroadcastNetworkEvent?.Invoke(CreateNetworkAnchorResponse.EventCode, JsonUtility.ToJson(response), new[] { request.SenderId });
            return;
        }

        if (TryGetNetworkAnchor(_genericCoordinateReferences, request.GenericCoordinates, request.NetworkAnchor,
            out NetworkAnchor localNetworkAnchor))
        {
            LocalNetworkAnchor = localNetworkAnchor;
            response = new CreateNetworkAnchorResponse()
            { ResultCode = ResultCode.SUCCESS, SenderId = _localPlayerId };
        }
        else
        {
            response = new CreateNetworkAnchorResponse()
            { ResultCode = ResultCode.FAILED, SenderId = _localPlayerId };
        }

        OnBroadcastNetworkEvent?.Invoke(CreateNetworkAnchorResponse.EventCode, JsonUtility.ToJson(response), new[] { request.SenderId });
    }

    #endregion

    #region ConnectToService

    public class ConnectToServiceRequest
    {
        public const byte EventCode = 105;
        public int SenderId;
    }

    public class DisconnectFromServiceRequest
    {
        public const byte EventCode = 106;
        public int SenderId;
    }

    public class ConnectToServiceResponse
    {
        public const byte EventCode = 107;
        public List<int> ConnectedPlayerIds = new List<int>();
        public ResultCode ResultCode;
        public int SenderId;
    }

    public class ConnectToServiceResult
    {
        public ResultCode ResultCode;
    }

    TaskCompletionSource<ConnectToServiceResponse> _connectToServiceResponseCompletionSource;

    public async Task<ConnectToServiceResult> RequestConnectToService(int playerId, IGenericCoordinateProvider coordinateProvider )
    {
        _connectToServiceResponseCompletionSource = new TaskCompletionSource<ConnectToServiceResponse>();
        if (_verboseLogging)
            Debug.Log("Connecting...");

        if (coordinateProvider == null)
        {
            Debug.LogError("Cannot connected! No Coordinate Provider was given!");
            _connectToServiceResponseCompletionSource.SetCanceled();
            return new ConnectToServiceResult() {ResultCode = ResultCode.FAILED};
        }

        _genericCoordinateProvider = coordinateProvider;
        _genericCoordinateProvider.InitializeGenericCoordinates();

        _localPlayerId = playerId;
        var request = new ConnectToServiceRequest() { SenderId = _localPlayerId };

        SendNetworkEvent(ConnectToServiceRequest.EventCode, JsonUtility.ToJson(request), new int[] { (int)SendCode.MASTER_CLIENT });
        var content =
            await TaskWithTimeout(_connectToServiceResponseCompletionSource.Task, TimeSpan.FromSeconds(3));
        if (content == null)
        {
            Debug.LogError("Connection failed, host did not respond!");
            _connectToServiceResponseCompletionSource.SetCanceled();
            return new ConnectToServiceResult() { ResultCode = ResultCode.FAILED };
        }

        if (_connectToServiceResponseCompletionSource.Task.IsCanceled || _connectToServiceResponseCompletionSource.Task.IsFaulted)
        {
            Debug.LogError($"Connection failed, the task was {_connectToServiceResponseCompletionSource.Task.Status}!");
            _connectToServiceResponseCompletionSource.SetCanceled();
            return new ConnectToServiceResult() { ResultCode = ResultCode.FAILED };
        }

        var taskResult = _connectToServiceResponseCompletionSource.Task.Result;
        IsConnected = taskResult.ResultCode == ResultCode.SUCCESS;

        if (_verboseLogging)
            Debug.Log("Connection to Network Anchors Successful");

        var result = new ConnectToServiceResult()
        {
            ResultCode = taskResult.ResultCode
        };
        return result;
    }

    public void DisconnectFromService(int playerId = -1)
    {
        if (IsConnected)
        {
            if (playerId == -1)
                playerId = _localPlayerId;

            var request = new DisconnectFromServiceRequest() { SenderId = playerId };
            SendNetworkEvent(DisconnectFromServiceRequest.EventCode, JsonUtility.ToJson(request), new int[] { (int)SendCode.MASTER_CLIENT});
            _genericCoordinateProvider.DisableGenericCoordinates();
        }
    }

    private void ProcessConnectToServiceRequest(ConnectToServiceRequest request)
    {
        if (!_connectedPlayers.Contains(request.SenderId))
            _connectedPlayers.Add(request.SenderId);

        var response = new ConnectToServiceResponse()
        { ResultCode = ResultCode.SUCCESS, ConnectedPlayerIds = _connectedPlayers, SenderId = _localPlayerId };

        OnBroadcastNetworkEvent?.Invoke(ConnectToServiceResponse.EventCode, JsonUtility.ToJson(response), new[] {(int)SendCode.ALL });
    }

    private void ProcessDisconnectFromServiceRequest(DisconnectFromServiceRequest request)
    {
        if (_connectedPlayers.Contains(request.SenderId))
            _connectedPlayers.Remove(request.SenderId);

        var response = new ConnectToServiceResponse()
        { ResultCode = ResultCode.SUCCESS, ConnectedPlayerIds = _connectedPlayers, SenderId = _localPlayerId };

        OnBroadcastNetworkEvent?.Invoke(ConnectToServiceResponse.EventCode, JsonUtility.ToJson(response), new[] { (int)SendCode.ALL });
    }

    #endregion

    #region GetRemoteCoordinate
    public class GetRemoteCoordinatesRequest
    {
        public const byte EventCode = 108;
        public int SenderId;
    }

    public class GetRemoteCoordinatesResponse
    {
        public const byte EventCode = 109;
        public ResultCode ResultCode;
        public int SenderId;
        public List<GenericCoordinateReference> GenericCoordinates = new List<GenericCoordinateReference>();
    }

    public class GetRemoteCoordinatesResult
    {
        public ResultCode ResultCode;
        public List<GenericCoordinateReference> GenericCoordinates = new List<GenericCoordinateReference>();
    }

    TaskCompletionSource<GetRemoteCoordinatesResponse> _getRemoteCoordinatesResponseCompletionSource;

    public async Task<GetRemoteCoordinatesResult> RequestRemoteCoordinates()
    {

        for (int i = 0; i < _connectedPlayers.Count; i++)
        {
            if (_connectedPlayers[i] == _localPlayerId)
                continue;


            _getRemoteCoordinatesResponseCompletionSource = new TaskCompletionSource<GetRemoteCoordinatesResponse>();
            
            var clientRequest = new GetRemoteCoordinatesRequest()
            { SenderId = _localPlayerId };

            SendNetworkEvent(GetRemoteCoordinatesRequest.EventCode, JsonUtility.ToJson(clientRequest), new int[] { _connectedPlayers[i] });

            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {
                var completedTask = await Task.WhenAny(_getRemoteCoordinatesResponseCompletionSource.Task, Task.Delay(RequestTimeoutMs, timeoutCancellationTokenSource.Token));
                if (completedTask != _getRemoteCoordinatesResponseCompletionSource.Task)
                {
                    continue;
                }

                timeoutCancellationTokenSource.Cancel();
                await _getRemoteCoordinatesResponseCompletionSource.Task;
            }


            if (_getRemoteCoordinatesResponseCompletionSource.Task.Result != null
                && _getRemoteCoordinatesResponseCompletionSource.Task.Result.ResultCode == ResultCode.SUCCESS)
            {
                _genericCoordinateReferences = _getRemoteCoordinatesResponseCompletionSource.Task.Result.GenericCoordinates;
                return (new GetRemoteCoordinatesResult()
                    { ResultCode = ResultCode.SUCCESS, GenericCoordinates = _genericCoordinateReferences });
            }
        }

        return (new GetRemoteCoordinatesResult()
        { ResultCode = ResultCode.FAILED});

    }

    private void ProcessRemoteCoordinatesRequest(GetRemoteCoordinatesRequest request)
    {
        GetRemoteCoordinatesResponse response = null;
        if ( _genericCoordinateReferences == null || _genericCoordinateReferences.Count == 0)
        {
            response = new GetRemoteCoordinatesResponse()
            { ResultCode = ResultCode.FAILED, GenericCoordinates = _genericCoordinateReferences, SenderId = _localPlayerId };

            OnBroadcastNetworkEvent?.Invoke(GetRemoteCoordinatesResponse.EventCode, JsonUtility.ToJson(response), new[] { request.SenderId });
            return;
        }

        response = new GetRemoteCoordinatesResponse()
        { ResultCode = ResultCode.SUCCESS, GenericCoordinates = _genericCoordinateReferences, SenderId = _localPlayerId };

        OnBroadcastNetworkEvent?.Invoke(GetRemoteCoordinatesResponse.EventCode, JsonUtility.ToJson(response), new[] { request.SenderId });

    }

    #endregion

    public static Task<TResult> TaskWithTimeout<TResult>(Task<TResult> task, TimeSpan timeout)
    {
        var timeoutTask = Task.Delay(timeout).ContinueWith(_ => default(TResult), TaskContinuationOptions.ExecuteSynchronously);
        return Task.WhenAny(task, timeoutTask).Unwrap();
    }

    private static bool TryGetNetworkAnchor(List<GenericCoordinateReference> localCoordinateReferences,
        List<GenericCoordinateReference> remoteCoordinates,
        NetworkAnchor remoteNetworkAnchor, out NetworkAnchor localNetworkAnchor)
    {

        localNetworkAnchor = null;
        var sharedCoordinate = localCoordinateReferences.FirstOrDefault(x =>
            remoteCoordinates.Any(j => j.CoordinateId == x.CoordinateId));


        if (sharedCoordinate != null)
        {
            var remoteSharedCoordinate = remoteCoordinates.Find(x => x.CoordinateId == sharedCoordinate.CoordinateId);

            localNetworkAnchor = new NetworkAnchor(remoteNetworkAnchor.AnchorId, sharedCoordinate, remoteSharedCoordinate,
                remoteNetworkAnchor.GetWorldPosition(remoteSharedCoordinate),
                remoteNetworkAnchor.GetWorldRotation(remoteSharedCoordinate));
            return true;
        }

        return false;

    }

}
