using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// The entry point for localization requests. When information is requested, the services appends any required data,such as the
/// players coordinates, then invokes the BroadcastNetworkEvent. In order to make this solution flexible, the service does not
/// communicate with the network directly. The network events should be caught, then relayed to your preferred network solution. 
/// </summary>
[DisallowMultipleComponent]
public class NetworkAnchorService : MonoBehaviour
{
    /// <summary>
    /// The instance of the Network Anchor Service. Best when referenced in the Start function.
    /// </summary>
    public static NetworkAnchorService Instance { get; private set; }

    //Server or Host
    /// <summary>
    /// Contains a list of all the connected players
    /// </summary>
    private List<int> _connectedPlayers = new List<int>();

    //Player
    /// <summary>
    /// The local players ID
    /// </summary>
    private int _localPlayerId;

    /// <summary>
    /// The local network anchor. Changing values will trigger the OnNetworkAnchorChanged event.
    /// </summary>
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
    /// <summary>
    /// The private reference to LocalNetworkAnchor, use LocalNetworkAnchor trigger the OnChange event.
    /// </summary>
    private NetworkAnchor _localNetworkAnchor;

    /// <summary>
    /// The local player's coordinates.
    /// </summary>
    private List<GenericCoordinateReference> _genericCoordinateReferences;

    /// <summary>
    /// The Coordinate Service that is being used. Right now only pcfs are supported.
    /// </summary>
    private IGenericCoordinateProvider _genericCoordinateProvider;

    /// <summary>
    /// True if the player has connected to the network anchor service. Changing values will trigger the OnConnectionChanged event.
    /// </summary>
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

    /// The private reference to IsConnected, use IsConnected trigger the OnChange event.
    private bool _isConnected;

    /// <summary>
    /// Delegate for events that return true or false values.
    /// </summary>
    /// <param name="condition">true or false</param>
    public delegate void ConditionalEvent(bool condition);
    /// <summary>
    /// Called when IsConnected becomes true or false.
    /// </summary>
    public ConditionalEvent OnConnectionChanged;

    /// <summary>
    /// Delegate for events that return a NetworkAnchor.
    /// </summary>
    /// <param name="players">Network anchor value</param>
    public delegate void NetworkAnchorEvent(NetworkAnchor networkAnchor);
    /// <summary>
    /// Called when the local player's network anchor is created or updated.
    /// </summary>
    public NetworkAnchorEvent OnNetworkAnchorChanged;

    /// <summary>
    /// Event that sends the data than needs to be sent to remote players.
    /// </summary>
    /// <param name="networkEventCode">The network event code that is referenced to ensure the current parser for the data</param>
    /// <param name="jsonData">The event data json file that will be parsed</param>
    /// <param name="players">The target players the event is sent to. Values less than 0 are special types {ALL,OTHERS,MASTER_CLIENT} see SendCodes for more details.</param>
    public delegate void BroadcastNetworkEvent(byte networkEventCode, string jsonData, int[] players);
    /// <summary>
    /// Called when the NetworkAnchorService needs data to be sent to remote players.
    /// </summary>
    public BroadcastNetworkEvent OnBroadcastNetworkEvent;
    /// <summary>
    /// The Result of the Request, Result or Response.
    /// </summary>
    public enum ResultCode
    {
        UNKNOWN = 0,
        SUCCESS,
        NO_MATCHES_FOUND,
        FAILED = 100,
    }
    /// <summary>
    /// Used as a Player ID value in  BroadcastNetworkEvent, when the recipient IDs are not hard coded.
    /// </summary>
    public enum SendCode
    {
        MASTER_CLIENT = -1,
        OTHERS = -2,
        ALL =-3
    }

    /// <summary>
    /// How long requests have before they timeout.
    /// </summary>
    const int RequestTimeoutMs = 3000;

    /// <summary>
    /// When enabled, all info debug logs are written to the console.
    /// </summary>
    [SerializeField] [Tooltip("Logs the network messages that are received by the service")]
    private bool _logNetworkEventCodes = true;

    //Action called with debug information
    public Action<string, int> OnDebugLogInfo;


