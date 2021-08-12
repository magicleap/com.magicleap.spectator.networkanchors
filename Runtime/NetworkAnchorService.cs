using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkAnchorService : MonoBehaviour
{
    private List<PlayerPcfReference> playerCoordinateReferences = new List<PlayerPcfReference>();

    //The LocalPlayers ID
    public string _localPlayerId;
    //The Coordinate Service that is being used
    public IGenericCoordinateProvider GenericCoordinateProvider;
    //Local Player Start Service Event
    public Action<string,IGenericCoordinateProvider> OnServiceStarted;

    //Network Response actions
    public Action<UploadCoordinatesResponse> OnCoordinatesUploaded;
    public Action<CreateAnchorResponse> OnNetworkAnchorCreated;
    public Action<SharedAnchorResponse> OnReceiveNetworkAnchor;
    public Action<GetHostCoordinatesResponse> OnReceiveHostedCoordinates;

    /// <summary>
    /// Function used to send the network events to the client and server
    /// </summary>
    /// <param name="networkEventCode">The network event code that is referenced to ensure the current parser for the data</param>
    /// <param name="jsonData">The event data json file that will be parsed</param>
    /// <param name="players">The target for the Network event. int[0] = All , int[1]{-1} = MasterClient , int[i>0] = Target Player Id's </param>
    public delegate void BroadcastNetworkEvent(byte networkEventCode, string jsonData, int[] players);
    public BroadcastNetworkEvent OnBroadcastNetworkEvent;


    private PlayerPcfReference _localPcfReferences;

    //Client Request Event Codes
    public const byte UploadCoordinatesRequestEventCode = 101;
    public const byte CreateAnchorRequestEventCode = 102;
    public const byte SharedAnchorRequestEventCode = 103;
    public const byte GetHostCoordinatesRequestEventCode = 104;
    public class DownloadRemoteCoordinatesRequest
    {
        public string SenderId;
        public string TargetId;
    }
    public class HasNetworkAnchorRequest
    {
        public const byte GetHostCoordinatesRequestEventCode = 105;
        public string SenderId;
        public string TargetId;
    }
 

    //Server Responses
    public class UploadCoordinatesResponse
    {
        public const byte EventCode = 11;
        public ResultCode ResponseCode;
    }
    public class CreateAnchorResponse
    {
        public const byte EventCode = 12;
        public ResultCode ResponseCode;
        public NetworkAnchor NetworkAnchor;
    }
    public class SharedAnchorResponse
    {
        public const byte EventCode = 13;
        public ResultCode ResponseCode;
        public NetworkAnchor NetworkAnchor;
    }
    public class GetHostCoordinatesResponse
    {
        public const byte EventCode = 14;
        public ResultCode ResponseCode;
        public PlayerPcfReference PlayerPcfReference;
    }
    public class HasNetworkAnchorResponse
    {
        public const byte EventCode = 15;
        public bool Value;
        public ResultCode ResponseCode;
    }

    //Client request tasks. 
    TaskCompletionSource<UploadCoordinatesResponse> _uploadCoordinatesCompletionSource;
    TaskCompletionSource<CreateAnchorResponse> _createNetworkAnchorCompletionSource;
    TaskCompletionSource<SharedAnchorResponse> _sharedAnchorRequestCompletionSource;
    TaskCompletionSource<GetHostCoordinatesResponse> _downloadHostCoordinatesCompletionSource;
    TaskCompletionSource<HasNetworkAnchorResponse> _hasNetworkAnchorRequestCompletionSource;

    private bool _debug = true;

    //Response Result Codes
    public enum ResultCode
    {
        UNKNOWN = 0,
        SUCCESS,
        EXISTS,
        MISSING_INFORMATION,
        MISSING_ANCHOR,
        MISSING_SHARED_COORDINATE,
        MISSING_COORDINATES,
        FAILED = 100
    }

    public void StartService(string playerId, IGenericCoordinateProvider coordinateProvider)
    {
        _localPlayerId = playerId;
        GenericCoordinateProvider = coordinateProvider;
        GenericCoordinateProvider.InitializeGenericCoordinates();
        OnServiceStarted?.Invoke(playerId,coordinateProvider);
    }

    private void OnDestroy()
    {
        GenericCoordinateProvider?.DisableGenericCoordinates();
    }

    //Process Network Events
    public void ProcessNetworkEvents(byte eventCode, object jsonData)
    {
        #region Client

        if (eventCode == SharedAnchorResponse.EventCode)
        {
            SharedAnchorResponse result = JsonUtility.FromJson<SharedAnchorResponse>((string)jsonData);
            _sharedAnchorRequestCompletionSource.TrySetResult(result);
            OnReceiveNetworkAnchor?.Invoke(result);
        }

        if (eventCode == CreateAnchorResponse.EventCode)
        {
            CreateAnchorResponse result = JsonUtility.FromJson<CreateAnchorResponse>((string)jsonData);
            _createNetworkAnchorCompletionSource.TrySetResult(result);
            OnNetworkAnchorCreated?.Invoke(result);
        }

        if (eventCode == UploadCoordinatesResponse.EventCode)
        {
            UploadCoordinatesResponse result = JsonUtility.FromJson<UploadCoordinatesResponse>((string)jsonData);
            _uploadCoordinatesCompletionSource.TrySetResult(result);
            OnCoordinatesUploaded?.Invoke(result);
        }

        if (eventCode == GetHostCoordinatesResponse.EventCode)
        {
            if(_debug)
                Debug.Log("Got download network anchor response from server { " + jsonData + "}");

            GetHostCoordinatesResponse result = JsonUtility.FromJson<GetHostCoordinatesResponse>((string)jsonData);
            if (_debug)
                Debug.Log("Result = " + result.ResponseCode);

            _downloadHostCoordinatesCompletionSource.TrySetResult(result);
            OnReceiveHostedCoordinates?.Invoke(result);
        }

        #endregion

        #region Server

        if (eventCode == UploadCoordinatesRequestEventCode)
        {
            ProcessCoordinatesUpload((string)jsonData);
        }

        if (eventCode == CreateAnchorRequestEventCode)
        {
            ProcessNetworkAnchorCreation((string)jsonData);
        }

        if (eventCode == SharedAnchorRequestEventCode)
        {
            ProcessGetSharedNetworkAnchorRequest((string)jsonData);
        }

        if (eventCode == GetHostCoordinatesRequestEventCode)
        {
            if (_debug)
                Debug.Log("Received get host coordinates request");

            ProcessDownloadPlayerCoordinatesRequest((string)jsonData);
        }
        #endregion
    }

    //Client Requests Logic
    public async Task<SharedAnchorResponse> RequestSharedNetworkAnchor()
    {
        _sharedAnchorRequestCompletionSource = new TaskCompletionSource<SharedAnchorResponse>();

        var genericCoordinates = GenericCoordinateProvider.RequestCoordinateReferences(true);

        while (genericCoordinates.Status != TaskStatus.RanToCompletion)
            await Task.Delay(100);

        if (genericCoordinates.IsCompleted || genericCoordinates.Result == null)
        {
            Debug.LogError("Generic coordinates could not be found.");
            _sharedAnchorRequestCompletionSource.TrySetResult(new SharedAnchorResponse()
                {ResponseCode = ResultCode.FAILED});

            return _sharedAnchorRequestCompletionSource.Task.Result;
        }

        var playerReferenceCoordinates = new PlayerPcfReference()
        { PlayerId = _localPlayerId, CoordinateReferences = genericCoordinates.Result };

        _localPcfReferences = playerReferenceCoordinates;

        SendNetworkEvent(SharedAnchorRequestEventCode, JsonUtility.ToJson(playerReferenceCoordinates), new int[] { -1 });

        while (!_sharedAnchorRequestCompletionSource.Task.IsCompleted)
            await Task.Delay(100);

        return _sharedAnchorRequestCompletionSource.Task.Result;
    }

    public async Task<UploadCoordinatesResponse> RequestUploadCoordinates()
    {
        _uploadCoordinatesCompletionSource = new TaskCompletionSource<UploadCoordinatesResponse>();
        var genericCoordinates = GenericCoordinateProvider.RequestCoordinateReferences(true);

        while (genericCoordinates.Status != TaskStatus.RanToCompletion)
            await Task.Delay(100);


        if (genericCoordinates.IsCompleted || genericCoordinates.Result == null)
        {
            Debug.LogError("Generic coordinates could not be found.");
            _uploadCoordinatesCompletionSource.TrySetResult(new UploadCoordinatesResponse()
                { ResponseCode = ResultCode.FAILED });

            return _uploadCoordinatesCompletionSource.Task.Result;
        }

        _localPcfReferences = new PlayerPcfReference() { PlayerId = _localPlayerId, CoordinateReferences = genericCoordinates.Result };

        SendNetworkEvent(UploadCoordinatesRequestEventCode, JsonUtility.ToJson(_localPcfReferences), new int[] { -1 });

        while (!_uploadCoordinatesCompletionSource.Task.IsCompleted)
            await Task.Delay(100);

        return _uploadCoordinatesCompletionSource.Task.Result;
    }

    public async Task<CreateAnchorResponse> RequestCreateNetworkAnchor(NetworkAnchor networkAnchor)
    {
        _createNetworkAnchorCompletionSource = new TaskCompletionSource<CreateAnchorResponse>();

        SendNetworkEvent(CreateAnchorRequestEventCode, JsonUtility.ToJson(networkAnchor), new int[1] { -1 });

        while (!_createNetworkAnchorCompletionSource.Task.IsCompleted)
            await Task.Delay(100);

        return _createNetworkAnchorCompletionSource.Task.Result;
    }

    public async Task<GetHostCoordinatesResponse> RequestDownloadRemoteCoordinates(string targetPlayer = "")
    {
        _downloadHostCoordinatesCompletionSource = new TaskCompletionSource<GetHostCoordinatesResponse>();

        var request = new DownloadRemoteCoordinatesRequest() {SenderId = _localPlayerId, TargetId = targetPlayer};
        SendNetworkEvent(GetHostCoordinatesRequestEventCode, JsonUtility.ToJson(request), new int[1] { -1 });

        while (!_downloadHostCoordinatesCompletionSource.Task.IsCompleted)
            await Task.Delay(100);

        return _downloadHostCoordinatesCompletionSource.Task.Result;
    }

    public async Task<HasNetworkAnchorResponse> RequestHasNetworkAnchor(string targetPlayer = "")
    {
        _hasNetworkAnchorRequestCompletionSource = new TaskCompletionSource<HasNetworkAnchorResponse>();

        var request = new HasNetworkAnchorRequest() { SenderId = _localPlayerId, TargetId = targetPlayer };
        SendNetworkEvent(HasNetworkAnchorRequest.GetHostCoordinatesRequestEventCode, JsonUtility.ToJson(request), new int[1] { -1 });

        while (!_hasNetworkAnchorRequestCompletionSource.Task.IsCompleted)
            await Task.Delay(100);

        return _hasNetworkAnchorRequestCompletionSource.Task.Result;
    }

    //End

    //Server Responses Logic
    private void ProcessCoordinatesUpload(string playerCoordinatesJson)
    {
        var playerCoordinates = JsonUtility.FromJson(playerCoordinatesJson, typeof(PlayerPcfReference)) as PlayerPcfReference;
        if (playerCoordinates == null)
        {
            Debug.LogError($"A player has uploaded invalid data as coordinates {playerCoordinatesJson}");
           var result = JsonUtility.ToJson(new UploadCoordinatesResponse()
                { ResponseCode = ResultCode.MISSING_INFORMATION });

            SendNetworkEvent(UploadCoordinatesResponse.EventCode, result, new int[] { int.Parse(playerCoordinates.PlayerId) });

        }
        else
        {
            AddOrUpdatePlayerCoordinates(playerCoordinates);

            playerCoordinateReferences.Add(playerCoordinates);
             var result = JsonUtility.ToJson(new UploadCoordinatesResponse()
                { ResponseCode = ResultCode.SUCCESS });

            SendNetworkEvent(UploadCoordinatesResponse.EventCode, result, new int[] { int.Parse(playerCoordinates.PlayerId) });
        }
    }

    private void AddOrUpdatePlayerCoordinates(PlayerPcfReference playerCoordinates)
    {
        string result = "";
        //Find the existing player or create a new one
        for (int i = 0; i < playerCoordinateReferences.Count; i++)
        {
            if (playerCoordinateReferences[i].PlayerId == playerCoordinates.PlayerId)
            {
                playerCoordinateReferences[i].CoordinateReferences = playerCoordinates.CoordinateReferences;
                break;
            }
        }

        if (string.IsNullOrEmpty(result))
        {
            playerCoordinateReferences.Add(playerCoordinates);
    
        }
    }

    private void ProcessNetworkAnchorCreation(string networkAnchorJson)
    {
        var targetAnchor = JsonUtility.FromJson(networkAnchorJson, typeof(NetworkAnchor)) as NetworkAnchor;

        bool isValid = !string.IsNullOrEmpty(targetAnchor.AnchorId)
                       && targetAnchor.LinkedCoordinate != null
                       && !string.IsNullOrEmpty(targetAnchor.LinkedCoordinate.CoordinateId);

        PlayerPcfReference playerCoordinates = null;
        for (int i = 0; i < playerCoordinateReferences.Count; i++)
        {
            if (playerCoordinateReferences[i].PlayerId == targetAnchor.OwnerId)
            {
                playerCoordinates = playerCoordinateReferences[i];
            }
        }

        if (playerCoordinates != null && !playerCoordinates.NetworkAnchorIsValid)
        {
            if (!isValid)
            {
                string result = JsonUtility.ToJson(new CreateAnchorResponse()
                { ResponseCode = ResultCode.MISSING_INFORMATION, NetworkAnchor = targetAnchor });

                SendNetworkEvent(CreateAnchorResponse.EventCode, result, new[] { int.Parse(targetAnchor.OwnerId) });
            }
            else
            {
                playerCoordinates.NetworkAnchor = targetAnchor;

                string result = JsonUtility.ToJson(new CreateAnchorResponse()
                { ResponseCode = ResultCode.SUCCESS, NetworkAnchor = targetAnchor });

                SendNetworkEvent(CreateAnchorResponse.EventCode, result, new int[0]);
            }
        }
        else
        {

            string result = JsonUtility.ToJson(new CreateAnchorResponse()
            { ResponseCode = isValid ? ResultCode.EXISTS : ResultCode.FAILED, NetworkAnchor = targetAnchor });

            SendNetworkEvent(CreateAnchorResponse.EventCode, result, new[] { int.Parse(targetAnchor.OwnerId) });
        }
    }

    private void ProcessGetSharedNetworkAnchorRequest(string playerReferenceCoordinates)
    {
        GenericCoordinateReference coordinateReference = null;
        PlayerPcfReference matchedPlayerCoordinates = null;

        PlayerPcfReference playerReference = JsonUtility.FromJson<PlayerPcfReference>(playerReferenceCoordinates) as PlayerPcfReference;

        //If we do not have any uploaded coordinates return false
        if (playerCoordinateReferences.Count == 0)
        {
            string anchorResultJson = JsonUtility.ToJson(new SharedAnchorResponse() { NetworkAnchor = null, ResponseCode = ResultCode.MISSING_COORDINATES });
            SendNetworkEvent(SharedAnchorResponse.EventCode, anchorResultJson, new[] { int.Parse(playerReference.PlayerId) });
            return;
        }

        //If we do not have an active anchor return false
        if (!playerCoordinateReferences.Exists(x=>x.NetworkAnchorIsValid==true))
        {
            string anchorResultJson = JsonUtility.ToJson(new SharedAnchorResponse() { NetworkAnchor = null, ResponseCode = ResultCode.MISSING_ANCHOR });
            SendNetworkEvent(SharedAnchorResponse.EventCode, anchorResultJson, new[] { int.Parse(playerReference.PlayerId) });
            return;
        }

        //We add or update the current players coordinates
        AddOrUpdatePlayerCoordinates(playerReference);
        var pcfIds = playerReference.CoordinateReferences.Select(x => x.CoordinateId).ToList();

        for (int i = 0; i < playerCoordinateReferences.Count; i++)
        {
            matchedPlayerCoordinates = playerCoordinateReferences[i];
            var coordinateReferences = playerCoordinateReferences[i].CoordinateReferences;
            for (int j = 0; j < coordinateReferences.Count; j++)
            {
                if (pcfIds.Contains(coordinateReferences[j].CoordinateId))
                {
                    coordinateReference = coordinateReferences[j];
                    break;
                }
            }
        }

        if (coordinateReference == null)
        {
            // TODO: Network call to the players to get new anchors and try again?
            string anchorResultJson = JsonUtility.ToJson(new SharedAnchorResponse() { NetworkAnchor = null, ResponseCode = ResultCode.MISSING_SHARED_COORDINATE });
            SendNetworkEvent(SharedAnchorResponse.EventCode, anchorResultJson, new[] { int.Parse(playerReference.PlayerId) });
        }
        else
        {
            if (_debug)
                Debug.Log($"Player: {playerReference.PlayerId} coordinates with Player:{matchedPlayerCoordinates.PlayerId}. Original Network Anchor {matchedPlayerCoordinates.NetworkAnchor}");

            var resultAnchor = new NetworkAnchor(matchedPlayerCoordinates.NetworkAnchor.AnchorId, coordinateReference, matchedPlayerCoordinates.NetworkAnchor.GetWorldPosition(), matchedPlayerCoordinates.NetworkAnchor.GetWorldRotation());

            if (_debug)
                Debug.Log("Returning Network Anchor to player " + JsonUtility.ToJson(resultAnchor));

            string anchorResultJson = JsonUtility.ToJson(new SharedAnchorResponse() { NetworkAnchor = resultAnchor, ResponseCode = ResultCode.SUCCESS });
            SendNetworkEvent(SharedAnchorResponse.EventCode, anchorResultJson, new[] { int.Parse(playerReference.PlayerId) });
        }

    }

    private void ProcessDownloadPlayerCoordinatesRequest(string jsonRequest)
    {
        DownloadRemoteCoordinatesRequest request = JsonUtility.FromJson<DownloadRemoteCoordinatesRequest>(jsonRequest);

        if (playerCoordinateReferences.Count > 0)
        {
            PlayerPcfReference targetPlayerReference = null;
            if (request.TargetId == "")
            {
                targetPlayerReference = playerCoordinateReferences[0];
            }
            else
            {
                targetPlayerReference = playerCoordinateReferences.FirstOrDefault(x => x.PlayerId == request.TargetId);
                if (_debug)
                    Debug.Log($"Search for anchors from player {request.TargetId} Returned : {targetPlayerReference !=null}");
                 
            }

            if (targetPlayerReference != null)
            {
                var resultData = new GetHostCoordinatesResponse()
                {
                    PlayerPcfReference = targetPlayerReference,
                    ResponseCode = ResultCode.SUCCESS
                };

                if (_debug)
                    Debug.Log("Getting network anchors was successful");

                SendNetworkEvent(GetHostCoordinatesResponse.EventCode, JsonUtility.ToJson(resultData), new[] { int.Parse(request.SenderId) });
            }
          
        }
        else
        {
            var resultData = new GetHostCoordinatesResponse()
            {
                ResponseCode = ResultCode.MISSING_COORDINATES
            };

            if (_debug)
                Debug.Log("Could not get network anchors, ResultCode.MISSING_COORDINATES");

            SendNetworkEvent(GetHostCoordinatesResponse.EventCode, JsonUtility.ToJson(resultData), new[] { int.Parse(request.TargetId) });
        }
    }

    private void ProcessHasNetworkAnchorRequest(string jsonRequest)
    {
        DownloadRemoteCoordinatesRequest request = JsonUtility.FromJson<DownloadRemoteCoordinatesRequest>(jsonRequest);

        if (playerCoordinateReferences.Count > 0)
        {
            PlayerPcfReference targetPlayerReference = null;
            if (request.TargetId == "")
            {
                targetPlayerReference = playerCoordinateReferences[0];
            }
            else
            {
                targetPlayerReference = playerCoordinateReferences.FirstOrDefault(x => x.PlayerId == request.TargetId);
                if (_debug)
                    Debug.Log($"Search for anchors from player {request.TargetId} Returned : {targetPlayerReference != null}");

            }

            if (targetPlayerReference != null)
            {
                var resultData = new GetHostCoordinatesResponse()
                {
                    PlayerPcfReference = targetPlayerReference,
                    ResponseCode = ResultCode.SUCCESS
                };

                if (_debug)
                    Debug.Log("Getting network anchors was successful");

                SendNetworkEvent(GetHostCoordinatesResponse.EventCode, JsonUtility.ToJson(resultData), new[] { int.Parse(request.SenderId) });
            }

        }
        else
        {
            var resultData = new GetHostCoordinatesResponse()
            {
                ResponseCode = ResultCode.MISSING_COORDINATES
            };

            if (_debug)
                Debug.Log("Could not get network anchors, ResultCode.MISSING_COORDINATES");

            SendNetworkEvent(GetHostCoordinatesResponse.EventCode, JsonUtility.ToJson(resultData), new[] { int.Parse(request.TargetId) });
        }
    }
    //End



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

}