    void Awake()
    {
        //Set the instance of the network service for easy access in external scripts.
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            enabled = false;
            Debug.Log("More than one instance of Network Anchor Service found. Disabling");
        }
    }

    /// <summary>
    /// Call this function when receiving events from the network for the NetworkAnchorService to interpret. Messages without valid event codes ill be ignored.
    /// </summary>
    /// <param name="eventCode">The code for the NetworkAnchorService messages.</param>
    /// <param name="jsonData">String data in json format, that contains the message data.</param>
    public void ProcessNetworkEvents(byte eventCode, object jsonData)
    {
        if(_logNetworkEventCodes)
            OnDebugLogInfo?.Invoke("Received Message with code", eventCode);


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
    /// <param name="jsonData">The event data json file that will be parsed.</param>
    /// <param name="players">The target for the Network event.</param>
    private void SendNetworkEvent(byte networkEventCode, string jsonData, int[] players)
    {
        OnBroadcastNetworkEvent?.Invoke(networkEventCode, jsonData, players);
    }

    /// <summary>
    /// Logic for locating an existing network anchor. 
    /// </summary>
    #region GetNetworkAnchor

    //Sent by the local player to remote player(s).
    public class GetNetworkAnchorRequest
    {
        public const byte EventCode = 101;
        public int SenderId;
    }

    //Sent as a response to any anchor request.
    //Includes the players coordinates and localized network anchor.
    public class GetNetworkAnchorResponse
    {
        public const byte EventCode = 102;
        public ResultCode ResultCode;
        public int SenderId;
        public NetworkAnchor NetworkAnchor;
        public List<GenericCoordinateReference> GenericCoordinates = new List<GenericCoordinateReference>();
    }

    //Returned value of the request.
    public class GetNetworkAnchorResult
    {
        /// <summary>
        /// Result of the request. Will return unsuccessful if no player is localized or if the local player does not have
        /// any coordinates to compare.
        /// </summary>
        public ResultCode ResultCode;
        /// <summary>
        /// The local players network anchor.
        /// </summary>
        public NetworkAnchor NetworkAnchor;
    }

    //completes when the server responds or when the call runs too long and times out
    TaskCompletionSource<GetNetworkAnchorResponse> _getNetworkAnchorResponseCompletionSource;

    public async Task<GetNetworkAnchorResult> RequestNetworkAnchor()
    {
            OnDebugLogInfo?.Invoke("Requesting the remote Network Anchor", GetNetworkAnchorRequest.EventCode);

        //First we request the anchors from our coordinate provider
        var genericCoordinatesRequest = _genericCoordinateProvider.RequestCoordinateReferences(true);

         await genericCoordinatesRequest;

        OnDebugLogInfo?.Invoke("Local coordinates received successfully", GetNetworkAnchorRequest.EventCode);

        if (!genericCoordinatesRequest.IsCompleted || genericCoordinatesRequest.Result == null)
        {
            Debug.LogError("Generic coordinates could not be found.");

            return (new GetNetworkAnchorResult()
            { ResultCode = ResultCode.FAILED });
        }

        //We cache the generic coordinates so they can be referenced again in the future.
        _genericCoordinateReferences = genericCoordinatesRequest.Result;

        //Iterate through each player and request their network anchor and coordinates.
        //If they return both, we create a network anchor locally and position it relative to a shared coordinate.
        for (int i = 0; i < _connectedPlayers.Count; i++)
        {
            //We ignore ourselves
            if (_connectedPlayers[i] == _localPlayerId)
                continue;

            //Create a new request with our ID
            var clientRequest = new GetNetworkAnchorRequest()
            { SenderId = _localPlayerId };

            //The result will be set when we catch a network event with the response
            _getNetworkAnchorResponseCompletionSource = new TaskCompletionSource<GetNetworkAnchorResponse>();
           
            OnDebugLogInfo?.Invoke("Requesting network anchor from player {ID: " + _connectedPlayers[i] + "}", GetNetworkAnchorRequest.EventCode);

            //Send the network event.
            SendNetworkEvent(GetNetworkAnchorRequest.EventCode, JsonUtility.ToJson(clientRequest), new int[] { _connectedPlayers[i] });

            //Wait for players to respond, or timeout.
            var content =
                await TaskWithTimeout(_getNetworkAnchorResponseCompletionSource.Task,TimeSpan.FromMilliseconds(RequestTimeoutMs));

            //If the result is null, check the other players.
            if (content==null)
            {
                //Set the result to null to stop the task without throwing a cancel exception
                _getNetworkAnchorResponseCompletionSource.TrySetResult(null);
                OnDebugLogInfo?.Invoke("Did not get a response in time, connection failed. Continuing...", GetNetworkAnchorRequest.EventCode);
                continue;
            }

            //If out network call results in a value, check if the call was successful
            if (_getNetworkAnchorResponseCompletionSource.Task.Result != null
                && _getNetworkAnchorResponseCompletionSource.Task.Result.ResultCode == ResultCode.SUCCESS)
            {
                //Reference the remote player's coordinates 
                var remoteCoordinates = _getNetworkAnchorResponseCompletionSource.Task.Result.GenericCoordinates;

                //Reference the remote player's network anchor
                var remoteNetworkAnchor = _getNetworkAnchorResponseCompletionSource.Task.Result.NetworkAnchor;

                OnDebugLogInfo?.Invoke("Received network anchor from player {ID : " + _connectedPlayers[i], GetNetworkAnchorRequest.EventCode);

                //Use our utility call to check if the player has a common PCF and if a local anchor can be created.
                if (TryGetNetworkAnchor(_genericCoordinateReferences, remoteCoordinates, remoteNetworkAnchor,
                    out NetworkAnchor localNetworkAnchor))
                {
        
                    OnDebugLogInfo?.Invoke("Remote anchor is valid and has been located locally.", GetNetworkAnchorRequest.EventCode);

                    //Cache the local network anchor for easy access.
                    //Setting the value will call an event
                    LocalNetworkAnchor = localNetworkAnchor;
                    return (new GetNetworkAnchorResult()
                        { ResultCode = ResultCode.SUCCESS, NetworkAnchor = LocalNetworkAnchor });
                }
                else
                {
                    OnDebugLogInfo?.Invoke("Local player did not share coordinates with the remote player.", GetNetworkAnchorRequest.EventCode);
             
                }
            }
        }

        OnDebugLogInfo?.Invoke("Network Anchor could not be found / does not exist.", GetNetworkAnchorRequest.EventCode);
        return (new GetNetworkAnchorResult()
        { ResultCode = ResultCode.NO_MATCHES_FOUND, NetworkAnchor = LocalNetworkAnchor });

    }

    //Called when a remote player requests an anchor from (us) the local player.
    private void ProcessNetworkAnchorRequest(GetNetworkAnchorRequest request)
    {
        GetNetworkAnchorResponse response = null;
        //We only return a failed response if we do not have a local network anchor and local coordinates.
        if (LocalNetworkAnchor == null || string.IsNullOrEmpty(LocalNetworkAnchor.AnchorId)
            && _genericCoordinateReferences == null || _genericCoordinateReferences.Count == 0)
        {
            response = new GetNetworkAnchorResponse()
            {
                ResultCode = ResultCode.FAILED,GenericCoordinates = _genericCoordinateReferences,  SenderId = _localPlayerId
            };
            //Send the response to the network
            OnBroadcastNetworkEvent?.Invoke(GetNetworkAnchorResponse.EventCode, JsonUtility.ToJson(response), new[] { request.SenderId });

            OnDebugLogInfo?.Invoke("A player has requested your coordinates and network anchor, but the you have not localized.", GetNetworkAnchorRequest.EventCode);

            return;
        }

        //If we have a network anchor and stored coordinates return the data and a success result code.
        response = new GetNetworkAnchorResponse()
        { ResultCode = ResultCode.SUCCESS, GenericCoordinates = _genericCoordinateReferences, NetworkAnchor = LocalNetworkAnchor, SenderId = _localPlayerId };
        
        OnDebugLogInfo?.Invoke("Sending local coordinates and network anchor to remote player.", GetNetworkAnchorRequest.EventCode);

        //Network events are sent as json.
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

        OnDebugLogInfo?.Invoke("Requesting to create a new Network Anchor.", CreateNetworkAnchorRequest.EventCode);

        _createNetworkAnchorResponseCompletionSource = new TaskCompletionSource<CreateNetworkAnchorResponse>();

        var genericCoordinatesRequest = _genericCoordinateProvider.RequestCoordinateReferences(true);

        await genericCoordinatesRequest;

        if (!genericCoordinatesRequest.IsCompleted || genericCoordinatesRequest.Result == null)
        {
         
            OnDebugLogInfo?.Invoke("Generic coordinates could not be found.", CreateNetworkAnchorRequest.EventCode);

            _createNetworkAnchorResponseCompletionSource.SetCanceled();
            return (new CreateNetworkAnchorResult()
            { ResultCode = ResultCode.FAILED });
        }

        _genericCoordinateReferences = genericCoordinatesRequest.Result;

        var newNetworkAnchor = new NetworkAnchor(id, _genericCoordinateReferences[0], position, rotation);

        var createAnchorRequest = new CreateNetworkAnchorRequest()
        { GenericCoordinates = _genericCoordinateReferences, NetworkAnchor = newNetworkAnchor, SenderId = _localPlayerId };

        SendNetworkEvent(CreateNetworkAnchorRequest.EventCode, JsonUtility.ToJson(createAnchorRequest), new int[] { (int)SendCode.OTHERS});

        OnDebugLogInfo?.Invoke("Notifying others about the new Network Anchor.", CreateNetworkAnchorRequest.EventCode);

        //TODO: change logic to check if the request has been sent successfully rather than making every client respond.
        var content =
            await TaskWithTimeout(_createNetworkAnchorResponseCompletionSource.Task, TimeSpan.FromMilliseconds(RequestTimeoutMs));

        if (content == null)
        {
            Debug.LogError("Could not get coordinates");
            OnDebugLogInfo?.Invoke("ERROR:Could not get coordinates!", CreateNetworkAnchorRequest.EventCode);

            _createNetworkAnchorResponseCompletionSource.SetCanceled();
            return (new CreateNetworkAnchorResult()
                { ResultCode = ResultCode.FAILED });
        }

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

        OnDebugLogInfo?.Invoke("Connecting...", ConnectToServiceRequest.EventCode);

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
            await TaskWithTimeout(_connectToServiceResponseCompletionSource.Task, TimeSpan.FromMilliseconds(RequestTimeoutMs));
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

  
        OnDebugLogInfo?.Invoke("Connection to Network Anchors Successful", ConnectToServiceRequest.EventCode);

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
            {
                playerId = _localPlayerId;
                //If we are the target, also disable the coordinates and clear local values
                _genericCoordinateProvider.DisableGenericCoordinates();
                _localNetworkAnchor = null;
                _genericCoordinateReferences = new List<GenericCoordinateReference>();
            }

            var request = new DisconnectFromServiceRequest() { SenderId = playerId };
            SendNetworkEvent(DisconnectFromServiceRequest.EventCode, JsonUtility.ToJson(request), new int[] { (int)SendCode.MASTER_CLIENT});
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

    /// <summary>
    /// Try to create a network anchor based on data from another player.
    /// </summary>
    /// <param name="localCoordinateReferences">The local coordinates.</param>
    /// <param name="remoteCoordinates">The remote player's coordinates.</param>
    /// <param name="remoteNetworkAnchor">The remote player's Network Anchor.</param>
    /// <param name="localNetworkAnchor">The resulting local network anchor.</param>
    /// <returns>Returns true if the remote player has a network anchor and the local and remote players have at least one matching coordinate and</returns>
    private static bool TryGetNetworkAnchor(List<GenericCoordinateReference> localCoordinateReferences,
        List<GenericCoordinateReference> remoteCoordinates,
        NetworkAnchor remoteNetworkAnchor, out NetworkAnchor localNetworkAnchor)
    {
        localNetworkAnchor = null;
        //Find a local coordinate that we share by comparing the coordinate IDs
        var sharedCoordinate = localCoordinateReferences.FirstOrDefault(x =>
            remoteCoordinates.Any(j => j.CoordinateId == x.CoordinateId));

        if (sharedCoordinate != null && NetworkAnchor.IsValid(remoteNetworkAnchor))
        {
            //If we share a coordinate, find the remote player's coordinate.
            //This is required because the world positions are different for each player.
            var remoteSharedCoordinate = remoteCoordinates.Find(x => x.CoordinateId == sharedCoordinate.CoordinateId);

            //Since the world position of the network anchor is different for each player, we create a new network anchor by
            //finding the anchors relative position to a coordinate that is shared by both players (The relative position is the same).
            //We can then use the relative position to determine the world position of the anchor for the local player.
            
            localNetworkAnchor = new NetworkAnchor(remoteNetworkAnchor.AnchorId, sharedCoordinate, remoteSharedCoordinate,
                remoteNetworkAnchor.GetWorldPosition(),
                remoteNetworkAnchor.GetWorldRotation());

            return true;
        }

        return false;

    }

}
